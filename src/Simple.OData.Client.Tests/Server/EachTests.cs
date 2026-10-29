using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Bulk updates and deletes through the OData 4.01 <c>$each</c> segment.</summary>
public sealed class EachTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private ODataClient CreateEachClient(Action<ODataClientSettings>? configure = null) => CreateClient(s =>
	{
		s.BulkOperationMode = BulkOperationMode.Each;
		configure?.Invoke(s);
	});

	[Fact]
	public async Task UpdateEntriesSendsASinglePatchToEach()
	{
		var client = CreateEachClient();
		var updated = await client.For("Products").Filter("CategoryID eq 1").Set(new { UnitPrice = 1m }).UpdateEntriesAsync();
		updated.Should().HaveCount(Seed.BeveragesProducts);
		updated.Should().AllSatisfy(x => x["UnitPrice"].Should().Be(1m));

		var request = Request("PATCH", "/Products/$each");
		request.Query.Should().Contain("$filter=");
		Requests.Where(x => x.Method == "PATCH").Should().ContainSingle();
		(await client.For("Products").Filter("UnitPrice eq 1").FindEntriesAsync()).Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task UpdateEntriesWithoutResult()
	{
		var client = CreateEachClient();
		var updated = await client.For("Products").Filter("CategoryID eq 1").Set(new { UnitPrice = 2m }).UpdateEntriesAsync(resultRequired: false);
		updated.Should().BeEmpty();
		Request("PATCH", "/Products/$each").Headers["Prefer"].Should().Contain("return=minimal");
		(await client.For("Products").Filter("UnitPrice eq 2").FindEntriesAsync()).Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task UpdateEntriesTyped()
	{
		var updated = await CreateEachClient().For<Product>().Filter(x => x.CategoryID == 1).Set(new { UnitPrice = 3m }).UpdateEntriesAsync();
		updated.Should().HaveCount(Seed.BeveragesProducts);
		updated.Should().AllSatisfy(x => x.UnitPrice.Should().Be(3m));
	}

	[Fact]
	public async Task UpdateEntriesDynamic()
	{
		var x = ODataDynamic.Expression;
		IEnumerable<dynamic> updated = await CreateEachClient().For(x.Products).Filter(x.CategoryID == 1).Set(x.UnitPrice = 4m).UpdateEntriesAsync();
		updated.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task DeleteEntriesSendsASingleDeleteToEach()
	{
		var client = CreateEachClient();
		var count = await client.For("Products").Filter("CategoryID eq 2").DeleteEntriesAsync();
		count.Should().Be(-1, "the service does not report how many entries a $each delete removed");
		Request("DELETE", "/Products/$each").Query.Should().Contain("$filter=");
		(await client.For("Products").FindEntriesAsync()).Should().HaveCount(Seed.Products - Seed.CondimentsProducts);
	}

	[Fact]
	public async Task DeleteEntriesTyped()
	{
		var client = CreateEachClient();
		await client.For<Product>().Filter(x => x.UnitPrice > 50).DeleteEntriesAsync();
		(await client.For<Product>().FindEntriesAsync()).Should().HaveCount(Seed.Products - 3, "Carnarvon Tigers, Sir Rodney's Marmalade and Mishi Kobe Niku cost more than 50");
	}

	[Fact]
	public async Task ClientSideModeIteratesInstead()
	{
		var client = CreateClient();
		var updated = await client.For("Products").Filter("CategoryID eq 1").Set(new { UnitPrice = 5m }).UpdateEntriesAsync();
		updated.Should().HaveCount(Seed.BeveragesProducts);
		Requests.Should().NotContain(x => x.Path.EndsWith("/$each", StringComparison.Ordinal));
		Requests.Where(x => x.Method == "PATCH").Should().HaveCount(Seed.BeveragesProducts);
	}
}
