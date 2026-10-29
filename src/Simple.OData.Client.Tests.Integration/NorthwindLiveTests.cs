using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests;

internal class NorthwindProduct
{
	public int ProductID { get; set; }
	public string ProductName { get; set; }
	public decimal? UnitPrice { get; set; }
	public int? CategoryID { get; set; }
	public NorthwindCategory Category { get; set; }
}

internal class NorthwindCategory
{
	public int CategoryID { get; set; }
	public string CategoryName { get; set; }
	public IList<NorthwindProduct> Products { get; set; }
}

/// <summary>Read-only scenarios against the public V4 Northwind service.</summary>
public sealed class NorthwindLiveTests : LiveTestBase
{
	private const int Products = 77;

	private static Task<ODataClient> ClientAsync() => CreateClientAsync(LiveServices.NorthwindReadOnly);

	[LiveFact]
	public async Task MetadataListsTheEntitySets()
	{
		var model = await (await ClientAsync()).GetMetadataAsync();
		model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
		model.EntityContainer.FindEntitySet("Categories").Should().NotBeNull();
	}

	[LiveFact]
	public async Task FindEntriesWithSelect()
	{
		var products = await (await ClientAsync()).For("Products").Select("ProductID", "ProductName").Top(5).FindEntriesAsync();
		products.Should().HaveCount(5);
		products.Should().AllSatisfy(x => x.Keys.Should().Contain(["ProductID", "ProductName"]));
	}

	[LiveFact]
	public async Task FilterByName()
	{
		var products = await (await ClientAsync()).For("Products").Filter("ProductName eq 'Chai'").FindEntriesAsync();
		products.Single()["ProductID"].Should().Be(1);
	}

	[LiveFact]
	public async Task FilterTyped()
	{
		var product = await (await ClientAsync()).For<NorthwindProduct>("Products").Filter(x => x.ProductName == "Chai").FindEntryAsync();
		product!.ProductID.Should().Be(1);
	}

	[LiveFact]
	public async Task FilterDynamic()
	{
		var x = ODataDynamic.Expression;
		dynamic product = await (await ClientAsync()).For(x.Products).Filter(x.ProductName == "Chai").FindEntryAsync();
		((int)product.ProductID).Should().Be(1);
	}

	[LiveFact]
	public async Task GetByKey()
	{
		var category = await (await ClientAsync()).For("Categories").Key(1).FindEntryAsync();
		category!["CategoryName"].Should().Be("Beverages");
	}

	[LiveFact]
	public async Task ExpandSingleAndCollection()
	{
		var client = await ClientAsync();
		var product = await client.For("Products").Key(1).Expand("Category").FindEntryAsync();
		((Entry)product!["Category"]!)["CategoryName"].Should().Be("Beverages");

		var category = await client.For<NorthwindCategory>("Categories").Key(1).Expand(x => x.Products).FindEntryAsync();
		category!.Products.Should().HaveCount(12);
	}

	[LiveFact]
	public async Task OrderSkipAndTop()
	{
		var client = await ClientAsync();
		(await client.For("Products").OrderBy("ProductName").Top(1).FindEntriesAsync()).Single()["ProductName"].Should().Be("Alice Mutton");
		(await client.For("Products").OrderBy("ProductID").Skip(1).Top(1).FindEntriesAsync()).Single()["ProductID"].Should().Be(2);
	}

	[LiveFact]
	public async Task CountAndFeedAnnotations()
	{
		var client = await ClientAsync();
		(await client.For("Products").Count().FindScalarAsync<int>()).Should().Be(Products);
		var annotations = new ODataFeedAnnotations();
		await client.For("Products").WithCount().Top(1).FindEntriesAsync(annotations);
		annotations.Count.Should().Be(Products);
	}

	[LiveFact]
	public async Task AsyncEnumerableFollowsServerPaging()
	{
		var count = 0;
		await foreach (var _ in (await ClientAsync()).For("Orders").FindEntriesAsAsyncEnumerable())
		{
			count++;
		}

		count.Should().BeGreaterThan(20, "Northwind pages orders server-side and the client follows every next link");
	}
}
