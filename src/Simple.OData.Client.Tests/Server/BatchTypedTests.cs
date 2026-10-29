using System.Net;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Typed <c>$batch</c> scenarios against the in-process server.</summary>
public sealed class BatchTypedTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task Success()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For<Product>().Set(new Product { ProductName = "Test1", UnitPrice = 10m }).InsertEntryAsync(false);
		batch += c => c.For<Product>().Set(new Product { ProductName = "Test2", UnitPrice = 20m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		(await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync()).Should().NotBeNull();
		(await client.For<Product>().Filter(x => x.ProductName == "Test2").FindEntryAsync()).Should().NotBeNull();
	}

	[Fact]
	public async Task SuccessWithResults()
	{
		var client = CreateClient();
		Product? product1 = null;
		Product? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For<Product>().Set(new Product { ProductName = "Test1", UnitPrice = 10m }).InsertEntryAsync();
		batch += async c => product2 = await c.For<Product>().Set(new Product { ProductName = "Test2", UnitPrice = 20m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		product1!.ProductID.Should().BeGreaterThan(Seed.Products);
		product2!.ProductID.Should().NotBe(product1.ProductID);
		(await client.For<Product>().Key(product2.ProductID).FindEntryAsync())!.ProductName.Should().Be("Test2");
	}

	[Fact]
	public async Task PartialFailures()
	{
		var client = CreateClient(s => s.IgnoreResourceNotFoundException = false);
		Product? product1 = null;
		var batch = new ODataBatch(client);
		batch += async c => product1 = await c.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 10m }).InsertEntryAsync();
		batch += c => c.For<Product>().Key(99999).FindEntryAsync();

		var act = () => batch.ExecuteAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
		product1!.ProductName.Should().Be("Test1");
	}

	[Fact]
	public async Task AllFailures()
	{
		var batch = new ODataBatch(CreateClient(s => s.IgnoreResourceNotFoundException = false));
		batch += c => c.For<Product>().Key(99998).FindEntryAsync();
		batch += c => c.For<Product>().Key(99999).FindEntryAsync();
		var act = () => batch.ExecuteAsync();
		await act.Should().ThrowAsync<WebRequestException>();
	}

	[Fact]
	public async Task MultipleUpdatesSingleBatch()
	{
		var client = CreateClient();
		Product? product = null;
		Product? product1 = null;
		Product? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product = await c.For<Product>().Set(new Product { ProductName = "Test11", UnitPrice = 21m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For<Product>().Key(product!.ProductID).Set(new { UnitPrice = 22m }).UpdateEntryAsync(false);
		batch += async c => product1 = await c.For<Product>().Filter(x => x.ProductName == "Test11").FindEntryAsync();
		batch += c => c.For<Product>().Key(product!.ProductID).Set(new { UnitPrice = 23m }).UpdateEntryAsync(false);
		batch += async c => product2 = await c.For<Product>().Filter(x => x.ProductName == "Test11").FindEntryAsync();
		await batch.ExecuteAsync();

		product1!.UnitPrice.Should().Be(22m);
		product2!.UnitPrice.Should().Be(23m);
	}

	[Fact]
	public async Task UpdateDeleteSingleBatch()
	{
		var client = CreateClient();
		Product? product = null;
		Product? product1 = null;
		Product? product2 = null;
		var batch = new ODataBatch(client);
		batch += async c => product = await c.For<Product>().Set(new Product { ProductName = "Test11", UnitPrice = 21m }).InsertEntryAsync();
		await batch.ExecuteAsync();

		batch = new ODataBatch(client);
		batch += c => c.For<Product>().Key(product!.ProductID).Set(new { UnitPrice = 222m }).UpdateEntryAsync(false);
		batch += async c => product1 = await c.For<Product>().Filter(x => x.ProductName == "Test11").FindEntryAsync();
		batch += c => c.For<Product>().Key(product!.ProductID).DeleteEntryAsync();
		batch += async c => product2 = await c.For<Product>().Filter(x => x.UnitPrice == 222m).FindEntryAsync();
		await batch.ExecuteAsync();

		product1!.UnitPrice.Should().Be(222m);
		product2.Should().BeNull();
		(await client.For<Product>().Filter(x => x.ProductName == "Test11").FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task InsertUpdateDeleteSeparateBatches()
	{
		var client = CreateClient();
		var batch = new ODataBatch(client);
		batch += c => c.For<Product>().Set(new Product { ProductName = "Test12", UnitPrice = 21m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();
		var product = await client.For<Product>().Filter(x => x.ProductName == "Test12").FindEntryAsync();
		product!.UnitPrice.Should().Be(21m);

		batch = new ODataBatch(client);
		batch += c => c.For<Product>().Key(product.ProductID).Set(new { UnitPrice = 22m }).UpdateEntryAsync(false);
		await batch.ExecuteAsync();
		(await client.For<Product>().Key(product.ProductID).FindEntryAsync())!.UnitPrice.Should().Be(22m);

		batch = new ODataBatch(client);
		batch += c => c.For<Product>().Key(product.ProductID).DeleteEntryAsync();
		await batch.ExecuteAsync();
		(await client.For<Product>().Key(product.ProductID).FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task InsertSingleEntityWithSingleAssociationSingleBatch()
	{
		var client = CreateClient();
		var category = new Category { CategoryName = "Test13" };
		var batch = new ODataBatch(client);
		batch += c => c.For<Category>().Set(category).InsertEntryAsync();
		batch += c => c.For<Product>().Set(new { ProductName = "Test14", UnitPrice = 21m, Category = category }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var product = await client.For<Product>().Expand(x => x.Category).Filter(x => x.ProductName == "Test14").FindEntryAsync();
		product!.Category!.CategoryName.Should().Be("Test13");
	}

	[Fact]
	public async Task InsertSingleEntityWithMultipleAssociationsSingleBatch()
	{
		var client = CreateClient();
		var product1 = new Product { ProductName = "Test15", UnitPrice = 21m };
		var product2 = new Product { ProductName = "Test16", UnitPrice = 22m };
		var batch = new ODataBatch(client);
		batch += c => c.For<Product>().Set(product1).InsertEntryAsync();
		batch += c => c.For<Product>().Set(product2).InsertEntryAsync();
		batch += c => c.For<Category>().Set(new { CategoryName = "Test17", Products = new[] { product1, product2 } }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		var category = await client.For<Category>().Expand(x => x.Products).Filter(x => x.CategoryName == "Test17").FindEntryAsync();
		category!.Products.Should().HaveCount(2);
	}

	[Fact]
	public async Task OperationsInBatch()
	{
		Product? mostExpensive = null;
		ProductWithRating? checkedOut = null;
		var batch = new ODataBatch(CreateClient());
		batch += async c => mostExpensive = await c.For<Product>().Function("MostExpensive").ExecuteAsSingleAsync();
		batch += async c => checkedOut = await c.For<ProductWithRating>("Products").Key(1).Action("CheckOut").ExecuteAsSingleAsync();
		await batch.ExecuteAsync();

		mostExpensive!.ProductName.Should().Be("Mishi Kobe Niku");
		checkedOut!.CheckedOut.Should().BeTrue();
	}
}
