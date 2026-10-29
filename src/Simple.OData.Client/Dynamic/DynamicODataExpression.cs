using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

/// <summary>
/// An <see cref="ODataExpression"/> that participates in C# <see langword="dynamic"/> binding: property access
/// builds reference paths, method calls build OData functions and operators build comparisons.
/// </summary>
public sealed class DynamicODataExpression : ODataExpression, IDynamicMetaObjectProvider
{
	private static readonly MethodInfo GetMemberMethod = Method(nameof(GetMember));
	private static readonly MethodInfo SetMemberMethod = Method(nameof(SetMember));
	private static readonly MethodInfo InvokeMethod = Method(nameof(Invoke));
	private static readonly MethodInfo BinaryMethod = Method(nameof(Binary));

	internal DynamicODataExpression()
	{
	}

	private DynamicODataExpression(string reference)
		: base(reference)
	{
	}

	private DynamicODataExpression(string reference, object? value)
		: base(reference, value)
	{
	}

	private DynamicODataExpression(ExpressionFunction function)
		: base(function)
	{
	}

	private DynamicODataExpression(ODataExpression? left, ODataExpression? right, ExpressionType expressionOperator)
		: base(left, right, expressionOperator)
	{
	}

	private DynamicODataExpression(ODataExpression caller, string reference)
		: base(caller, reference)
	{
	}

	private DynamicODataExpression(ODataExpression caller, ExpressionFunction function)
		: base(caller, function)
	{
	}

	/// <inheritdoc />
	public DynamicMetaObject GetMetaObject(Expression parameter) => new MetaObject(parameter, this);

	private static MethodInfo Method(string name) =>
		typeof(DynamicODataExpression).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
		?? throw new MissingMethodException(nameof(DynamicODataExpression), name);

	private static bool IsAnyAll(string name) =>
		string.Equals(name, ODataLiteral.Any, StringComparison.OrdinalIgnoreCase) || string.Equals(name, ODataLiteral.All, StringComparison.OrdinalIgnoreCase);

	private static string? TypeOperationName(string name)
	{
		if (string.Equals(name, ODataLiteral.Is, StringComparison.OrdinalIgnoreCase) || string.Equals(name, ODataLiteral.IsOf, StringComparison.OrdinalIgnoreCase))
		{
			return ODataLiteral.IsOf;
		}

		if (string.Equals(name, ODataLiteral.As, StringComparison.OrdinalIgnoreCase) || string.Equals(name, ODataLiteral.Cast, StringComparison.OrdinalIgnoreCase))
		{
			return ODataLiteral.Cast;
		}

		return null;
	}

	// The binder caches one rule per call site, so everything that depends on the receiver instance
	// (its reference path, whether it is a cast) is decided here at run time rather than at bind time.
	private static DynamicODataExpression GetMember(DynamicODataExpression self, string name)
	{
		if (!self.IsNull && (FunctionMapping.ContainsFunction(name, 0) || self.Function?.FunctionName == ODataLiteral.Cast))
		{
			return new DynamicODataExpression(self, name);
		}

		return new DynamicODataExpression(string.IsNullOrEmpty(self.Reference) ? name : $"{self.Reference}/{name}");
	}

	private static DynamicODataExpression SetMember(string name, object? value) => new(name, value);

	private static DynamicODataExpression Invoke(DynamicODataExpression self, string name, object?[] arguments)
	{
		if (TypeOperationName(name) is { } typeOperation)
		{
			if (arguments.Length != 1)
			{
				throw new ArgumentException($"{name} expects a single type argument.", nameof(arguments));
			}

			return new DynamicODataExpression(new ExpressionFunction(typeOperation, [self.IsNull ? null : self, arguments[0]]));
		}

		return new DynamicODataExpression(self, new ExpressionFunction(name, arguments));
	}

	private static DynamicODataExpression Binary(DynamicODataExpression self, ExpressionType operation, object? right) =>
		new(self, new ODataExpression(right), operation);

	private sealed class MetaObject(Expression parameter, DynamicODataExpression value) : DynamicMetaObject(parameter, BindingRestrictions.Empty, value)
	{
		private Expression Self => Expression.Convert(Expression, LimitType);

		private BindingRestrictions TypeRestrictions => BindingRestrictions.GetTypeRestriction(Expression, LimitType);

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder) =>
			new(Expression.Call(GetMemberMethod, Self, Expression.Constant(binder.Name)), TypeRestrictions);

		public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) =>
			new(
				Expression.Call(SetMemberMethod, Expression.Constant(binder.Name), Expression.Convert(value.Expression, typeof(object))),
				TypeRestrictions.Merge(value.Restrictions));

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			var name = binder.Name;
			if (!FunctionMapping.ContainsFunction(name, args.Length) && !IsAnyAll(name) && TypeOperationName(name) is null)
			{
				return base.BindInvokeMember(binder, args);
			}

			var arguments = Expression.NewArrayInit(typeof(object), args.Select(x => Expression.Convert(x.Expression, typeof(object))));
			var restrictions = args.Aggregate(TypeRestrictions, (current, arg) => current.Merge(arg.Restrictions));
			return new DynamicMetaObject(Expression.Call(InvokeMethod, Self, Expression.Constant(name), arguments), restrictions);
		}

		public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
		{
			// enums have no implicit conversion to ODataExpression, so the C# binder cannot find the operators by itself
			if (arg.RuntimeType is not { IsEnum: true })
			{
				return base.BindBinaryOperation(binder, arg);
			}

			var call = Expression.Call(BinaryMethod, Self, Expression.Constant(binder.Operation), Expression.Convert(arg.Expression, typeof(object)));
			return new DynamicMetaObject(call, TypeRestrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.RuntimeType)));
		}
	}
}
