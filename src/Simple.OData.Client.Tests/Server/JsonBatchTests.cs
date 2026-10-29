using System.Net;
using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;
using Untyped = System.Collections.Generic.IDictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>The JSON <c>$batch</c> format (<see cref="BatchFormat.Json"/>) against the in-process server.</summary>
public sealed class JsonBatchTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private ODataClient CreateJsonClient(Action<ODataClientSettings>? configure = null) => CreateClient(s =>
	{
		s.BatchFormat = BatchFormat.Json;
		configure?.Invoke(s);
	});

	[Fact]
	public async Task Success()
	{
		var client = CreateJsonClient();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test2", ["UnitPrice"] = 20m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		(await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync()).Should().NotBeNull();
		(await client.For("Products").Filter("ProductName eq 'Test2'").FindEntryAsync()).Should().NotBeNull();
		Requests.Single(x => x.IsBatch).Headers["Content-Type"].Should().StartWith("application/json");
	}

	[Fact]
	public async Task SuccessWithResults()
	{
		var client = CreateJsonClient();
		Untyped? product1 = null;
		Product? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += async c => product2 = await c.For<Product>().Set(new Product { ProductName = "Test2", UnitPrice = 20m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		product1!["ProductID"].Should().BeOfType<int>().Which.Should().BeGreaterThan(Seed.Products);
		product2!.ProductID.Should().BeGreaterThan(Seed.Products);
	}

	[Fact]
	public async Task ReadOnlyBatch()
	{
		Untyped? product = null;
		IReadOnlyList<Untyped>? products = null;
		var count = 0;
		var batch = new ODataBatch(CreateJsonClient());
		batch += async c => product = await c.For("Products").Key(1).FindEntryAsync();
		batch += async c => products = await c.For("Products").Filter("CategoryID eq 1").FindEntriesAsync();
		batch += async c => count = await c.For("Products").Count().FindScalarAsync<int>();
		await batch.ExecuteAsync();

		product!["ProductName"].Should().Be("Chai");
		products.Should().HaveCount(Seed.BeveragesProducts);
		count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task PartialFailures()
	{
		var client = CreateJsonClient(s => s.IgnoreResourceNotFoundException = false);
		Untyped? product1 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += c => c.For("Products").Key(99999).FindEntryAsync();

		var act = () => batch.ExecuteAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
		product1.Should().NotBeNull();
	}

	[Fact]
	public async Task ContinueOnError()
	{
		var client = CreateJsonClient(s =>
		{
			s.IgnoreResourceNotFoundException = false;
			s.ContinueOnError = true;
		});
		Untyped? product1 = null;
		Untyped? product3 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync();
		batch += c => c.For("Products").Key(99999).FindEntryAsync();
		batch += async c => product3 = await c.For("Products").Set(new Entry { ["ProductName"] = "Test3", ["UnitPrice"] = 30m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		product1.Should().NotBeNull();
		product3.Should().NotBeNull();
	}

	[Fact]
	public async Task ContentIdReference()
	{
		var client = CreateJsonClient();
		Untyped? category = null;
		var batch = new ODataBatch(client);
		batch += async c => category = await c.For("Categories").Set(new Entry { ["CategoryName"] = "Test13" }).InsertEntryAsync();
		batch += c => c.For("Products").Set(new Entry { ["ProductName"] = "Test14", ["UnitPrice"] = 21m, ["Category"] = category }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var product = await client.For("Products").Expand("Category").Filter("ProductName eq 'Test14'").FindEntryAsync();
		((Untyped)product!["Category"]!)["CategoryName"].Should().Be("Test13");
	}

	[Fact]
	public async Task UpdateDeleteAndFunction()
	{
		var client = CreateJsonClient();
		var product = await client.For("Products").Set(new Entry { ["ProductName"] = "Test11", ["UnitPrice"] = 21m }).InsertEntryAsync();
		Untyped? updated = null;
		var scalar = 0;
		var batch = new ODataBatch(client);
		batch += async c => updated = await c.For("Products").Key(product!).Set(new Entry { ["UnitPrice"] = 22m }).UpdateEntryAsync();
		batch += async c => scalar = await c.Unbound().Function("PassThroughInt").Set(new Entry { ["value"] = 7 }).ExecuteAsScalarAsync<int>();
		batch += c => c.For("Products").Key(product!).DeleteEntryAsync();
		await batch.ExecuteAsync();

		updated!["UnitPrice"].Should().Be(22m);
		scalar.Should().Be(7);
		(await client.For("Products").Key(product!).FindEntryAsync()).Should().BeNull();
	}
}
