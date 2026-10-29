using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Command-text based (untyped) modification API against the in-process server.</summary>
public sealed class ClientReadWriteTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task InsertEntryWithResult()
	{
		var product = await CreateClient().InsertEntryAsync("Products", new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 18m }, true);
		product!["ProductName"].Should().Be("Test1");
	}

	[Fact]
	public async Task InsertEntryNoResult()
	{
		var product = await CreateClient().InsertEntryAsync("Products", new Entry { ["ProductName"] = "Test2", ["UnitPrice"] = 18m }, false);
		product.Should().BeNull();
	}

	[Fact]
	public async Task InsertEntrySubcollection()
	{
		var ship = await CreateClient().InsertEntryAsync("Transport/NorthwindModel.Ship", new Entry { ["ShipName"] = "Test1" }, true);
		ship!["ShipName"].Should().Be("Test1");
	}

	[Fact]
	public async Task UpdateEntryWithResult()
	{
		var key = new Entry { ["ProductID"] = 1 };
		var product = await CreateClient().UpdateEntryAsync("Products", key, new Entry { ["ProductName"] = "Chai", ["UnitPrice"] = 123m }, true);
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateEntryNoResult()
	{
		var client = CreateClient();
		var key = new Entry { ["ProductID"] = 1 };
		var product = await client.UpdateEntryAsync("Products", key, new Entry { ["ProductName"] = "Chai", ["UnitPrice"] = 123m }, false);
		product.Should().BeNull();
		product = await client.GetEntryAsync("Products", key);
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateEntriesByFilter()
	{
		var client = CreateClient();
		var updated = await client.UpdateEntriesAsync("Products", "Products?$filter=CategoryID eq 1", new Entry { ["UnitPrice"] = 1m });
		updated.Should().HaveCount(Seed.BeveragesProducts);
		(await client.FindEntriesAsync("Products?$filter=CategoryID eq 1")).Should().OnlyContain(x => (decimal)x["UnitPrice"]! == 1m);
	}

	[Fact]
	public async Task UpdateEntrySubcollection()
	{
		var client = CreateClient();
		var ship = await client.InsertEntryAsync("Transport/NorthwindModel.Ship", new Entry { ["ShipName"] = "Test1" }, true);
		var key = new Entry { ["TransportID"] = ship!["TransportID"] };
		await client.UpdateEntryAsync("Transport/NorthwindModel.Ship", key, new Entry { ["ShipName"] = "Test2" });
		ship = await client.GetEntryAsync("Transport", key);
		ship!["ShipName"].Should().Be("Test2");
	}

	[Fact]
	public async Task UpdateEntrySubcollectionWithAnnotations()
	{
		var client = CreateClient(x => x.IncludeAnnotationsInResults = true);
		var ship = await client.InsertEntryAsync("Transport/NorthwindModel.Ship", new Entry { ["ShipName"] = "Test1" }, true);
		var key = new Entry { ["TransportID"] = ship!["TransportID"] };
		await client.UpdateEntryAsync("Transport/NorthwindModel.Ship", key, new Entry { ["ShipName"] = "Test2" });
		ship = await client.GetEntryAsync("Transport", key);
		ship!["ShipName"].Should().Be("Test2");
		((ODataEntryAnnotations)ship["__annotations"]!).TypeName.Should().Be("NorthwindModel.Ship");
	}

	[Fact]
	public async Task DeleteEntry()
	{
		var client = CreateClient();
		await client.InsertEntryAsync("Products", new Entry { ["ProductName"] = "Test3", ["UnitPrice"] = 18m }, true);
		var product = await client.FindEntryAsync("Products?$filter=ProductName eq 'Test3'");
		product.Should().NotBeNull();
		await client.DeleteEntryAsync("Products", product!);
		(await client.FindEntryAsync("Products?$filter=ProductName eq 'Test3'")).Should().BeNull();
	}

	[Fact]
	public async Task DeleteEntriesByFilter()
	{
		var client = CreateClient();
		var count = await client.DeleteEntriesAsync("Products", "Products?$filter=CategoryID eq 2");
		count.Should().Be(Seed.CondimentsProducts);
		(await client.FindEntriesAsync("Products")).Should().HaveCount(Seed.Products - Seed.CondimentsProducts);
	}

	[Fact]
	public async Task DeleteEntrySubCollection()
	{
		var client = CreateClient();
		var ship = await client.InsertEntryAsync("Transport/NorthwindModel.Ship", new Entry { ["ShipName"] = "Test3" }, true);
		ship = await client.FindEntryAsync("Transport?$filter=TransportID eq " + ship!["TransportID"]);
		ship.Should().NotBeNull();
		await client.DeleteEntryAsync("Transport", ship!);
		(await client.FindEntryAsync("Transport?$filter=TransportID eq " + ship["TransportID"])).Should().BeNull();
	}

	[Fact]
	public async Task DeleteEntrySubCollectionWithAnnotations()
	{
		var client = CreateClient(x => x.IncludeAnnotationsInResults = true);
		var ship = await client.InsertEntryAsync("Transport/NorthwindModel.Ship", new Entry { ["ShipName"] = "Test3" }, true);
		ship = await client.FindEntryAsync("Transport?$filter=TransportID eq " + ship!["TransportID"]);
		ship.Should().NotBeNull();
		await client.DeleteEntryAsync("Transport", ship!);
		(await client.FindEntryAsync("Transport?$filter=TransportID eq " + ship["TransportID"])).Should().BeNull();
	}

	[Fact]
	public async Task LinkEntry()
	{
		var client = CreateClient();
		var category = await client.InsertEntryAsync("Categories", new Entry { ["CategoryName"] = "Test4" }, true);
		var product = await client.InsertEntryAsync("Products", new Entry { ["ProductName"] = "Test5" }, true);
		await client.LinkEntryAsync("Products", product!, "Category", category!);
		product = await client.FindEntryAsync("Products?$filter=ProductName eq 'Test5'");
		product!["CategoryID"].Should().Be(category["CategoryID"]);
	}

	[Fact]
	public async Task UnlinkEntry()
	{
		var client = CreateClient();
		var category = await client.InsertEntryAsync("Categories", new Entry { ["CategoryName"] = "Test6" }, true);
		await client.InsertEntryAsync("Products", new Entry { ["ProductName"] = "Test7", ["CategoryID"] = category!["CategoryID"] }, true);
		var product = await client.FindEntryAsync("Products?$filter=ProductName eq 'Test7'");
		product!["CategoryID"].Should().Be(category["CategoryID"]);
		await client.UnlinkEntryAsync("Products", product, "Category");
		product = await client.FindEntryAsync("Products?$filter=ProductName eq 'Test7'");
		product!["CategoryID"].Should().BeNull();
	}
}
