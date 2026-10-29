using Simple.OData.Client.Aggregation;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Aggregation;

public sealed class TypedDataAggregationTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private static string Apply(Func<IDataAggregation<Product>, object> configure, Action<FluentCommand>? more = null)
	{
		var builder = new DataAggregationBuilder<Product>();
		configure(builder);
		var command = new FluentCommand().For("Products").Apply(builder);
		more?.Invoke(command);
		return command.Resolve(Session).Format();
	}

	[Fact]
	public void Filter() =>
		Apply(b => b.Filter(x => x.ProductName.Contains("ai")).Filter(x => x.ProductName.StartsWith("Ch")))
			.Should().Be("Products?$apply=filter%28contains%28ProductName%2C%27ai%27%29%20and%20startswith%28ProductName%2C%27Ch%27%29%29");

	[Fact]
	public void AggregateFunctions()
	{
		Apply(b => b.Aggregate((x, a) => new { AverageUnitPrice = a.Average(x.UnitPrice) }))
			.Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29");
		Apply(b => b.Aggregate((x, a) => new { Total = a.Sum(x.UnitPrice) }))
			.Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20sum%20as%20Total%29");
		Apply(b => b.Aggregate((x, a) => new { MinPrice = a.Min(x.UnitPrice) }))
			.Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20min%20as%20MinPrice%29");
		Apply(b => b.Aggregate((x, a) => new { MaxPrice = a.Max(x.UnitPrice) }))
			.Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20max%20as%20MaxPrice%29");
		Apply(b => b.Aggregate((x, a) => new { Count = a.CountDistinct(x.ProductName) }))
			.Should().Be("Products?$apply=aggregate%28ProductName%20with%20countdistinct%20as%20Count%29");
		Apply(b => b.Aggregate((x, a) => new { Count = a.Count() }))
			.Should().Be("Products?$apply=aggregate%28%24count%20as%20Count%29");
	}

	[Fact]
	public void AggregateIntoConcreteType() =>
		Apply(b => b.Aggregate((x, a) => new ProductGroupedByCategory { AverageUnitPrice = a.Average(x.UnitPrice) }))
			.Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29");

	[Fact]
	public void GroupBy()
	{
		Apply(b => b.GroupBy((x, _) => x.Category.CategoryName))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%29");
		Apply(b => b.GroupBy((x, _) => new { x.Category.CategoryName, x.ProductName }))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%2CProductName%29%29");
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29");
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice), Count = a.Count() }))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%2C%24count%20as%20Count%29%29");
	}

	[Fact]
	public void GroupByWithNestedAssignments() =>
		Apply(b => b.GroupBy((x, a) => new
		{
			x.ProductName,
			Category = new Category { CategoryName = x.Category.CategoryName },
			AverageUnitPrice = a.Average(x.UnitPrice),
		})).Should().Be("Products?$apply=groupby%28%28ProductName%2CCategory%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29");

	[Fact]
	public void FilterThenGroupByIntoConcreteType()
	{
		var categories = new List<string> { "Beverage", "Food" };
		Apply(b => b
			.Filter(x => categories.Contains(x.Category.CategoryName))
			.GroupBy((x, a) => new ProductGroupedByCategory
			{
				Category = new Category { CategoryName = x.Category.CategoryName },
				AverageUnitPrice = a.Average(x.UnitPrice),
			}))
			.Should().Be("Products?$apply=filter%28%28Category%2FCategoryName%20in%20%28%27Beverage%27%2C%27Food%27%29%29%29%2Fgroupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29");
	}

	[Fact]
	public void ChainedTransformations()
	{
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, MaxUnitPrice = a.Max(x.UnitPrice) }).Filter(x => x.MaxUnitPrice > 100))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20max%20as%20MaxUnitPrice%29%29%2Ffilter%28MaxUnitPrice%20gt%20100%29");
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }).Aggregate((x, a) => new { MaxPrice = a.Max(x.AverageUnitPrice) }))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29%2Faggregate%28AverageUnitPrice%20with%20max%20as%20MaxPrice%29");
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }).GroupBy((x, a) => new { x.AverageUnitPrice, CategoriesCount = a.Count() }))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29%2Fgroupby%28%28AverageUnitPrice%29%2Caggregate%28%24count%20as%20CategoriesCount%29%29");
	}

	[Fact]
	public void ApplyThenOrderBy() =>
		Apply(b => b.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) }), c => c.OrderBy("AverageUnitPrice"))
			.Should().Be("Products?$apply=groupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29&$orderby=AverageUnitPrice");

	[Fact]
	public void MultipleAppliesAreChained()
	{
		var first = new DataAggregationBuilder<Product>();
		first.Filter(x => x.Category.CategoryName.Contains("ab"));
		var second = new DataAggregationBuilder<Product>();
		second.GroupBy((x, a) => new { x.Category.CategoryName, AverageUnitPrice = a.Average(x.UnitPrice) });
		var third = new DataAggregationBuilder<ProductGroupedByCategory>();
		third.Filter(x => x.AverageUnitPrice > 100);
		new FluentCommand().For("Products").Apply(first).Apply(second).Apply(third).Resolve(Session).Format()
			.Should().Be("Products?$apply=filter%28contains%28Category%2FCategoryName%2C%27ab%27%29%29%2Fgroupby%28%28Category%2FCategoryName%29%2Caggregate%28UnitPrice%20with%20average%20as%20AverageUnitPrice%29%29%2Ffilter%28AverageUnitPrice%20gt%20100%29");
	}

	[Fact]
	public void TextAndBuilderAppliesAreChained()
	{
		var builder = new DataAggregationBuilder<Product>();
		builder.Aggregate((x, a) => new { Total = a.Sum(x.UnitPrice) });
		new FluentCommand().For("Products").Apply("filter(UnitPrice gt 10)").Apply(builder).Resolve(Session).Format()
			.Should().Be("Products?$apply=filter%28UnitPrice%20gt%2010%29%2Faggregate%28UnitPrice%20with%20sum%20as%20Total%29");
	}

	[Fact]
	public void Compute() =>
		Apply(b => b.Compute(x => new { Doubled = x.UnitPrice * 2, Upper = x.ProductName.ToUpper() }))
			.Should().Be("Products?$apply=compute%28UnitPrice%20mul%202%20as%20Doubled%2Ctoupper%28ProductName%29%20as%20Upper%29");

	[Fact]
	public void TopAndBottomCount()
	{
		Apply(b => b.TopCount(3, x => x.UnitPrice)).Should().Be("Products?$apply=topcount%283%2CUnitPrice%29");
		Apply(b => b.BottomCount(2, x => x.Category.CategoryName)).Should().Be("Products?$apply=bottomcount%282%2CCategory%2FCategoryName%29");
		Apply(b => b.Filter(x => x.UnitPrice > 1).TopCount(3, x => x.UnitPrice))
			.Should().Be("Products?$apply=filter%28UnitPrice%20gt%201%29%2Ftopcount%283%2CUnitPrice%29");
	}

	[Fact]
	public void UnsupportedAggregateBodyThrows()
	{
		var act = () => Apply(b => b.Aggregate((x, a) => x.ProductName));
		act.Should().Throw<ArgumentException>();
	}

	private sealed class ProductGroupedByCategory : Product
	{
		public decimal AverageUnitPrice { get; set; }
	}
}
