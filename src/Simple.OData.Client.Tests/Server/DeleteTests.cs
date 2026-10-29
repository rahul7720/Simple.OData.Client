using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class DeleteTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task DeleteByKey()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Key(product!["ProductID"]).DeleteEntryAsync();
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByKeyClearMetadataCache()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		client.Session.ClearMetadataCache();
		await client.For("Products").Key(product!["ProductID"]).DeleteEntryAsync();
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByFilter()
	{
		var client = CreateClient();
		await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var count = await client.For("Products").Filter("ProductName eq 'Test1'").DeleteEntriesAsync();
		count.Should().Be(1);
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByFilterFromCommand()
	{
		var client = CreateClient();
		await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var commandText = await client.For("Products").Filter("ProductName eq 'Test1'").GetCommandTextAsync();
		await client.DeleteEntriesAsync("Products", commandText);
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteByObjectAsKey()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Key(product!).DeleteEntryAsync();
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteDerived()
	{
		var client = CreateClient();
		var ship = await client.For("Transport").As("Ship").Set(new { ShipName = "Test1" }).InsertEntryAsync();
		await client.For("Transport").As("Ship").Key(ship!["TransportID"]).DeleteEntryAsync();
		(await client.For("Transport").As("Ship").Filter("ShipName eq 'Test1'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task DeleteNonExistingThrows()
	{
		var client = CreateClient(x => x.IgnoreResourceNotFoundException = false);
		var act = () => client.For("Products").Key(-1).DeleteEntryAsync();
		await act.Should().ThrowAsync<WebRequestException>();
	}

	[Fact]
	public async Task DeleteNonExistingIsIgnoredWhenConfigured()
	{
		var act = () => CreateClient().For("Products").Key(-1).DeleteEntryAsync();
		await act.Should().NotThrowAsync();
	}
}
