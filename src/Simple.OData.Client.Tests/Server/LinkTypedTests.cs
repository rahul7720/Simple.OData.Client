using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class LinkTypedTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task LinkEntry()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test4" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Test5" }).InsertEntryAsync();
		await client.For<Product>().Key(product!).LinkEntryAsync(x => x.Category, category!);
		product = await client.For<Product>().Filter(x => x.ProductName == "Test5").FindEntryAsync();
		product!.CategoryID.Should().Be(category.CategoryID);
	}

	[Fact]
	public async Task UnlinkEntry()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test4" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Test5", CategoryID = category!.CategoryID }).InsertEntryAsync();
		await client.For<Product>().Key(product!).UnlinkEntryAsync(x => x.Category);
		product = await client.For<Product>().Filter(x => x.ProductName == "Test5").FindEntryAsync();
		product!.CategoryID.Should().BeNull();
	}

	[Fact]
	public async Task LinkByTypeName()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Typed" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Typed" }).InsertEntryAsync();
		await client.For<Product>().Key(product!).LinkEntryAsync(category!);
		(await client.For<Product>().Key(product.ProductID).FindEntryAsync())!.CategoryID.Should().Be(category.CategoryID);
		await client.For<Product>().Key(product).UnlinkEntryAsync<Category>();
		(await client.For<Product>().Key(product.ProductID).FindEntryAsync())!.CategoryID.Should().BeNull();
	}
}
