using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Key-as-segment addressing (<c>Products/1</c>).</summary>
public sealed class KeyAsSegmentTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private ODataClient CreateSegmentClient() => CreateClient(s => s.KeyAsSegment = true);

	[Fact]
	public async Task ReadsByKeySegment()
	{
		var product = await CreateSegmentClient().For("Products").Key(1).FindEntryAsync();
		product!["ProductName"].Should().Be("Chai");
		Request("GET", "/Products/1").Should().NotBeNull();
	}

	[Fact]
	public async Task ReadsByKeySegmentTyped()
	{
		var product = await CreateSegmentClient().For<Product>().Key(2).FindEntryAsync();
		product!.ProductName.Should().Be("Chang");
		Request("GET", "/Products/2").Should().NotBeNull();
	}

	[Fact]
	public async Task StringKeysAndNavigation()
	{
		var client = CreateSegmentClient();
		var customer = await client.For("Customers").Key("ALFKI").FindEntryAsync();
		customer!["CustomerID"].Should().Be("ALFKI");
		Request("GET", "/Customers/ALFKI").Should().NotBeNull();

		var category = await client.For("Products").Key(1).NavigateTo("Category").FindEntryAsync();
		category!["CategoryName"].Should().Be("Beverages");
		Request("GET", "/Products/1/Category").Should().NotBeNull();
	}

	[Fact]
	public async Task UpdatesAndDeletesByKeySegment()
	{
		var client = CreateSegmentClient();
		var product = await client.For("Products").Set(new { ProductName = "Segment", UnitPrice = 1m }).InsertEntryAsync();
		var id = (int)product!["ProductID"]!;
		await client.For("Products").Key(id).Set(new { UnitPrice = 2m }).UpdateEntryAsync();
		Request("PATCH", $"/Products/{id}").Should().NotBeNull();
		await client.For("Products").Key(id).DeleteEntryAsync();
		Request("DELETE", $"/Products/{id}").Should().NotBeNull();
		(await client.For("Products").Key(id).FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task CompoundKeysStayParenthesized()
	{
		var client = CreateSegmentClient();
		var detail = (await client.For("Order_Details").Top(1).FindEntriesAsync()).Single();
		var found = await client.For("Order_Details").Key(new { OrderID = detail["OrderID"], ProductID = detail["ProductID"] }).FindEntryAsync();
		found!["Quantity"].Should().Be(detail["Quantity"]);
		Requests.Should().Contain(x => x.Path.Contains($"Order_Details(OrderID={detail["OrderID"]},ProductID={detail["ProductID"]})", StringComparison.Ordinal));
	}
}
