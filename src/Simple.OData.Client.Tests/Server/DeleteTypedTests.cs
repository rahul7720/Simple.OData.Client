using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class DeleteTypedTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task DeleteByKey()
	{
		var client = CreateClient();
		var product = await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Key(product!.ProductID).DeleteEntryAsync();
		(await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByFilter()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Filter(x => x.ProductName == "Test1").DeleteEntriesAsync();
		(await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByObjectAsKey()
	{
		var client = CreateClient();
		var product = await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Key(product!).DeleteEntryAsync();
		(await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteDerived()
	{
		var client = CreateClient();
		var ship = await client.For<Transport>().As<Ship>().Set(new { ShipName = "Test1" }).InsertEntryAsync();
		await client.For<Transport>().As<Ship>().Key(ship!.TransportID).DeleteEntryAsync();
		(await client.For<Transport>().As<Ship>().Filter(x => x.ShipName == "Test1").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByCompoundKey()
	{
		var client = CreateClient();
		await client.For<OrderDetail>("Order_Details").Key(10248, 11).DeleteEntryAsync();
		(await client.For<OrderDetail>("Order_Details").Key(10248, 11).FindEntryAsync()).Should().BeNull();
		(await client.For<OrderDetail>("Order_Details").FindEntriesAsync()).Should().HaveCount(Seed.OrderDetails - 1);
	}
}
