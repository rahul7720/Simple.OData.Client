using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Reading with <c>odata.metadata=none</c>: payloads without context URLs still materialize through every API.</summary>
public sealed class MetadataLevelTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private ODataClient CreateNoneClient() => CreateClient(s => s.MetadataLevel = ODataMetadataLevel.None);

	[Fact]
	public async Task FluentReadsWithoutContextUrl()
	{
		var client = CreateNoneClient();
		(await client.For("Products").Key(1).FindEntryAsync())!["ProductName"].Should().Be("Chai");
		(await client.For<Product>().Filter(x => x.CategoryID == 1).FindEntriesAsync()).Should().HaveCount(Seed.BeveragesProducts);
		Request("GET", "/Products(1)").Headers["Accept"].Should().Contain("odata.metadata=none");
	}

	[Fact]
	public async Task CommandTextReadsWithoutContextUrl()
	{
		var client = CreateNoneClient();
		(await client.FindEntriesAsync("Products?$filter=CategoryID eq 1")).Should().HaveCount(Seed.BeveragesProducts);
		(await client.FindEntryAsync("Products(1)"))!["ProductName"].Should().Be("Chai");
		(await client.FindEntriesAsync("Products(1)/Category")).Should().ContainSingle().Which["CategoryName"].Should().Be("Beverages");
	}

	[Fact]
	public async Task CommandTextNavigationReadsWithMinimalMetadata()
	{
		var client = CreateClient();
		(await client.FindEntriesAsync("Products(1)/Category")).Should().ContainSingle().Which["CategoryName"].Should().Be("Beverages");
		(await client.FindEntryAsync("Products(1)/Category"))!["CategoryName"].Should().Be("Beverages");
		(await client.FindEntriesAsync("Categories(1)/Products")).Should().HaveCount(Seed.BeveragesProducts);
		(await client.FindEntriesAsync("Products?$filter=CategoryID eq 1")).Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task DynamicReadsWithoutContextUrl()
	{
		var x = ODataDynamic.Expression;
		dynamic product = await CreateNoneClient().For(x.Products).Key(2).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chang");
	}
}
