using System.Linq.Expressions;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

/// <summary>
/// An OData filter/query expression tree. Built from LINQ expressions, from the dynamic API or with operators.
/// </summary>
public partial class ODataExpression
{
	private static readonly char[] PropertySeparators = ['.', '/'];

	private readonly ODataExpression? _functionCaller;
	private readonly ODataExpression? _left;
	private readonly ODataExpression? _right;
	private readonly ExpressionType _operator = ExpressionType.Default;
	private readonly Type? _conversionType;

	internal ODataExpression()
	{
	}

	/// <summary>Builds an expression from a LINQ predicate.</summary>
	public ODataExpression(Expression expression)
		: this(FromLinqExpression(expression))
	{
	}

	/// <summary>Copies another expression.</summary>
	protected internal ODataExpression(ODataExpression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		_functionCaller = expression._functionCaller;
		_left = expression._left;
		_right = expression._right;
		_operator = expression._operator;
		_conversionType = expression._conversionType;
		Reference = expression.Reference;
		Value = expression.Value;
		Function = expression.Function;
	}

	/// <summary>A constant value.</summary>
	protected internal ODataExpression(object? value)
	{
		Value = value;
	}

	/// <summary>A property reference.</summary>
	protected internal ODataExpression(string reference)
	{
		Reference = reference;
	}

	/// <summary>A property reference with a value (used by <c>Set</c>).</summary>
	protected internal ODataExpression(string reference, object? value)
	{
		Reference = reference;
		Value = value;
	}

	/// <summary>A function call without a target.</summary>
	protected internal ODataExpression(ExpressionFunction function)
	{
		Function = function;
	}

	/// <summary>A binary or unary operation.</summary>
	protected internal ODataExpression(ODataExpression? left, ODataExpression? right, ExpressionType expressionOperator)
	{
		_left = left;
		_right = right;
		_operator = expressionOperator;
	}

	/// <summary>A reference on a target expression (e.g. a zero-argument function or a cast member).</summary>
	protected internal ODataExpression(ODataExpression caller, string reference)
	{
		_functionCaller = caller;
		Reference = reference;
	}

	/// <summary>A function call on a target expression.</summary>
	protected internal ODataExpression(ODataExpression caller, ExpressionFunction function)
	{
		_functionCaller = caller;
		Function = function;
	}

	/// <summary>A value conversion of another expression.</summary>
	protected internal ODataExpression(ODataExpression expression, Type conversionType)
	{
		_conversionType = conversionType;
		Value = expression;
	}

	/// <summary>The referenced property path, when this is a reference.</summary>
	public string? Reference { get; private set; }

	/// <summary>The constant value, when this is a value.</summary>
	public object? Value { get; private set; }

	/// <summary>The function call, when this is a function.</summary>
	public ExpressionFunction? Function { get; private set; }

	/// <summary>Whether this expression converts another expression's value.</summary>
	public bool IsValueConversion => _conversionType is not null;

	/// <summary>Whether the expression carries nothing (the lambda parameter itself).</summary>
	public bool IsNull => Value is null && Reference is null && Function is null && _operator == ExpressionType.Default;

	internal ODataExpression? FunctionCaller => _functionCaller;

	internal static ODataExpression FromReference(string reference) => new(reference, (object?)null);

	internal static ODataExpression FromValue(object? value) => new(value);

	internal static ODataExpression FromFunction(ExpressionFunction function) => new(function);

	internal static ODataExpression FromFunction(string functionName, ODataExpression? targetExpression, IEnumerable<object?> arguments) =>
		targetExpression is null
			? new ODataExpression(new ExpressionFunction(functionName, arguments))
			: new ODataExpression(targetExpression, new ExpressionFunction(functionName, arguments));

	internal static ODataExpression FromFunction(string functionName, ODataExpression? targetExpression, IEnumerable<Expression> arguments) =>
		targetExpression is null
			? new ODataExpression(new ExpressionFunction(functionName, arguments))
			: new ODataExpression(targetExpression, new ExpressionFunction(functionName, arguments));

	/// <summary>Translates a LINQ expression (a lambda or its body) into an <see cref="ODataExpression"/>.</summary>
	public static ODataExpression FromLinqExpression(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		return ParseLinqExpression(expression) ?? new ODataExpression();
	}

	/// <summary>Formats the expression as OData filter text using the session's metadata.</summary>
	public string AsString(ISession session) => Format(new ExpressionContext(session));

	/// <summary>Formats the expression as OData filter text evaluated against <paramref name="entityCollection"/>.</summary>
	public string AsString(ISession session, EntityCollection? entityCollection) => Format(new ExpressionContext(session, entityCollection));

	/// <summary>
	/// When the expression is a conjunction of <c>Property eq value</c> comparisons, collects them into
	/// <paramref name="lookupColumns"/> and returns <see langword="true"/>.
	/// </summary>
	internal bool ExtractLookupColumns(IDictionary<string, object?> lookupColumns)
	{
		switch (_operator)
		{
			case ExpressionType.And:
				return _left!.ExtractLookupColumns(lookupColumns) && _right!.ExtractLookupColumns(lookupColumns);

			case ExpressionType.Equal:
				var expression = IsValueConversion ? this : _left;
				while (expression is { IsValueConversion: true })
				{
					expression = expression.Value as ODataExpression;
				}

				if (expression is { Reference: { Length: > 0 } reference })
				{
					if (reference.IndexOfAny(PropertySeparators) >= 0)
					{
						// a nested property is not a key
						return false;
					}

					lookupColumns.TryAdd(reference, _right);
				}

				return true;

			default:
				return IsValueConversion && (Value as ODataExpression)?.ExtractLookupColumns(lookupColumns) == true;
		}
	}

	/// <summary>Whether the expression contains an <c>isof</c> constraint on <paramref name="typeName"/>.</summary>
	internal bool HasTypeConstraint(string? typeName)
	{
		if (typeName is null)
		{
			return false;
		}

		if (_operator == ExpressionType.And)
		{
			return _left!.HasTypeConstraint(typeName) || _right!.HasTypeConstraint(typeName);
		}

		if (Function is { FunctionName: ODataLiteral.IsOf } function)
		{
			return function.Arguments[^1]?.HasTypeConstraint(typeName) == true;
		}

		return Value is Type valueType && valueType.Name == typeName;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is ODataExpression other
		&& Equals(_functionCaller, other._functionCaller)
		&& Equals(_left, other._left)
		&& Equals(_right, other._right)
		&& _operator == other._operator
		&& _conversionType == other._conversionType
		&& Reference == other.Reference
		&& Equals(Value, other.Value)
		&& Equals(Function, other.Function);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(_functionCaller, _left, _right, _operator, _conversionType, Reference, Value, Function);
}

/// <summary>
/// A typed OData expression built from a predicate over <typeparamref name="T"/>.
/// </summary>
public sealed partial class ODataExpression<T> : ODataExpression
{
	/// <summary>Builds an expression from a predicate.</summary>
	public ODataExpression(Expression<Func<T, bool>> predicate)
		: base(predicate)
	{
	}

	internal ODataExpression(ODataExpression expression)
		: base(expression)
	{
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is ODataExpression<T> && base.Equals(obj);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(typeof(T), base.GetHashCode());
}
