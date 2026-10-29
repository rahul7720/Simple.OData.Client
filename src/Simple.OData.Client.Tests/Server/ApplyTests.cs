using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public class CategoryCount
{
	public int? CategoryID { get; set; }
	public int Count { get; set; }
}

public class PriceTotal
{
	public decimal Total { get; set; }
}

public class PriceStats
{
	public decimal Average { get; set; }
	public decimal Min { get; set; }
	public decimal Max { get; set; }
}

/// <summary><c>$apply</c> aggregation scenarios.</summary>
public sealed class ApplyTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task GroupByWithCountTyped()
	{
		var groups = await CreateClient().For<Product>().Apply(b => b.GroupBy((x, a) => new CategoryCount { CategoryID = x.CategoryID, Count = a.Count() })).FindEntriesAsync();
		groups.Should().HaveCount(Seed.Categories);
		groups.Single(x => x.CategoryID == 1).Count.Should().Be(Seed.BeveragesProducts);
		groups.Single(x => x.CategoryID == 2).Count.Should().Be(Seed.CondimentsProducts);
		groups.Sum(x => x.Count).Should().Be(Seed.Products);
	}

	[Fact]
	public async Task AggregateSum()
	{
		var totals = await CreateClient().For<Product>().Apply(b => b.Aggregate((x, a) => new PriceTotal { Total = a.Sum(x.UnitPrice) })).FindEntriesAsync();
		totals.Single().Total.Should().Be(644.70m);
	}

	[Fact]
	public async Task AggregateAverageMinMax()
	{
		var stats = await CreateClient().For<Product>().Apply(b => b.Aggregate((x, a) => new PriceStats { Average = a.Average(x.UnitPrice), Min = a.Min(x.UnitPrice), Max = a.Max(x.UnitPrice) })).FindEntriesAsync();
		stats.Single().Min.Should().Be(6m);
		stats.Single().Max.Should().Be(97m);
		stats.Single().Average.Should().BeApproximately(644.70m / Seed.Products, 0.01m);
	}

	[Fact]
	public async Task FilterThenGroupBy()
	{
		var groups = await CreateClient().For<Product>().Apply(b => b.Filter(x => x.UnitPrice > 20).GroupBy((x, a) => new CategoryCount { CategoryID = x.CategoryID, Count = a.Count() })).FindEntriesAsync();
		groups.Should().HaveCount(7);
		groups.Single(x => x.CategoryID == 2).Count.Should().Be(4);
		groups.Should().NotContain(x => x.CategoryID == 1);
	}

	[Fact]
	public async Task DynamicBuilder()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		IEnumerable<dynamic> groups = await CreateClient().For(x.Products).Apply(ODataDynamicDataAggregation.Builder.GroupBy(new { x.CategoryID, Count = a.Count() })).FindEntriesAsync();
		var list = groups.ToList();
		list.Should().HaveCount(Seed.Categories);
		list.Count(g => (int)g.CategoryID == 1 && (int)g.Count == Seed.BeveragesProducts).Should().Be(1);
	}

	[Fact]
	public async Task StringApply()
	{
		var groups = await CreateClient().For("Products").Apply("groupby((CategoryID),aggregate($count as Count))").FindEntriesAsync();
		groups.Should().HaveCount(Seed.Categories);
		groups.Single(x => Equals(x["CategoryID"], 1))["Count"].Should().Be(2);
		Request("GET", "/Products").Query.Should().Contain("$apply=");
	}

	[Fact]
	public async Task ApplyWithOrderByAndTop()
	{
		var groups = await CreateClient().For("Products").Apply("groupby((CategoryID),aggregate(UnitPrice with sum as Total))").OrderByDescending("Total").Top(1).FindEntriesAsync();
		groups.Single()["CategoryID"].Should().Be(2, "condiments carry the largest total");
	}
}
