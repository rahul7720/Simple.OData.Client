using System.Net;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Dynamic-expression <c>$batch</c> scenarios against the in-process server.</summary>
public sealed class BatchDynamicTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task Success()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 10m).InsertEntryAsync(false);
		batch += c => c.For(x.Products).Set(x.ProductName = "Test2", x.UnitPrice = 20m).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		dynamic product = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(10m);
		product = await client.For(x.Products).Filter(x.ProductName == "Test2").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(20m);
	}

	[Fact]
	public async Task SuccessWithResults()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic? product1 = null;
		dynamic? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 10m).InsertEntryAsync();
		batch += async c => product2 = await c.For(x.Products).Set(x.ProductName = "Test2", x.UnitPrice = 20m).InsertEntryAsync();
		await batch.ExecuteAsync();

		int id1 = product1!.ProductID;
		int id2 = product2!.ProductID;
		id1.Should().BeGreaterThan(Seed.Products);
		id2.Should().NotBe(id1);
		dynamic product = await client.For(x.Products).Key(id1).FindEntryAsync();
		((string)product.ProductName).Should().Be("Test1");
	}

	[Fact]
	public async Task PartialFailures()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient(s => s.IgnoreResourceNotFoundException = false);
		dynamic? product1 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 10m).InsertEntryAsync();
		batch += c => c.For(x.Products).Key(99999).FindEntryAsync();

		var act = () => batch.ExecuteAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
		string name = product1!.ProductName;
		name.Should().Be("Test1");
	}

	[Fact]
	public async Task UpdateDeleteSingleBatch()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic? product = null;
		dynamic? product1 = null;
		dynamic? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product = await c.For(x.Products).Set(x.ProductName = "Test11", x.UnitPrice = 21m).InsertEntryAsync();
		await batch.ExecuteAsync();
		int id = product!.ProductID;

		batch = new ODataBatch(client);
		batch += c => c.For(x.Products).Key(id).Set(x.UnitPrice = 222m).UpdateEntryAsync(false);
		batch += async c => product1 = await c.For(x.Products).Filter(x.ProductName == "Test11").FindEntryAsync();
		batch += c => c.For(x.Products).Key(id).DeleteEntryAsync();
		batch += async c => product2 = await c.For(x.Products).Filter(x.UnitPrice == 222m).FindEntryAsync();
		await batch.ExecuteAsync();

		((decimal)product1!.UnitPrice).Should().Be(222m);
		((object?)product2).Should().BeNull();
	}

	[Fact]
	public async Task InsertSingleEntityWithSingleAssociationSingleBatch()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic? category = null;
		var batch = new ODataBatch(client);
		batch += async c => category = await c.For(x.Categories).Set(x.CategoryName = "Test13").InsertEntryAsync();
		batch += c => c.For(x.Products).Set(x.ProductName = "Test14", x.UnitPrice = 21m, x.Category = category).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		dynamic product = await client.For(x.Products).Expand(x.Category).Filter(x.ProductName == "Test14").FindEntryAsync();
		((string)product.Category.CategoryName).Should().Be("Test13");
	}

	[Fact]
	public async Task InsertSingleEntityWithMultipleAssociationsSingleBatch()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic? product1 = null;
		dynamic? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For(x.Products).Set(x.ProductName = "Test15", x.UnitPrice = 21m).InsertEntryAsync();
		batch += async c => product2 = await c.For(x.Products).Set(x.ProductName = "Test16", x.UnitPrice = 22m).InsertEntryAsync();
		batch += c => c.For(x.Categories).Set(x.CategoryName = "Test17", x.Products = new[] { product1, product2 }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		dynamic category = await client.For(x.Categories).Expand(x.Products).Filter(x.CategoryName == "Test17").FindEntryAsync();
		((IEnumerable<dynamic>)category.Products).Should().HaveCount(2);
	}
}
