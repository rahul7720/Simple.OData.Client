using System.Net;
using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;
using Untyped = System.Collections.Generic.IDictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Untyped <c>$batch</c> scenarios against the in-process server.</summary>
public sealed class BatchTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task Success()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test2", ["UnitPrice"] = 20m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().NotBeNull();
		(await client.For("Products").Filter("ProductName eq 'Test2'").FindEntryAsync()).Should().NotBeNull();
		Requests.Should().ContainSingle(x => x.IsBatch);
		Requests.Where(x => !x.IsBatch && x.Method == "POST").Should().HaveCount(2, "both inserts travelled inside the batch");
	}

	[Fact]
	public async Task SuccessWithSettings()
	{
		var settings = new ODataClientSettings(CreateHttpClient());
		var batch = new ODataBatch(settings);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();
		(await new ODataClient(settings).For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().NotBeNull();
	}

	[Fact]
	public async Task EmptyBatch()
	{
		await new ODataBatch(CreateClient()).ExecuteAsync();
		Requests.Should().NotContain(x => x.IsBatch);
	}

	[Fact]
	public async Task ReadOnlyBatch()
	{
		Untyped? product = null;
		IReadOnlyList<Untyped>? products = null;
		var batch = new ODataBatch(CreateClient());
		batch += async c => product = await c.For("Products").Key(1).FindEntryAsync();
		batch += async c => products = await c.For("Products").Filter("CategoryID eq 1").FindEntriesAsync();
		await batch.ExecuteAsync();

		product!["ProductName"].Should().Be("Chai");
		products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task NestedBatch()
	{
		var client = CreateClient();
		var batch1 = new ODataBatch(client);
		batch1 += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync(false);
		batch1 += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test2", ["UnitPrice"] = 20m }).InsertEntryAsync(false);
		var batch2 = new ODataBatch(client);
		batch2 += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test3", ["UnitPrice"] = 30m }).InsertEntryAsync(false);
		batch2 += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test4", ["UnitPrice"] = 40m }).InsertEntryAsync(false);
		await batch2.ExecuteAsync();
		await batch1.ExecuteAsync();

		foreach (var name in new[] { "Test1", "Test2", "Test3", "Test4" })
		{
			(await client.For("Products").Filter($"ProductName eq '{name}'").FindEntryAsync()).Should().NotBeNull(name);
		}
	}

	[Fact]
	public async Task SuccessWithResults()
	{
		var client = CreateClient();
		Untyped? product1 = null;
		Untyped? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += async c => product2 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test2", ["UnitPrice"] = 20m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		product1!["ProductID"].Should().BeOfType<int>().Which.Should().BeGreaterThan(Seed.Products);
		product2!["ProductID"].Should().NotBe(product1["ProductID"]);
		(await client.For("Products").Key(product1["ProductID"]).FindEntryAsync())!["ProductName"].Should().Be("Test1");
	}

	[Fact]
	public async Task PartialFailures()
	{
		var client = CreateClient(s => s.IgnoreResourceNotFoundException = false);
		Untyped? product1 = null;
		Untyped? missing = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += async c => missing = await c.For("Products").Key(99999).FindEntryAsync();

		var act = () => batch.ExecuteAsync();
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.NotFound);

		product1.Should().NotBeNull("results before the failure are assigned");
		missing.Should().BeNull();
		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().NotBeNull();
	}

	[Fact]
	public async Task AllFailures()
	{
		var batch = new ODataBatch(CreateClient(s => s.IgnoreResourceNotFoundException = false));
		batch += c => c.For("Products").Key(99998).FindEntryAsync();
		batch += c => c.For("Products").Key(99999).FindEntryAsync();

		var act = () => batch.ExecuteAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task ContinueOnError()
	{
		var client = CreateClient(s =>
		{
			s.IgnoreResourceNotFoundException = false;
			s.ContinueOnError = true;
		});
		Untyped? product1 = null;
		Untyped? missing = null;
		Untyped? product3 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += async c => missing = await c.For("Products").Key(99999).FindEntryAsync();
		batch += async c => product3 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test3", ["UnitPrice"] = 30m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		product1.Should().NotBeNull();
		missing.Should().BeNull();
		product3.Should().NotBeNull();
		(await client.For("Products").Filter("startswith(ProductName,'Test')").FindEntriesAsync()).Should().HaveCount(2);
		Requests.Single(x => x.IsBatch).HasHeader("Prefer", "odata.continue-on-error").Should().BeTrue();
	}

	[Fact]
	public async Task MultipleUpdateEntrySingleBatch()
	{
		var client = CreateClient();
		Untyped? product = null;
		Untyped? product1 = null;
		Untyped? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product = await c.For("Products").Set(new Entry { ["ProductName"] = "Test11", ["UnitPrice"] = 21m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product!).Set(new Entry { ["UnitPrice"] = 22m }).UpdateEntryAsync(false);
		batch += async c => product1 = await c.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync();
		batch += c => c.For("Products").Key(product!).Set(new Entry { ["UnitPrice"] = 23m }).UpdateEntryAsync(false);
		batch += async c => product2 = await c.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync();
		await batch.ExecuteAsync();

		product1!["UnitPrice"].Should().Be(22m);
		product2!["UnitPrice"].Should().Be(23m);
		(await client.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync())!["UnitPrice"].Should().Be(23m);
	}

	[Fact]
	public async Task MultipleUpdatesEntriesSingleBatch()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test11", ["UnitPrice"] = 121m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test12", ["UnitPrice"] = 121m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test13", ["UnitPrice"] = 121m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Filter("UnitPrice eq 121").Set(new Entry { ["UnitPrice"] = 122m }).UpdateEntriesAsync(false);
		await batch.ExecuteAsync();

		(await client.For("Products").Filter("UnitPrice eq 121").FindEntriesAsync()).Should().BeEmpty();
		(await client.For("Products").Filter("UnitPrice eq 122").FindEntriesAsync()).Should().HaveCount(3);
	}

	[Fact]
	public async Task UpdateEntriesWithEachInBatch()
	{
		var client = CreateClient(s => s.BulkOperationMode = BulkOperationMode.Each);
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Filter("CategoryID eq 1").Set(new Entry { ["UnitPrice"] = 1m }).UpdateEntriesAsync(false);
		await batch.ExecuteAsync();

		(await client.For("Products").Filter("UnitPrice eq 1").FindEntriesAsync()).Should().HaveCount(Seed.BeveragesProducts);
		Requests.Should().ContainSingle(x => !x.IsBatch && x.Path.EndsWith("/Products/$each", StringComparison.Ordinal));
	}

	[Fact]
	public async Task UpdateDeleteSingleBatch()
	{
		var client = CreateClient();
		Untyped? product = null;
		Untyped? product1 = null;
		Untyped? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product = await c.For("Products").Set(new Entry { ["ProductName"] = "Test11", ["UnitPrice"] = 21m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product!).Set(new Entry { ["UnitPrice"] = 22m }).UpdateEntryAsync(false);
		batch += async c => product1 = await c.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync();
		batch += c => c.For("Products").Key(product!).DeleteEntryAsync();
		batch += async c => product2 = await c.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync();
		await batch.ExecuteAsync();

		product1!["UnitPrice"].Should().Be(22m);
		product2.Should().BeNull();
		(await client.For("Products").Filter("ProductName eq 'Test11'").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task InsertUpdateDeleteSeparateBatches()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test12", ["UnitPrice"] = 21m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();
		var product = await client.For("Products").Filter("ProductName eq 'Test12'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(21m);

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product["ProductID"]).Set(new Entry { ["UnitPrice"] = 22m }).UpdateEntryAsync(false);
		await batch.ExecuteAsync();
		(await client.For("Products").Key(product["ProductID"]).FindEntryAsync())!["UnitPrice"].Should().Be(22m);

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product["ProductID"]).DeleteEntryAsync();
		await batch.ExecuteAsync();
		(await client.For("Products").Key(product["ProductID"]).FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task InsertSingleEntityWithSingleAssociationSingleBatch()
	{
		var client = CreateClient();
		Untyped? category = null;
		var batch = new ODataBatch(client);
		batch += async c => category = await c.For("Categories").Set(new Entry { ["CategoryName"] = "Test13" }).InsertEntryAsync();
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test14", ["UnitPrice"] = 21m, ["Category"] = category }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var product = await client.For("Products").Expand("Category").Filter("ProductName eq 'Test14'").FindEntryAsync();
		((Untyped)product!["Category"]!)["CategoryName"].Should().Be("Test13");
	}

	[Fact]
	public async Task InsertSingleEntityWithMultipleAssociationsSingleBatch()
	{
		var client = CreateClient();
		Untyped? product1 = null;
		Untyped? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test15", ["UnitPrice"] = 21m }).InsertEntryAsync();
		batch += async c => product2 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test16", ["UnitPrice"] = 22m }).InsertEntryAsync();
		batch += c => c.For("Categories").Set(new Entry { ["CategoryName"] = "Test17", ["Products"] = new[] { product1, product2 } }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var category = await client.For("Categories").Expand("Products").Filter("CategoryName eq 'Test17'").FindEntryAsync();
		((IEnumerable<object?>)category!["Products"]!).Should().HaveCount(2);
	}

	[Fact]
	public async Task DeleteEntryNonExistingFailsTheBatch()
	{
		// the service stops at the first failed operation, so a missing entity cannot be ignored inside a batch
		var batch = new ODataBatch(CreateClient());
		batch += c => c.For("Products").Key(99999).DeleteEntryAsync();
		var act = () => batch.ExecuteAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DeleteEntryNonExistingWithContinueOnError()
	{
		var client = CreateClient(s => s.ContinueOnError = true);
		Untyped? product = null;
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(99999).DeleteEntryAsync();
		batch += async c => product = await c.For("Products").Set(new Entry { ["ProductName"] = "AfterMiss", ["UnitPrice"] = 1m }).InsertEntryAsync();
		await batch.ExecuteAsync();
		product!["ProductName"].Should().Be("AfterMiss");
	}

	[Fact]
	public async Task DeleteEntriesInBatch()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test21", ["UnitPrice"] = 111m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test22", ["UnitPrice"] = 111m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test23", ["UnitPrice"] = 111m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Filter("UnitPrice eq 111").DeleteEntriesAsync();
		await batch.ExecuteAsync();
		(await client.For("Products").Filter("UnitPrice eq 111").FindEntriesAsync()).Should().BeEmpty();
	}

	[Fact]
	public async Task DeleteEntriesNonExistingThenInsert()
	{
		var client = CreateClient();
		Untyped? product = null;
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Filter("ProductName eq 'Test99'").DeleteEntriesAsync();
		batch += async c => product = await c.For("Products").Set(new Entry { ["ProductName"] = "Test15", ["UnitPrice"] = 21m }).InsertEntryAsync();
		batch += c => c.For("Categories").Set(new Entry { ["CategoryName"] = "Test17", ["Products"] = new[] { product } }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var category = await client.For("Categories").Expand("Products").Filter("CategoryName eq 'Test17'").FindEntryAsync();
		((IEnumerable<object?>)category!["Products"]!).Should().ContainSingle();
	}

	[Fact]
	public async Task UpdateWithResultRequired()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new Entry { ["ProductName"] = "Test11", ["UnitPrice"] = 21m }).InsertEntryAsync();
		Untyped? updated = null;
		var batch = new ODataBatch(client);
		batch += async c => updated = await c.For("Products").Key(product!).Set(new Entry { ["UnitPrice"] = 22m }).UpdateEntryAsync();
		await batch.ExecuteAsync();
		updated!["UnitPrice"].Should().Be(22m);
	}

	[Fact]
	public async Task FunctionsInBatch()
	{
		var scalar = 0;
		Untyped? mostExpensive = null;
		int[]? numbers = null;
		var batch = new ODataBatch(CreateClient());
		batch += async c => scalar = await c.Unbound().Function("PassThroughInt").Set(new Entry { ["value"] = 1 }).ExecuteAsScalarAsync<int>();
		batch += async c => mostExpensive = await c.For("Products").Function("MostExpensive").ExecuteAsSingleAsync();
		batch += async c => numbers = await c.Unbound().Function("PassThroughIntCollection").Set(new Entry { ["numbers"] = new[] { 1, 2 } }).ExecuteAsArrayAsync<int>();
		await batch.ExecuteAsync();

		scalar.Should().Be(1);
		mostExpensive!["ProductName"].Should().Be("Mishi Kobe Niku");
		numbers.Should().Equal(1, 2);
	}

	[Fact]
	public async Task LinkAndUnlinkInBatch()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new Entry { ["CategoryName"] = "Test4" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new Entry { ["ProductName"] = "Test5" }).InsertEntryAsync();

		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product!).LinkEntryAsync("Category", category!);
		await batch.ExecuteAsync();
		(await client.For("Products").Key(product!).FindEntryAsync())!["CategoryID"].Should().Be(category!["CategoryID"]);

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(product!).UnlinkEntryAsync("Category");
		await batch.ExecuteAsync();
		(await client.For("Products").Key(product!).FindEntryAsync())!["CategoryID"].Should().BeNull();
	}

	[Fact]
	public async Task CountInBatch()
	{
		var count = 0;
		var batch = new ODataBatch(CreateClient());
		batch += async c => count = await c.For("Products").Count().FindScalarAsync<int>();
		await batch.ExecuteAsync();
		count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task MediaStreamInBatch()
	{
		string? content = null;
		var batch = new ODataBatch(CreateClient());
		batch += async c => content = await c.For("Pictures").Key(1).Media().GetStreamAsStringAsync();
		await batch.ExecuteAsync();
		content.Should().Be("image-bytes");
	}
}
