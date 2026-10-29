namespace Simple.OData.Client;

/// <summary>Aggregation methods usable inside <see cref="IDataAggregation{T}"/> expressions; never invoked at run time.</summary>
public interface IAggregationFunction<T>
{
	/// <summary><c>property with average</c>.</summary>
	TR Average<TR>(TR value);

	/// <summary><c>property with sum</c>.</summary>
	TR Sum<TR>(TR value);

	/// <summary><c>property with min</c>.</summary>
	TR Min<TR>(TR value);

	/// <summary><c>property with max</c>.</summary>
	TR Max<TR>(TR value);

	/// <summary><c>$count</c>.</summary>
	int Count();

	/// <summary><c>property with countdistinct</c>.</summary>
	int CountDistinct<TR>(TR value);
}
