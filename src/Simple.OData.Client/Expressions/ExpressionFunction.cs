using System.Linq.Expressions;

namespace Simple.OData.Client;

/// <summary>
/// A function call inside an <see cref="ODataExpression"/>.
/// </summary>
public sealed class ExpressionFunction
{
	internal ExpressionFunction(string functionName, List<ODataExpression?> arguments)
	{
		FunctionName = functionName;
		Arguments = arguments;
	}

	/// <summary>Creates a function call with constant arguments.</summary>
	public ExpressionFunction(string functionName, IEnumerable<object?> arguments)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		ArgumentNullException.ThrowIfNull(arguments);
		FunctionName = functionName;
		Arguments = arguments.Select(x => (ODataExpression?)ODataExpression.FromValue(x)).ToList();
	}

	/// <summary>Creates a function call whose arguments are LINQ expressions.</summary>
	public ExpressionFunction(string functionName, IEnumerable<Expression> arguments)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		ArgumentNullException.ThrowIfNull(arguments);
		FunctionName = functionName;
		Arguments = arguments.Select(x => (ODataExpression?)ODataExpression.FromLinqExpression(x)).ToList();
	}

	/// <summary>The CLR or OData function name.</summary>
	public string FunctionName { get; }

	/// <summary>The arguments; an entry is <see langword="null"/> when it refers to the lambda parameter itself.</summary>
	public IReadOnlyList<ODataExpression?> Arguments { get; }

	internal int ArgumentCount => Arguments.Count;
}
