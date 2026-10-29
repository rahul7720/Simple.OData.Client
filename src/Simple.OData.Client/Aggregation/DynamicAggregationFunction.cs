namespace Simple.OData.Client;

/// <summary>Aggregation methods for <see cref="DynamicDataAggregation"/>; each returns a marker consumed by the builder.</summary>
public sealed class DynamicAggregationFunction
{
	internal DynamicAggregationFunction()
	{
	}

	/// <summary><c>property with average</c>.</summary>
	public (string Function, ODataExpression? Expression) Average(ODataExpression expression) => (nameof(Average), expression);

	/// <summary><c>property with sum</c>.</summary>
	public (string Function, ODataExpression? Expression) Sum(ODataExpression expression) => (nameof(Sum), expression);

	/// <summary><c>property with min</c>.</summary>
	public (string Function, ODataExpression? Expression) Min(ODataExpression expression) => (nameof(Min), expression);

	/// <summary><c>property with max</c>.</summary>
	public (string Function, ODataExpression? Expression) Max(ODataExpression expression) => (nameof(Max), expression);

	/// <summary><c>$count</c>.</summary>
	public (string Function, ODataExpression? Expression) Count() => (nameof(Count), null);

	/// <summary><c>property with countdistinct</c>.</summary>
	public (string Function, ODataExpression? Expression) CountDistinct(ODataExpression expression) => (nameof(CountDistinct), expression);
}
