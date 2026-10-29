using Simple.OData.Client.Commands;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Aggregation;

public sealed class DynamicDataAggregationTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private static void Check(dynamic aggregation, string expected, Action<FluentCommand>? more = null)
	{
		var command = new FluentCommand().For("Products").Apply(((DynamicDataAggregation)aggregation).CreateBuilder());
		more?.Invoke(command);
		string actual = command.Resolve(Session).Format();
		actual.Should().Be(expected);
	}

	[Fact]
	public void FilterText() =>
		Check(ODataDynamicDataAggregation.Builder.Filter("contains(ProductName,'ai')").Filter("startswith(ProductName,'Ch')"),
			"Products?$apply=filter%28contains%28ProductName%2C'ai'%29%20and%20startswith%28ProductName%2C'Ch'%29%29");

	[Fact]
	public void FilterExpressions()
	{
		var x = ODataDynamic.Expression;
		Check(ODataDynamicDataAggregation.Builder.Filter(x.ProductName.Contains("ai")), "Products?$apply=filter%28contains%28ProductName%2C'ai'%29%29");
		Check(ODataDynamicDataAggregation.Builder.Filter(x.ProductName.Contains("ai")).Filter(x.ProductName.StartsWith("Ch")),
			"Products?$apply=filter%28contains%28ProductName%2C'ai'%29%20and%20startswith%28ProductName%2C'Ch'%29%29");
		Check(ODataDynamicDataAggregation.Builder.Filter("UnitPrice gt 1").Filter(x.ProductName.Contains("ai")),
			"Products?$apply=filter%28UnitPrice%20gt%201%20and%20contains%28ProductName%2C'ai'%29%29");
	}

	[Fact]
	public void AggregateFunctions()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { AverageUnitPrice = a.Average(x.UnitPrice) }), "Products?$apply=aggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29");
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { Total = a.Sum(x.UnitPrice) }), "Products?$apply=aggregate%28UnitPrice%20with%20sum%20as%20Total%29");
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { MinPrice = a.Min(x.UnitPrice) }), "Products?$apply=aggregate%28UnitPrice%20with%20min%20as%20MinPrice%29");
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { MaxPrice = a.Max(x.UnitPrice) }), "Products?$apply=aggregate%28UnitPrice%20with%20max%20as%20MaxPrice%29");
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { Count = a.CountDistinct(x.ProductName) }), "Products?$apply=aggregate%28ProductName%20with%20countdistinct%20as%20Count%29");
		Check(ODataDynamicDataAggregation.Builder.Aggregate(new { Count = a.Count() }), "Products?$apply=aggregate%28%24count%20as%20Count%29");
	}

	[Fact]
	public void GroupBy()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		Check(ODataDynamicDataAggregation.Builder.GroupBy(x.Category.CategoryName), "Products?$apply=groupby%28%28Category%2FCategoryName%29%29");
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, x.ProductName }), "Products?$apply=groupby%28%28Category%2FCategoryName%2CProductName%29%29");
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29");
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice), Count = a.Count() }),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%2C%24count%20as%20Count%29%29");
	}

	[Fact]
	public void ChainedTransformations()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, MaxUnitPrice = a.Max(x.UnitPrice) }).Filter(x.MaxUnitPrice > 100),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20max%20as%20MaxUnitPrice%29%29%2Ffilter%28MaxUnitPrice%20gt%20100%29");
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }).Aggregate(new { MaxPrice = a.Max(x.AverageUnitPrice) }),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29%2Faggregate%28AverageUnitPrice%20with%20max%20as%20MaxPrice%29");
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }).GroupBy(new { x.AverageUnitPrice, CategoriesCount = a.Count() }),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29%2Fgroupby%28%28AverageUnitPrice%29%2Caggregate%28%24count%20as%20CategoriesCount%29%29");
	}

	[Fact]
	public void ApplyThenOrderBy()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		Check(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }),
			"Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29&$orderby=AverageUnitPrice",
			c => c.OrderBy("AverageUnitPrice"));
	}

	[Fact]
	public void MultipleApplies()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		var command = new FluentCommand().For("Products")
			.Apply(ODataDynamicDataAggregation.Builder.Filter("contains(Category/CategoryName,'v')").CreateBuilder())
			.Apply(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }).CreateBuilder());
		command.Resolve(Session).Format()
			.Should().Be("Products?$apply=filter%28contains%28Category%2FCategoryName%2C'v'%29%29%2Fgroupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29");
	}

	[Fact]
	public void ComputeAndTopBottomCount()
	{
		var x = ODataDynamic.Expression;
		Check(ODataDynamicDataAggregation.Builder.Compute(new { Doubled = x.UnitPrice * 2 }), "Products?$apply=compute%28UnitPrice%20mul%202%20as%20Doubled%29");
		Check(ODataDynamicDataAggregation.Builder.TopCount(3, x.UnitPrice), "Products?$apply=topcount%283%2CUnitPrice%29");
		Check(ODataDynamicDataAggregation.Builder.BottomCount(2, x.Category.CategoryName), "Products?$apply=bottomcount%282%2CCategory%2FCategoryName%29");
	}
}
