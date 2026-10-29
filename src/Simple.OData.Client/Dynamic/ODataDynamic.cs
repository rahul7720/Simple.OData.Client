namespace Simple.OData.Client;

/// <summary>Entry points for building OData expressions with C# <see langword="dynamic"/>.</summary>
public static class ODataDynamic
{
	/// <summary>A fresh dynamic expression root; member access, operators and method calls build the expression tree.</summary>
	public static dynamic Expression => new DynamicODataExpression();

	/// <summary>Creates a property reference expression.</summary>
	public static ODataExpression ExpressionFromReference(string reference)
	{
		ArgumentException.ThrowIfNullOrEmpty(reference);
		return ODataExpression.FromReference(reference);
	}

	/// <summary>Creates a literal value expression.</summary>
	public static ODataExpression ExpressionFromValue(object? value) => ODataExpression.FromValue(value);

	/// <summary>Creates a function call on the property named <paramref name="targetName"/>.</summary>
	public static ODataExpression ExpressionFromFunction(string functionName, string targetName, IEnumerable<object?> arguments)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		ArgumentException.ThrowIfNullOrEmpty(targetName);
		ArgumentNullException.ThrowIfNull(arguments);
		return ODataExpression.FromFunction(functionName, ODataExpression.FromReference(targetName), arguments);
	}
}
