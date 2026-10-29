using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client.Aggregation;

/// <summary>One transformation of an <c>$apply</c> pipeline.</summary>
internal interface IDataAggregationClause
{
	/// <summary>Formats the transformation, or returns an empty string when it contributes nothing.</summary>
	string Format(ExpressionContext context);
}

/// <summary>A property path given either as text or as a LINQ member expression resolved lazily.</summary>
internal readonly struct ApplyColumn
{
	private readonly string? _name;
	private readonly Expression? _expression;

	public ApplyColumn(string name) => _name = name;

	public ApplyColumn(Expression expression) => _expression = expression;

	public string Resolve(ITypeCache typeCache) => _name ?? _expression!.ExtractColumnName(typeCache);
}

internal sealed class FilterClause : IDataAggregationClause
{
	private string? _text;
	private ODataExpression? _expression;

	public FilterClause(string filter) => _text = filter;

	public FilterClause(ODataExpression filter) => _expression = filter;

	public void Append(string filter) => _text = _text is null ? filter : $"{_text} and {filter}";

	public void Append(ODataExpression filter) => _expression = _expression is null ? filter : _expression && filter;

	public string Format(ExpressionContext context)
	{
		var parts = new List<string>(2);
		if (!string.IsNullOrEmpty(_text))
		{
			parts.Add(_text);
		}

		if (_expression is not null)
		{
			parts.Add(_expression.Format(context));
		}

		return parts.Count == 0 ? string.Empty : $"filter({string.Join(" and ", parts)})";
	}
}

internal sealed record AggregationClause(string Alias, string Method, ApplyColumn? Column)
{
	public string Format(ExpressionContext context)
	{
		var column = Column?.Resolve(context.Session.TypeCache);
		var function = Method switch
		{
			nameof(IAggregationFunction<>.Average) => $"{column} with average",
			nameof(IAggregationFunction<>.Sum) => $"{column} with sum",
			nameof(IAggregationFunction<>.Min) => $"{column} with min",
			nameof(IAggregationFunction<>.Max) => $"{column} with max",
			nameof(IAggregationFunction<>.CountDistinct) => $"{column} with countdistinct",
			nameof(IAggregationFunction<>.Count) => ODataLiteral.Count,
			_ => throw new InvalidOperationException($"Unknown aggregation method '{Method}'."),
		};

		return $"{function} {ODataLiteral.As} {Alias}";
	}
}

internal sealed class AggregationClauseCollection : IDataAggregationClause
{
	public List<AggregationClause> Clauses { get; } = [];

	public string Format(ExpressionContext context) =>
		Clauses.Count == 0 ? string.Empty : $"aggregate({string.Join(',', Clauses.Select(x => x.Format(context)))})";
}

internal sealed class GroupByClause(IReadOnlyList<ApplyColumn> columns, AggregationClauseCollection? aggregation) : IDataAggregationClause
{
	public string Format(ExpressionContext context)
	{
		var builder = new StringBuilder("groupby((");
		builder.AppendJoin(',', columns.Select(x => x.Resolve(context.Session.TypeCache))).Append(')');
		var formattedAggregation = aggregation?.Format(context);
		if (!string.IsNullOrEmpty(formattedAggregation))
		{
			builder.Append(',').Append(formattedAggregation);
		}

		return builder.Append(')').ToString();
	}
}

internal sealed class ComputeClause(IReadOnlyList<(string Alias, ODataExpression Expression)> computedProperties) : IDataAggregationClause
{
	public string Format(ExpressionContext context) =>
		computedProperties.Count == 0
			? string.Empty
			: $"compute({string.Join(',', computedProperties.Select(x => $"{x.Expression.Format(context)} {ODataLiteral.As} {x.Alias}"))})";
}

internal sealed class TopBottomCountClause(bool top, int count, ApplyColumn column) : IDataAggregationClause
{
	public string Format(ExpressionContext context) =>
		$"{(top ? "topcount" : "bottomcount")}({count.ToString(CultureInfo.InvariantCulture)},{column.Resolve(context.Session.TypeCache)})";
}
