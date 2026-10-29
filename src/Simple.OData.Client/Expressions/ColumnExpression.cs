using System.Linq.Expressions;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Extracts <c>$select</c>/<c>$orderby</c> column paths from member-access lambdas.
/// </summary>
internal static class ColumnExpression
{
	public static IEnumerable<string> ExtractColumnNames<T>(this Expression<Func<T, object>> expression, ITypeCache typeCache)
	{
		ArgumentNullException.ThrowIfNull(expression);
		return ((Expression)expression).ExtractColumnNames(typeCache);
	}

	public static string ExtractColumnName(this Expression expression, ITypeCache typeCache) => expression.ExtractColumnNames(typeCache).Single();

	public static IEnumerable<string> ExtractColumnNames(this Expression expression, ITypeCache typeCache)
	{
		ArgumentNullException.ThrowIfNull(expression);
		ArgumentNullException.ThrowIfNull(typeCache);
		return expression.NodeType switch
		{
			ExpressionType.MemberAccess => ExtractColumnNames((MemberExpression)expression, typeCache),
			ExpressionType.Convert or ExpressionType.Quote => ((UnaryExpression)expression).Operand.ExtractColumnNames(typeCache),
			ExpressionType.Lambda => ((LambdaExpression)expression).Body.ExtractColumnNames(typeCache),
			ExpressionType.Call => ExtractColumnNames((MethodCallExpression)expression, typeCache),
			ExpressionType.New => ((NewExpression)expression).Arguments.SelectMany(x => x.ExtractColumnNames(typeCache)),
			_ => throw Utils.NotSupportedExpression(expression),
		};
	}

	private static IEnumerable<string> ExtractColumnNames(MethodCallExpression callExpression, ITypeCache typeCache)
	{
		if (callExpression.Arguments.Count != 2)
		{
			throw Utils.NotSupportedExpression(callExpression);
		}

		var sourceIsSelect = callExpression.Arguments[0] is MethodCallExpression { Method.Name: "Select" };
		switch (callExpression.Method.Name)
		{
			case "Select":
				return callExpression.Arguments[0].ExtractColumnNames(typeCache)
					.SelectMany(x => callExpression.Arguments[1].ExtractColumnNames(typeCache).Select(y => $"{x}/{y}"));
			case "OrderBy" when !sourceIsSelect:
				return callExpression.Arguments[0].ExtractColumnNames(typeCache)
					.SelectMany(x => callExpression.Arguments[1].ExtractColumnNames(typeCache).OrderBy(y => $"{x}/{y}", StringComparer.Ordinal));
			case "OrderByDescending" when !sourceIsSelect:
				return callExpression.Arguments[0].ExtractColumnNames(typeCache)
					.SelectMany(x => callExpression.Arguments[1].ExtractColumnNames(typeCache).OrderByDescending(y => $"{x}/{y}", StringComparer.Ordinal));
			default:
				throw Utils.NotSupportedExpression(callExpression);
		}
	}

	private static IEnumerable<string> ExtractColumnNames(MemberExpression memberExpression, ITypeCache typeCache)
	{
		if (memberExpression.Expression is null)
		{
			throw Utils.NotSupportedExpression(memberExpression);
		}

		var memberName = typeCache.GetMappedName(memberExpression.Expression.Type, memberExpression.Member.Name);
		return memberExpression.Expression is MemberExpression parent
			? parent.ExtractColumnNames(typeCache).Select(x => $"{x}/{memberName}")
			: [memberName];
	}
}
