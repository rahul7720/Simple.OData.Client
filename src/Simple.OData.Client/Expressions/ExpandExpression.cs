using System.Linq.Expressions;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Extracts <c>$expand</c> associations (with nested filter, order, top and skip) from navigation lambdas.
/// </summary>
internal static class ExpandExpression
{
	public static IEnumerable<ODataExpandAssociation> ExtractExpandAssociations<T>(this Expression<Func<T, object>> expression, ITypeCache typeCache)
	{
		ArgumentNullException.ThrowIfNull(expression);
		ArgumentNullException.ThrowIfNull(typeCache);
		var visitor = new ExpandExpressionVisitor(typeCache);
		visitor.Visit(expression);
		return visitor.ExpandAssociations.Count > 0 ? visitor.ExpandAssociations : throw Utils.NotSupportedExpression(expression);
	}

	private sealed class ExpandExpressionVisitor(ITypeCache typeCache) : ExpressionVisitor
	{
		public List<ODataExpandAssociation> ExpandAssociations { get; } = [];

		protected override Expression VisitMember(MemberExpression node)
		{
			if (node.Expression is null || Utils.IsSystemType(node.Expression.Type))
			{
				// only members of entity or complex types can be expanded
				throw Utils.NotSupportedExpression(node);
			}

			var memberName = typeCache.GetMappedName(node.Expression.Type, node.Member.Name);
			var association = new ODataExpandAssociation(memberName);
			var collection = node.Expression is MemberExpression
				? AddNestedAndGetDeepest(node.Expression).ExpandAssociations
				: ExpandAssociations;
			collection.Add(association);
			return node;
		}

		protected override Expression VisitUnary(UnaryExpression node)
		{
			if (node.NodeType is not (ExpressionType.Convert or ExpressionType.Quote))
			{
				return base.VisitUnary(node);
			}

			ExpandAssociations.AddRange(ExtractNested(node.Operand));
			return node;
		}

		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (node.Arguments.Count != 2)
			{
				throw Utils.NotSupportedExpression(node);
			}

			var association = AddNestedAndGetDeepest(node.Arguments[0]);
			var sourceIsSelect = node.Arguments[0] is MethodCallExpression { Method.Name: "Select" };
			switch (node.Method.Name)
			{
				case "Select":
					association.ExpandAssociations.AddRange(ExtractNested(node.Arguments[1]));
					return node;

				case "OrderBy" or "ThenBy" when !sourceIsSelect:
					association.OrderByColumns.AddRange(node.Arguments[1].ExtractColumnNames(typeCache).Select(c => new ODataOrderByColumn(c, false)));
					return node;

				case "OrderByDescending" or "ThenByDescending" when !sourceIsSelect:
					association.OrderByColumns.AddRange(node.Arguments[1].ExtractColumnNames(typeCache).Select(c => new ODataOrderByColumn(c, true)));
					return node;

				case "Where":
					var filter = ODataExpression.FromLinqExpression(((LambdaExpression)StripQuotes(node.Arguments[1])).Body);
					association.FilterExpression = association.FilterExpression is null ? filter : association.FilterExpression && filter;
					return node;

				case "Take":
					association.Top = EvaluateInt(node.Arguments[1]);
					return node;

				case "Skip":
					association.Skip = EvaluateInt(node.Arguments[1]);
					return node;

				default:
					throw Utils.NotSupportedExpression(node);
			}
		}

		protected override Expression VisitNew(NewExpression node)
		{
			ExpandAssociations.AddRange(node.Arguments.SelectMany(ExtractNested));
			return node;
		}

		private ODataExpandAssociation AddNestedAndGetDeepest(Expression nestedExpression)
		{
			var nested = ExtractNested(nestedExpression).First();
			var existing = ExpandAssociations.FirstOrDefault(x => x.Name == nested.Name && ReferenceEquals(x, nested));
			if (existing is null)
			{
				ExpandAssociations.Add(nested);
			}

			var deepest = nested;
			while (deepest.ExpandAssociations.Count > 0)
			{
				deepest = deepest.ExpandAssociations[0];
			}

			return deepest;
		}

		private List<ODataExpandAssociation> ExtractNested(Expression expression)
		{
			var visitor = new ExpandExpressionVisitor(typeCache);
			visitor.Visit(expression);
			return visitor.ExpandAssociations;
		}

		private static Expression StripQuotes(Expression expression) =>
			expression is UnaryExpression { NodeType: ExpressionType.Quote } quote ? StripQuotes(quote.Operand) : expression;

		private static int EvaluateInt(Expression expression) =>
			expression is ConstantExpression { Value: int constant }
				? constant
				: Convert.ToInt32(Expression.Lambda(expression).Compile().DynamicInvoke(), System.Globalization.CultureInfo.InvariantCulture);
	}
}
