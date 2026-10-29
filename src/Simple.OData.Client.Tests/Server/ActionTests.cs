using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;
using Untyped = System.Collections.Generic.IDictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Unbound and bound action scenarios against the in-process server.</summary>
public sealed class ActionTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task BoundActionWithoutResult()
	{
		var client = CreateClient();
		await client.For("Products").Key(1).Action("Rate").Set(new Entry { ["rating"] = 4 }).ExecuteAsync();
		(await client.For("Products").Key(1).FindEntryAsync())!["Rating"].Should().Be(4);
	}

	[Fact]
	public async Task BoundActionWithoutResultTyped()
	{
		var client = CreateClient();
		await client.For<ProductWithRating>("Products").Key(1).Action("Rate").Set(new { rating = 5 }).ExecuteAsync();
		(await client.For<ProductWithRating>("Products").Key(1).FindEntryAsync())!.Rating.Should().Be(5);
	}

	[Fact]
	public async Task BoundActionWithoutResultDynamic()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		await client.For(x.Products).Key(1).Action("Rate").Set(x.rating = 3).ExecuteAsync();
		dynamic product = await client.For(x.Products).Key(1).FindEntryAsync();
		((int)product.Rating).Should().Be(3);
	}

	[Fact]
	public async Task BoundActionReturningEntity()
	{
		var client = CreateClient();
		var product = await client.For("Products").Key(1).Action("CheckOut").ExecuteAsSingleAsync();
		product!["CheckedOut"].Should().Be(true);
		product = await client.For("Products").Key(1).Action("Return").ExecuteAsSingleAsync();
		product!["CheckedOut"].Should().Be(false);
	}

	[Fact]
	public async Task BoundActionReturningEntityTyped()
	{
		var product = await CreateClient().For<ProductWithRating>("Products").Key(2).Action("CheckOut").ExecuteAsSingleAsync();
		product!.ProductName.Should().Be("Chang");
		product.CheckedOut.Should().BeTrue();
	}

	[Fact]
	public async Task BoundCollectionActionWithScalarResult()
	{
		var client = CreateClient();
		var count = await client.For("Products").Action("DiscontinueAll").ExecuteAsScalarAsync<int>();
		count.Should().Be(Seed.Products);
		(await client.For("Products").Filter("Discontinued eq true").FindEntriesAsync()).Should().HaveCount(Seed.Products);
	}

	[Fact]
	public async Task ActionWithScalarResult()
	{
		var result = await CreateClient().Unbound().Action("Echo").Set(new Entry { ["value"] = 42 }).ExecuteAsScalarAsync<int>();
		result.Should().Be(42);
	}

	[Fact]
	public async Task ActionWithoutParametersOrResult()
	{
		var client = CreateClient();
		await client.For("Products").Key(1).Action("Rate").Set(new Entry { ["rating"] = 4 }).ExecuteAsync();
		await client.Unbound().Action("ResetData").ExecuteAsync();
		(await client.For("Products").Key(1).FindEntryAsync())!["Rating"].Should().BeNull();
	}

	[Fact]
	public async Task ActionWithSingleEntityResult()
	{
		var client = CreateClient();
		var product = await client.Unbound<Untyped>().Action("CreateProduct").Set(new Entry { ["name"] = "Test1", ["price"] = 18m }).ExecuteAsSingleAsync();
		product!["ProductName"].Should().Be("Test1");
		product["UnitPrice"].Should().Be(18m);
		(await client.For("Products").Key(product["ProductID"]).FindEntryAsync())!["ProductName"].Should().Be("Test1");
	}

	[Fact]
	public async Task ActionWithSingleEntityResultTyped()
	{
		var product = await CreateClient().Unbound<Product>().Action("CreateProduct").Set(new { name = "Test1", price = 18m }).ExecuteAsSingleAsync();
		product!.ProductName.Should().Be("Test1");
		product.ProductID.Should().BeGreaterThan(Seed.Products);
	}

	[Fact]
	public async Task ActionWithEnumerableResult()
	{
		var products = await CreateClient().Unbound<Untyped>().Action("ListProducts").Set(new Entry { ["categoryId"] = 1 }).ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ActionWithEnumerableResultTyped()
	{
		var products = await CreateClient().Unbound<Product>().Action("ListProducts").Set(new { categoryId = 2 }).ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.CondimentsProducts);
		products.Should().AllSatisfy(x => x.CategoryID.Should().Be(2));
	}

	[Fact]
	public async Task ActionWithEnumResult()
	{
		var client = CreateClient();
		(await client.Unbound().Action("GetAddressType").ExecuteAsScalarAsync<AddressType>()).Should().Be(AddressType.Shipping);
		(await client.Unbound().Action("GetAddressType").ExecuteAsScalarAsync<string>()).Should().Be("Shipping");
	}

	[Fact]
	public async Task ActionWithEnumParameter()
	{
		var client = CreateClient();
		var customers = await client.Unbound<Untyped>().Action("FilterCustomersByAddressType").Set(new Entry { ["type"] = AddressType.Work }).ExecuteAsEnumerableAsync();
		customers.Should().HaveCount(Seed.WorkAddressCustomers);
		customers = await client.Unbound<Untyped>().Action("FilterCustomersByAddressType").Set(new Entry { ["type"] = "Home" }).ExecuteAsEnumerableAsync();
		customers.Single()["CustomerID"].Should().Be("ANATR");
	}

	[Fact]
	public async Task ActionWithEnumParameterTyped()
	{
		var customers = await CreateClient().Unbound<Customer>().Action("FilterCustomersByAddressType").Set(new { type = AddressType.Shipping }).ExecuteAsEnumerableAsync();
		customers.Single().CustomerID.Should().Be("ANTON");
	}

	[Fact]
	public async Task ActionWithComplexParameter()
	{
		var address = new Address { City = "Oslo", Country = "Norway", Type = AddressType.Home };
		var result = await CreateClient().Unbound<Untyped>().Action("UpdateAddress").Set(new Entry { ["address"] = address }).ExecuteAsSingleAsync();
		result!["City"].Should().Be("Oslo");
		result["Country"].Should().Be("Norway");
	}

	[Fact]
	public async Task ActionWithComplexParameterTyped()
	{
		var address = new Address { City = "Oslo", Country = "Norway", Type = AddressType.Shipping };
		var result = await CreateClient().Unbound<Address>().Action("UpdateAddress").Set(new { address }).ExecuteAsSingleAsync();
		result!.City.Should().Be("Oslo");
		result.Type.Should().Be(AddressType.Shipping);
	}

	[Fact]
	public async Task ActionWithEntityCollectionParameter()
	{
		var client = CreateClient();
		var products = await client.For("Products").Top(3).FindEntriesAsync();
		var merged = await client.Unbound().Action("MergeProducts").Set(new Entry { ["products"] = products }).ExecuteAsScalarAsync<int>();
		merged.Should().Be(3);
	}

	[Fact]
	public async Task ActionWithEntityCollectionParameterTyped()
	{
		var client = CreateClient();
		var products = await client.For<Product>().Top(2).FindEntriesAsync();
		var merged = await client.Unbound().Action("MergeProducts").Set(new { products }).ExecuteAsScalarAsync<int>();
		merged.Should().Be(2);
	}

	[Fact]
	public async Task ActionsInBatch()
	{
		var client = CreateClient();
		IDictionary<string, object?>? created = null;
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(1).Action("Rate").Set(new Entry { ["rating"] = 2 }).ExecuteAsync();
		batch += c => c.For("Products").Key(2).Action("Rate").Set(new Entry { ["rating"] = 1 }).ExecuteAsync();
		batch += async c => { created = await c.Unbound<Untyped>().Action("CreateProduct").Set(new Entry { ["name"] = "Batched", ["price"] = 1m }).ExecuteAsSingleAsync(); };
		await batch.ExecuteAsync();

		created!["ProductName"].Should().Be("Batched");
		(await client.For("Products").Key(1).FindEntryAsync())!["Rating"].Should().Be(2);
		(await client.For("Products").Key(2).FindEntryAsync())!["Rating"].Should().Be(1);
	}

	[Fact]
	public async Task ActionsThroughClientApi()
	{
		var client = CreateClient();
		(await client.ExecuteActionAsScalarAsync<int>("Echo", new Entry { ["value"] = 5 })).Should().Be(5);
		(await client.ExecuteActionAsEnumerableAsync("ListProducts", new Entry { ["categoryId"] = 1 })).Should().HaveCount(Seed.BeveragesProducts);
		(await client.ExecuteActionAsSingleAsync("CreateProduct", new Entry { ["name"] = "Test1", ["price"] = 2m }))!["ProductName"].Should().Be("Test1");
		await client.ExecuteActionAsync("ResetData");
		(await client.For("Products").FindEntriesAsync()).Should().HaveCount(Seed.Products);
	}

	[Fact]
	public async Task ActionCommandText()
	{
		var client = CreateClient();
		(await client.Unbound().Action("Echo").GetCommandTextAsync()).Should().Be("Echo");
		(await client.For("Products").Key(1).Action("Rate").GetCommandTextAsync()).Should().Be("Products(1)/NorthwindModel.Rate");
		(await client.For("Products").Action("DiscontinueAll").GetCommandTextAsync()).Should().Be("Products/NorthwindModel.DiscontinueAll");
	}
}
