using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class LinkTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task LinkEntry()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test4" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Test5" }).InsertEntryAsync();
		await client.For("Products").Key(product!).LinkEntryAsync("Category", category!);
		product = await client.For("Products").Filter("ProductName eq 'Test5'").FindEntryAsync();
		product!["CategoryID"].Should().Be(category["CategoryID"]);
	}

	[Fact]
	public async Task UnlinkEntry()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test4" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Test5", CategoryID = category!["CategoryID"] }).InsertEntryAsync();
		await client.For("Products").Key(product!).UnlinkEntryAsync("Category");
		product = await client.For("Products").Filter("ProductName eq 'Test5'").FindEntryAsync();
		product!["CategoryID"].Should().BeNull();
	}

	[Fact]
	public async Task LinkToCollectionAndUnlinkMember()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Collection" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Member" }).InsertEntryAsync();
		await client.For("Categories").Key(category!).LinkEntryAsync("Products", product!);
		(await client.For("Products").Key(product["ProductID"]).FindEntryAsync())!["CategoryID"].Should().Be(category["CategoryID"]);
		await client.For("Categories").Key(category).UnlinkEntryAsync("Products", product);
		(await client.For("Products").Key(product["ProductID"]).FindEntryAsync())!["CategoryID"].Should().BeNull();
	}
}
