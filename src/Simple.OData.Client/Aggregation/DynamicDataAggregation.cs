using Simple.OData.Client.Aggregation;
using Simple.OData.Client.TypeSystem;

namespace Simple.OData.Client;

/// <summary>Builds an OData <c>$apply</c> pipeline from dynamic expressions and anonymous objects.</summary>
public sealed class DynamicDataAggregation
{
	private readonly Builder _builder = new();

	internal DynamicDataAggregation()
	{
	}

	/// <summary>Adds a <c>filter(...)</c> transformation from filter text; successive filters are combined with <c>and</c>.</summary>
	public DynamicDataAggregation Filter(string filter)
	{
		ArgumentException.ThrowIfNullOrEmpty(filter);
		_builder.Filter(filter);
		return this;
	}

	/// <summary>Adds a <c>filter(...)</c> transformation; successive filters are combined with <c>and</c>.</summary>
	public DynamicDataAggregation Filter(ODataExpression filter)
	{
		ArgumentNullException.ThrowIfNull(filter);
		_builder.Filter(filter);
		return this;
	}

	/// <summary>
	/// Adds an <c>aggregate(...)</c> transformation from an anonymous object whose properties are
	/// <see cref="DynamicAggregationFunction"/> results, e.g. <c>new { Total = a.Sum(x.Price) }</c>.
	/// </summary>
	public DynamicDataAggregation Aggregate(object aggregation)
	{
		ArgumentNullException.ThrowIfNull(aggregation);
		_builder.Add(ExtractAggregation(aggregation));
		return this;
	}

	/// <summary>
	/// Adds a <c>groupby(...)</c> transformation from a property expression or an anonymous object mixing
	/// property expressions and <see cref="DynamicAggregationFunction"/> results.
	/// </summary>
	public DynamicDataAggregation GroupBy(object groupBy)
	{
		ArgumentNullException.ThrowIfNull(groupBy);
		List<ApplyColumn> columns = [];
		AggregationClauseCollection? aggregation = null;
		if (groupBy is ODataExpression expression)
		{
			columns.Add(new ApplyColumn(ReferenceOf(expression)));
		}
		else
		{
			aggregation = new AggregationClauseCollection();
			foreach (var (name, value) in PropertyValues(groupBy))
			{
				switch (value)
				{
					case ODataExpression column:
						columns.Add(new ApplyColumn(ReferenceOf(column)));
						break;
					case ValueTuple<string, ODataExpression?> function:
						aggregation.Clauses.Add(ToAggregationClause(name, function));
						break;
				}
			}
		}

		_builder.Add(new GroupByClause(columns, aggregation));
		return this;
	}

	/// <summary>Adds a <c>compute(...)</c> transformation from an anonymous object of expressions, e.g. <c>new { Doubled = x.Price * 2 }</c>.</summary>
	public DynamicDataAggregation Compute(object compute)
	{
		ArgumentNullException.ThrowIfNull(compute);
		var computed = PropertyValues(compute)
			.Where(x => x.Value is ODataExpression)
			.Select(x => (x.Name, (ODataExpression)x.Value!))
			.ToList();
		_builder.Add(new ComputeClause(computed));
		return this;
	}

	/// <summary>Adds a <c>topcount(count,property)</c> transformation.</summary>
	public DynamicDataAggregation TopCount(int count, ODataExpression property) => AddTopBottom(top: true, count, property);

	/// <summary>Adds a <c>bottomcount(count,property)</c> transformation.</summary>
	public DynamicDataAggregation BottomCount(int count, ODataExpression property) => AddTopBottom(top: false, count, property);

	internal DataAggregationBuilder CreateBuilder() => _builder;

	private DynamicDataAggregation AddTopBottom(bool top, int count, ODataExpression property)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
		ArgumentNullException.ThrowIfNull(property);
		_builder.Add(new TopBottomCountClause(top, count, new ApplyColumn(ReferenceOf(property))));
		return this;
	}

	private static AggregationClauseCollection ExtractAggregation(object aggregation)
	{
		var collection = new AggregationClauseCollection();
		foreach (var (name, value) in PropertyValues(aggregation))
		{
			if (value is ValueTuple<string, ODataExpression?> function)
			{
				collection.Clauses.Add(ToAggregationClause(name, function));
			}
		}

		return collection;
	}

	private static AggregationClause ToAggregationClause(string alias, (string Function, ODataExpression? Expression) function) =>
		new(alias, function.Function, function.Expression is null ? null : new ApplyColumn(ReferenceOf(function.Expression)));

	private static IEnumerable<(string Name, object? Value)> PropertyValues(object source) =>
		source.GetType().GetDeclaredInstanceProperties().Select(x => (x.Name, x.GetValue(source)));

	private static string ReferenceOf(ODataExpression expression) =>
		expression.Reference ?? throw new ArgumentException("The expression must be a property reference.", nameof(expression));

	private sealed class Builder : DataAggregationBuilder
	{
		public void Add(IDataAggregationClause clause) => Clauses.Add(clause);

		public void Filter(string filter) => AddFilter(filter);

		public void Filter(ODataExpression filter) => AddFilter(filter);
	}
}
