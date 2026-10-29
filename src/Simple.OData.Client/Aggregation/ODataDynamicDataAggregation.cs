namespace Simple.OData.Client;

/// <summary>Entry points for building <c>$apply</c> pipelines with dynamic expressions.</summary>
public static class ODataDynamicDataAggregation
{
	/// <summary>A fresh aggregation builder.</summary>
	public static DynamicDataAggregation Builder => new();

	/// <summary>The aggregation functions usable inside <see cref="DynamicDataAggregation.Aggregate"/> and <see cref="DynamicDataAggregation.GroupBy"/>.</summary>
	public static DynamicAggregationFunction AggregationFunction => new();
}
