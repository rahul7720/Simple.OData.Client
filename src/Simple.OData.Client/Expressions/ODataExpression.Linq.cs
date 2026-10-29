using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

public partial class ODataExpression
{
	private static ODataExpression? ParseLinqExpression(Expression expression)
	{
		switch (expression.NodeType)
		{
			case ExpressionType.MemberAccess:
				return ParseMemberExpression((MemberExpression)expression);
			case ExpressionType.Call:
				return ParseCallExpression((MethodCallExpression)expression);
			case ExpressionType.Lambda:
				return ParseLinqExpression(((LambdaExpression)expression).Body);
			case ExpressionType.Quote:
				return ParseLinqExpression(((UnaryExpression)expression).Operand);
			case ExpressionType.Constant:
				return ParseConstantExpression((ConstantExpression)expression);
			case ExpressionType.Not:
			case ExpressionType.Convert:
			case ExpressionType.ConvertChecked:
			case ExpressionType.Negate:
			case ExpressionType.NegateChecked:
				return ParseUnaryExpression((UnaryExpression)expression);
			case ExpressionType.Equal:
			case ExpressionType.NotEqual:
			case ExpressionType.LessThan:
			case ExpressionType.LessThanOrEqual:
			case ExpressionType.GreaterThan:
			case ExpressionType.GreaterThanOrEqual:
			case ExpressionType.And:
			case ExpressionType.AndAlso:
			case ExpressionType.Or:
			case ExpressionType.OrElse:
			case ExpressionType.Add:
			case ExpressionType.AddChecked:
			case ExpressionType.Subtract:
			case ExpressionType.SubtractChecked:
			case ExpressionType.Multiply:
			case ExpressionType.MultiplyChecked:
			case ExpressionType.Divide:
			case ExpressionType.Modulo:
				return ParseBinaryExpression((BinaryExpression)expression);
			case ExpressionType.TypeIs:
				return ParseTypeIsExpression((TypeBinaryExpression)expression);
			case ExpressionType.TypeAs:
				return ParseTypeAsExpression((UnaryExpression)expression);
			case ExpressionType.Parameter:
				return null;
			case ExpressionType.New:
			case ExpressionType.NewArrayInit:
			case ExpressionType.NewArrayBounds:
			case ExpressionType.ArrayIndex:
			case ExpressionType.ListInit:
			case ExpressionType.MemberInit:
				return EvaluateIfClosed(expression) ?? throw Utils.NotSupportedExpression(expression);
			default:
				return EvaluateIfClosed(expression) ?? throw Utils.NotSupportedExpression(expression);
		}
	}

	private static ODataExpression ParseMemberExpression(MemberExpression memberExpression, Stack<MemberInfo>? memberChain = null)
	{
		if (memberExpression.Expression is null)
		{
			return new ODataExpression(EvaluateStaticMember(memberExpression));
		}

		memberChain ??= new Stack<MemberInfo>();
		memberChain.Push(memberExpression.Member);
		switch (memberExpression.Expression.NodeType)
		{
			case ExpressionType.Parameter:
				return FromReference(string.Join('.', memberChain.Select(x => x.GetMappedMemberName())));

			case ExpressionType.Constant:
				return ParseConstantExpression((ConstantExpression)memberExpression.Expression, memberChain);

			case ExpressionType.MemberAccess:
				var memberName = memberExpression.Member.GetMappedMemberName();
				if (FunctionMapping.ContainsFunction(memberName, 0) && IsParameterBound(memberExpression.Expression))
				{
					return FromFunction(memberName, ParseMemberExpression((MemberExpression)memberExpression.Expression), []);
				}

				return ParseMemberExpression((MemberExpression)memberExpression.Expression, memberChain);

			case ExpressionType.TypeAs:
			case ExpressionType.Convert:
				var inner = (UnaryExpression)memberExpression.Expression;
				if (!IsParameterBound(inner))
				{
					return new ODataExpression(EvaluateIfClosed(memberExpression)?.Value);
				}

				var cast = ParseTypeAsExpression(inner);
				return new ODataExpression(cast, string.Join('/', memberChain.Select(x => x.GetMappedMemberName())));

			default:
				return EvaluateIfClosed(memberExpression) ?? throw Utils.NotSupportedExpression(memberExpression);
		}
	}

	private static ODataExpression ParseCallExpression(MethodCallExpression callExpression)
	{
		if (callExpression.Object is null)
		{
			// static call: a built-in function on the first argument, or a value computed locally
			if (!IsParameterBound(callExpression))
			{
				if (callExpression.Method.DeclaringType == typeof(ODataFunctions))
				{
					return new ODataExpression(new ExpressionFunction(callExpression.Method.Name, callExpression.Arguments));
				}

				return EvaluateIfClosed(callExpression) ?? throw Utils.NotSupportedExpression(callExpression);
			}

			var target = callExpression.Arguments.FirstOrDefault() ?? throw Utils.NotSupportedExpression(callExpression);
			var arguments = callExpression.Method.DeclaringType == typeof(Convert)
				? callExpression.Arguments
				: callExpression.Arguments.Skip(1);
			return FromFunction(callExpression.Method.Name, ParseLinqExpression(target), arguments);
		}

		var parsedArguments = new List<ODataExpression?>(callExpression.Arguments.Count);
		foreach (var argument in callExpression.Arguments)
		{
			parsedArguments.Add(ParseCallArgumentExpression(argument));
		}

		var function = new ExpressionFunction(callExpression.Method.Name, parsedArguments);
		switch (callExpression.Object.NodeType)
		{
			case ExpressionType.MemberAccess:
				return new ODataExpression(ParseMemberExpression((MemberExpression)callExpression.Object), function);

			case ExpressionType.Call:
				if (string.Equals(callExpression.Method.Name, nameof(ToString), StringComparison.Ordinal))
				{
					return ParseCallExpression((MethodCallExpression)callExpression.Object);
				}

				return new ODataExpression(ParseCallExpression((MethodCallExpression)callExpression.Object), function);

			case ExpressionType.Constant:
				return new ODataExpression(ParseConstantExpression((ConstantExpression)callExpression.Object), function);

			case ExpressionType.Parameter:
				return new ODataExpression(function);

			default:
				if (!IsParameterBound(callExpression))
				{
					return EvaluateIfClosed(callExpression) ?? throw Utils.NotSupportedExpression(callExpression);
				}

				return new ODataExpression(ParseLinqExpression(callExpression.Object) ?? new ODataExpression(), function);
		}
	}

	private static ODataExpression? ParseCallArgumentExpression(Expression expression)
	{
		switch (expression.NodeType)
		{
			case ExpressionType.Constant:
				return ParseConstantExpression((ConstantExpression)expression);
			case ExpressionType.MemberAccess:
				return ParseMemberExpression((MemberExpression)expression);
			case ExpressionType.Convert:
			case ExpressionType.Quote:
				return ParseUnaryExpression((UnaryExpression)expression);
			case ExpressionType.Call:
				return ParseCallExpression((MethodCallExpression)expression);
			case ExpressionType.Lambda:
			case ExpressionType.Parameter:
				return ParseLinqExpression(expression);
			default:
				return IsParameterBound(expression)
					? ParseLinqExpression(expression)
					: EvaluateIfClosed(expression) ?? throw Utils.NotSupportedExpression(expression);
		}
	}

	private static ODataExpression ParseConstantExpression(ConstantExpression constExpression, Stack<MemberInfo>? members = null)
	{
		if (constExpression.Value is Expression nested)
		{
			return ParseLinqExpression(nested) ?? new ODataExpression();
		}

		if (members is null || members.Count == 0)
		{
			return new ODataExpression(constExpression.Value);
		}

		return new ODataExpression(EvaluateConstValue(constExpression.Value, members));
	}

	private static ODataExpression ParseUnaryExpression(UnaryExpression unaryExpression)
	{
		var operand = unaryExpression.Operand;
		switch (unaryExpression.NodeType)
		{
			case ExpressionType.Quote:
				return ParseLinqExpression(operand) ?? new ODataExpression();
			case ExpressionType.Not:
				return !(ParseLinqExpression(operand) ?? new ODataExpression());
			case ExpressionType.Negate:
			case ExpressionType.NegateChecked:
				return -(ParseLinqExpression(operand) ?? new ODataExpression());
			default:
				if (!IsParameterBound(operand))
				{
					var evaluated = EvaluateIfClosed(unaryExpression);
					if (evaluated is not null)
					{
						return evaluated;
					}
				}

				return new ODataExpression(ParseLinqExpression(operand) ?? new ODataExpression(), unaryExpression.Type);
		}
	}

	private static ODataExpression ParseBinaryExpression(BinaryExpression binaryExpression)
	{
		var leftExpression = ParseLinqExpression(binaryExpression.Left);
		var rightExpression = ParseLinqExpression(binaryExpression.Right);

		if (IsConvertFromCustomEnum(binaryExpression.Left, out var enumType))
		{
			return ParseBinaryExpression(leftExpression, ParseLinqExpression(Expression.Convert(binaryExpression.Right, enumType)), binaryExpression);
		}

		if (IsConvertFromCustomEnum(binaryExpression.Right, out enumType))
		{
			return ParseBinaryExpression(ParseLinqExpression(Expression.Convert(binaryExpression.Left, enumType)), rightExpression, binaryExpression);
		}

		return ParseBinaryExpression(leftExpression, rightExpression, binaryExpression);
	}

	private static ODataExpression ParseBinaryExpression(ODataExpression? left, ODataExpression? right, Expression operandExpression)
	{
		left ??= new ODataExpression();
		right ??= new ODataExpression();
		return operandExpression.NodeType switch
		{
			ExpressionType.Equal => left == right,
			ExpressionType.NotEqual => left != right,
			ExpressionType.LessThan => left < right,
			ExpressionType.LessThanOrEqual => left <= right,
			ExpressionType.GreaterThan => left > right,
			ExpressionType.GreaterThanOrEqual => left >= right,
			ExpressionType.And or ExpressionType.AndAlso => left && right,
			ExpressionType.Or or ExpressionType.OrElse => left || right,
			ExpressionType.Add or ExpressionType.AddChecked => left + right,
			ExpressionType.Subtract or ExpressionType.SubtractChecked => left - right,
			ExpressionType.Multiply or ExpressionType.MultiplyChecked => left * right,
			ExpressionType.Divide => left / right,
			ExpressionType.Modulo => left % right,
			_ => throw Utils.NotSupportedExpression(operandExpression),
		};
	}

	private static bool IsConvertFromCustomEnum(Expression expression, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? enumType)
	{
		enumType = null;
		if (expression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
		{
			var operandType = ((UnaryExpression)expression).Operand.Type;
			operandType = Nullable.GetUnderlyingType(operandType) ?? operandType;
			if (operandType.IsEnum && !Utils.IsSystemType(operandType))
			{
				enumType = operandType;
				return true;
			}
		}

		return false;
	}

	private static ODataExpression ParseTypeIsExpression(TypeBinaryExpression typeIsExpression)
	{
		var target = ParseLinqExpression(typeIsExpression.Expression);
		return FromFunction(new ExpressionFunction(ODataLiteral.IsOf, [target, FromValue(typeIsExpression.TypeOperand)]));
	}

	private static ODataExpression ParseTypeAsExpression(UnaryExpression typeAsExpression)
	{
		var target = ParseLinqExpression(typeAsExpression.Operand);
		return FromFunction(new ExpressionFunction(ODataLiteral.Cast, [target, FromValue(typeAsExpression.Type)]));
	}

	private static object? EvaluateStaticMember(MemberExpression expression)
	{
		return expression.Member switch
		{
			FieldInfo field => field.GetValue(null),
			PropertyInfo { GetMethod: not null } property when property.GetIndexParameters().Length == 0 => property.GetValue(null),
			PropertyInfo => throw new ArgumentException("Cannot evaluate closure references to indexed properties."),
			_ => null,
		};
	}

	private static object? EvaluateConstValue(object? value, Stack<MemberInfo> memberChain)
	{
		while (memberChain.Count > 0 && value is not null)
		{
			var member = memberChain.Pop();
			value = member switch
			{
				PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(value),
				PropertyInfo => throw new ArgumentException("Cannot evaluate constant value of indexed properties."),
				FieldInfo field => field.GetValue(value),
				_ => value,
			};
		}

		return value;
	}

	/// <summary>Evaluates an expression that does not reference the lambda parameter and wraps the result as a value.</summary>
	private static ODataExpression? EvaluateIfClosed(Expression expression)
	{
		if (IsParameterBound(expression))
		{
			return null;
		}

		try
		{
			var value = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile().Invoke();
			return new ODataExpression(value);
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or TargetInvocationException)
		{
			return null;
		}
	}

	private static bool IsParameterBound(Expression expression) => ParameterFinder.Contains(expression);

	private sealed class ParameterFinder : ExpressionVisitor
	{
		private readonly HashSet<ParameterExpression> _declared = [];
		private bool _found;

		public static bool Contains(Expression expression)
		{
			var finder = new ParameterFinder();
			finder.Visit(expression);
			return finder._found;
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			// a nested lambda's own parameters do not count, but a reference to the outer parameter does
			_found |= !_declared.Contains(node);
			return node;
		}

		protected override Expression VisitLambda<T>(Expression<T> node)
		{
			_declared.UnionWith(node.Parameters);
			Visit(node.Body);
			_declared.ExceptWith(node.Parameters);
			return node;
		}
	}
}

internal static class MemberInfoMappingExtensions
{
	public static string GetMappedMemberName(this MemberInfo member) => TypeSystem.MemberMapping.GetMappedName(member);
}
