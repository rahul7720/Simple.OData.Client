using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class DeleteDynamicTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task DeleteByKey()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Key(product.ProductID).DeleteEntryAsync();
		object? found = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		found.Should().BeNull();
	}

	[Fact]
	public async Task DeleteByFilter()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Filter(x.ProductName == "Test1").DeleteEntriesAsync();
		object? found = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		found.Should().BeNull();
	}

	[Fact]
	public async Task DeleteByObjectAsKey()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Key(product).DeleteEntryAsync();
		object? found = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		found.Should().BeNull();
	}

	[Fact]
	public async Task DeleteDerived()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic ship = await client.For(x.Transport).As(x.Ship).Set(x.ShipName = "Test1").InsertEntryAsync();
		await client.For(x.Transport).As(x.Ship).Key(ship.TransportID).DeleteEntryAsync();
		object? found = await client.For(x.Transport).As(x.Ship).Filter(x.ShipName == "Test1").FindEntryAsync();
		found.Should().BeNull();
	}
}
