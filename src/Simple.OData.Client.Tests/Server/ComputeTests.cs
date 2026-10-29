using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public class ProductWithDoubledPrice : Product
{
	public decimal Doubled { get; set; }
}

/// <summary><c>$compute</c> scenarios.</summary>
public sealed class ComputeTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task ComputedPropertyAppearsInTheDictionaryResult()
	{
		var product = await CreateClient().For("Products").Key(1).Compute("UnitPrice mul 2 as Doubled").Select("ProductName", "Doubled").FindEntryAsync();
		product!["ProductName"].Should().Be("Chai");
		product["Doubled"].Should().Be(36m);
		Request("GET", "/Products(1)").Query.Should().Contain("$compute=").And.Contain("Doubled");
	}

	[Fact]
	public async Task ComputedPropertyIsTypedThroughAnExpression()
	{
		var product = await CreateClient().For<ProductWithDoubledPrice>("Products").Key(1).Compute(x => new { Doubled = x.UnitPrice * 2 }).Select(x => new { x.ProductName, x.Doubled }).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
		product.Doubled.Should().Be(36m);
	}

	[Fact]
	public async Task ComputedPropertyCanBeFilteredAndOrdered()
	{
		var products = await CreateClient().For("Products").Compute("UnitPrice mul 2 as Doubled").Filter("Doubled gt 150").OrderBy("Doubled").Select("ProductName", "Doubled").FindEntriesAsync();
		products.Select(x => x["ProductName"]).Should().Equal("Sir Rodney's Marmalade", "Mishi Kobe Niku");
		products[1]["Doubled"].Should().Be(194m);
	}

	[Fact]
	public async Task ComputedPropertyDynamic()
	{
		var x = ODataDynamic.Expression;
		dynamic product = await CreateClient().For(x.Products).Key(1).Compute("UnitPrice mul 2 as Doubled").Select(x.ProductName, x.Doubled).FindEntryAsync();
		((decimal)product.Doubled).Should().Be(36m);
	}
}
