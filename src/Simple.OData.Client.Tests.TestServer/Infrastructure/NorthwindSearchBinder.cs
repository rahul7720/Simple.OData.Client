using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.OData.UriParser;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>Binds <c>$search</c> to a case-insensitive "contains" on the name property of the entity (ProductName, CompanyName or Name).</summary>
public sealed class NorthwindSearchBinder : ISearchBinder
{
	private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)])!;

	public Expression BindSearch(SearchClause searchClause, QueryBinderContext context)
	{
		ArgumentNullException.ThrowIfNull(searchClause);
		ArgumentNullException.ThrowIfNull(context);
		var parameter = context.CurrentParameter;
		return Expression.Lambda(Bind(searchClause.Expression, parameter), parameter);
	}

	private static Expression Bind(SingleValueNode node, ParameterExpression parameter) => node switch
	{
		SearchTermNode term => Matches(parameter, term.Text),
		BinaryOperatorNode { OperatorKind: BinaryOperatorKind.And } binary => Expression.AndAlso(Bind(binary.Left, parameter), Bind(binary.Right, parameter)),
		BinaryOperatorNode { OperatorKind: BinaryOperatorKind.Or } binary => Expression.OrElse(Bind(binary.Left, parameter), Bind(binary.Right, parameter)),
		UnaryOperatorNode { OperatorKind: UnaryOperatorKind.Not } unary => Expression.Not(Bind(unary.Operand, parameter)),
		_ => throw new NotSupportedException($"Unsupported $search node {node.Kind}."),
	};

	private static Expression Matches(ParameterExpression parameter, string text)
	{
		var property = parameter.Type.GetProperty("ProductName") ?? parameter.Type.GetProperty("CompanyName") ?? parameter.Type.GetProperty("Name");
		if (property is null || property.PropertyType != typeof(string))
		{
			return Expression.Constant(false);
		}

		var value = Expression.Property(parameter, property);
		var notNull = Expression.NotEqual(value, Expression.Constant(null, typeof(string)));
		var contains = Expression.Call(value, ContainsMethod, Expression.Constant(text), Expression.Constant(StringComparison.OrdinalIgnoreCase));
		return Expression.AndAlso(notNull, contains);
	}
}
