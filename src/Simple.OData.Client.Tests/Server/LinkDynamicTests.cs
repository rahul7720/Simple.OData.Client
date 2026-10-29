using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class LinkDynamicTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task LinkEntry()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test4").InsertEntryAsync();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test5").InsertEntryAsync();
		await client.For(x.Products).Key(product).LinkEntryAsync(x.Category, category);
		product = await client.For(x.Products).Filter(x.ProductName == "Test5").FindEntryAsync();
		((int)product.CategoryID).Should().Be((int)category.CategoryID);
	}

	[Fact]
	public async Task UnlinkEntry()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test4").InsertEntryAsync();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test5", x.CategoryID = category.CategoryID).InsertEntryAsync();
		await client.For(x.Products).Key(product).UnlinkEntryAsync(x.Category);
		product = await client.For(x.Products).Filter(x.ProductName == "Test5").FindEntryAsync();
		((object?)product.CategoryID).Should().BeNull();
	}
}
