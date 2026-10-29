using System.Linq.Expressions;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Extracts <c>$compute</c> clauses (<c>expression as Alias</c>) from projection lambdas such as
/// <c>x => new { Total = x.Price * x.Quantity }</c>.
/// </summary>
internal static class ComputeExpression
{
	public static IReadOnlyList<(string Alias, ODataExpression Expression)> Extract<T, TR>(Expression<Func<T, TR>> expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		var result = new List<(string, ODataExpression)>();
		switch (expression.Body)
		{
			case NewExpression { Members: not null } newExpression:
				for (var i = 0; i < newExpression.Members.Count && i < newExpression.Arguments.Count; i++)
				{
					result.Add((newExpression.Members[i].Name, ODataExpression.FromLinqExpression(newExpression.Arguments[i])));
				}

				break;

			case MemberInitExpression memberInit:
				foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
				{
					result.Add((binding.Member.Name, ODataExpression.FromLinqExpression(binding.Expression)));
				}

				break;

			default:
				throw new NotSupportedException("A $compute expression must create an anonymous type or initialize an object, e.g. x => new { Total = x.Price * x.Quantity }.");
		}

		return result;
	}
}
