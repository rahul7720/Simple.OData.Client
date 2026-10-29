using System.Linq.Expressions;

namespace Simple.OData.Client;

/// <summary>Builds an OData <c>$apply</c> pipeline over entities of type <typeparamref name="T"/>.</summary>
public interface IDataAggregation<T>
	where T : class
{
	/// <summary>Adds a <c>filter(...)</c> transformation; successive filters are combined with <c>and</c>.</summary>
	IDataAggregation<T> Filter(Expression<Func<T, bool>> filter);

	/// <summary>
	/// Adds an <c>aggregate(...)</c> transformation. The body must be an object initializer such as
	/// <c>(x, a) => new { Total = a.Sum(x.Count) }</c>.
	/// </summary>
	IDataAggregation<TR> Aggregate<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> aggregation)
		where TR : class;

	/// <summary>
	/// Adds a <c>groupby(...)</c> transformation. The body is a property or an object initializer mixing grouping
	/// properties and aggregations, such as <c>(x, a) => new { x.CategoryName, Average = a.Average(x.Price) }</c>.
	/// </summary>
	IDataAggregation<TR> GroupBy<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> groupBy)
		where TR : class;

	/// <summary>Adds a <c>compute(...)</c> transformation from an object initializer of computed properties.</summary>
	IDataAggregation<TR> Compute<TR>(Expression<Func<T, TR>> compute)
		where TR : class;

	/// <summary>Adds a <c>topcount(count,property)</c> transformation.</summary>
	IDataAggregation<T> TopCount(int count, Expression<Func<T, object>> propertyPath);

	/// <summary>Adds a <c>bottomcount(count,property)</c> transformation.</summary>
	IDataAggregation<T> BottomCount(int count, Expression<Func<T, object>> propertyPath);
}
