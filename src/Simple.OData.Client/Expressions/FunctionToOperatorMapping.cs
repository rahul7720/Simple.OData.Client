using System.Collections;
using System.Text;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Maps method calls that correspond to OData operators rather than functions (currently <c>in</c>).
/// </summary>
internal static class FunctionToOperatorMapping
{
	/// <summary>Formats <c>list.Contains(x.Prop)</c> as <c>(Prop in (…))</c>; returns <see langword="null"/> when the call is not an operator.</summary>
	public static string? TryFormat(ExpressionContext context, ODataExpression? functionCaller, ExpressionFunction function)
	{
		if (!IsInOperator(functionCaller, function))
		{
			return null;
		}

		var list = (IEnumerable)functionCaller!.Value!;
		var builder = new StringBuilder();
		builder.Append('(');
		var first = true;
		foreach (var item in list)
		{
			if (!first)
			{
				builder.Append(',');
			}

			builder.Append(UriLiteralFormatter.Format(context.Session, item, escapeDataString: false));
			first = false;
		}

		builder.Append(')');

		// The `in` operator is always parenthesized to work around https://github.com/OData/odata.net/issues/2016.
		return $"({function.Arguments[0]!.Format(context)} in {builder})";
	}

	private static bool IsInOperator(ODataExpression? functionCaller, ExpressionFunction function) =>
		string.Equals(function.FunctionName, nameof(Enumerable.Contains), StringComparison.Ordinal)
		&& function.ArgumentCount == 1
		&& function.Arguments[0] is not null
		&& functionCaller is { Value: IEnumerable and not string and not byte[] };
}
