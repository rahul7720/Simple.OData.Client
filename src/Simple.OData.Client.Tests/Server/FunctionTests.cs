using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;
using Untyped = System.Collections.Generic.IDictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Unbound and bound function scenarios against the in-process server.</summary>
public sealed class FunctionTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task FunctionWithString()
	{
		var result = await CreateClient().Unbound().Function("PassThroughString").Set(new Entry { ["value"] = "abc" }).ExecuteAsScalarAsync<string>();
		result.Should().Be("abc");
	}

	[Fact]
	public async Task FunctionWithStringAsSingle()
	{
		var result = await CreateClient().Unbound<string>().Function("PassThroughString").Set(new Entry { ["value"] = "abc" }).ExecuteAsSingleAsync();
		result.Should().Be("abc");
	}

	[Fact]
	public async Task FunctionWithInt()
	{
		var result = await CreateClient().Unbound().Function("PassThroughInt").Set(new Entry { ["value"] = 1 }).ExecuteAsScalarAsync<int>();
		result.Should().Be(1);
	}

	[Fact]
	public async Task FunctionWithLong()
	{
		var result = await CreateClient().Unbound().Function("PassThroughLong").Set(new Entry { ["value"] = 1L }).ExecuteAsScalarAsync<long>();
		result.Should().Be(1L);
	}

	[Fact]
	public async Task FunctionWithDateTimeOffset()
	{
		var value = new DateTimeOffset(2013, 1, 1, 12, 13, 14, TimeSpan.FromHours(2));
		var result = await CreateClient().Unbound().Function("PassThroughDateTime").Set(new Entry { ["value"] = value }).ExecuteAsScalarAsync<DateTimeOffset>();
		result.Should().Be(value);
	}

	[Fact]
	public async Task FunctionWithUtcDateTime()
	{
		var value = new DateTime(2013, 1, 1, 12, 13, 14, DateTimeKind.Utc);
		var result = await CreateClient().Unbound().Function("PassThroughDateTime").Set(new Entry { ["value"] = value }).ExecuteAsScalarAsync<DateTime>();
		result.Should().Be(value);
	}

	[Fact]
	public async Task FunctionWithGuid()
	{
		var value = new Guid("8DA69EAD-C2DC-4E1E-A588-BA9EB6AA7294");
		var result = await CreateClient().Unbound().Function("PassThroughGuid").Set(new Entry { ["value"] = value }).ExecuteAsScalarAsync<Guid>();
		result.Should().Be(value);
	}

	[Fact]
	public async Task FunctionWithIntCollectionSingleElement()
	{
		var result = await CreateClient().Unbound().Function("PassThroughIntCollection").Set(new Entry { ["numbers"] = new[] { 1 } }).ExecuteAsArrayAsync<int>();
		result.Should().Equal(1);
	}

	[Fact]
	public async Task FunctionWithIntCollectionMultipleElements()
	{
		var result = await CreateClient().Unbound().Function("PassThroughIntCollection").Set(new Entry { ["numbers"] = new[] { 1, 2, 3 } }).ExecuteAsArrayAsync<int>();
		result.Should().Equal(1, 2, 3);
	}

	[Fact]
	public async Task FunctionWithIntCollectionEmpty()
	{
		var result = await CreateClient().Unbound().Function("PassThroughIntCollection").Set(new Entry { ["numbers"] = Array.Empty<int>() }).ExecuteAsArrayAsync<int>();
		result.Should().BeEmpty();
	}

	[Fact]
	public async Task FunctionWithComplexType()
	{
		var address = new Address { City = "Oslo", Country = "Norway", Region = "Oslo", PostalCode = "1234", Type = AddressType.Work };
		var result = await CreateClient().Unbound<Untyped>().Function("PassThroughAddress").Set(new Entry { ["address"] = address }).ExecuteAsSingleAsync();
		result!["City"].Should().Be("Oslo");
		result["Country"].Should().Be("Norway");
	}

	[Fact]
	public async Task FunctionWithComplexTypeAsDictionary()
	{
		var address = new Entry { ["City"] = "Oslo", ["Country"] = "Norway", ["Type"] = "Home" };
		var result = await CreateClient().Unbound<Address>().Function("PassThroughAddress").Set(new Entry { ["address"] = address }).ExecuteAsSingleAsync();
		result!.City.Should().Be("Oslo");
		result.Type.Should().Be(AddressType.Home);
	}

	[Fact]
	public async Task FunctionWithoutParameters()
	{
		var result = await CreateClient().Unbound().Function("ProductCount").ExecuteAsScalarAsync<int>();
		result.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task FunctionReturningEntityCollection()
	{
		var products = await CreateClient().Unbound<Untyped>().Function("GetProductsByRating").Set(new Entry { ["rating"] = 0 }).ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.Products);
		products.Should().AllSatisfy(x => x.Should().ContainKey("ProductName"));
	}

	[Fact]
	public async Task FunctionReturningEntityCollectionTyped()
	{
		var products = await CreateClient().Unbound<Product>().Function("GetProductsByRating").Set(new { rating = 0 }).ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.Products);
		products.Should().Contain(x => x.ProductName == "Chai");
	}

	[Fact]
	public async Task FunctionReturningEntityCollectionWithQueryOptions()
	{
		var products = await CreateClient().Unbound<Untyped>().Function("GetProductsByRating").Set(new Entry { ["rating"] = 0 }).Top(2).ExecuteAsEnumerableAsync();
		products.Should().HaveCount(2);
	}

	[Fact]
	public async Task FunctionReturningEntityCollectionFiltersByRating()
	{
		var client = CreateClient();
		await client.For("Products").Key(1).Action("Rate").Set(new Entry { ["rating"] = 5 }).ExecuteAsync();
		var products = await client.Unbound<Untyped>().Function("GetProductsByRating").Set(new Entry { ["rating"] = 5 }).ExecuteAsEnumerableAsync();
		products.Single()["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task BoundCollectionFunction()
	{
		var product = await CreateClient().For("Products").Function("MostExpensive").ExecuteAsSingleAsync();
		product!["ProductName"].Should().Be("Mishi Kobe Niku");
	}

	[Fact]
	public async Task BoundCollectionFunctionTyped()
	{
		var product = await CreateClient().For<Product>().Function("MostExpensive").ExecuteAsSingleAsync();
		product!.ProductName.Should().Be("Mishi Kobe Niku");
		product.UnitPrice.Should().Be(97m);
	}

	[Fact]
	public async Task BoundCollectionFunctionDynamic()
	{
		var x = ODataDynamic.Expression;
		dynamic product = await CreateClient().For(x.Products).Function("MostExpensive").ExecuteAsSingleAsync();
		((string)product.ProductName).Should().Be("Mishi Kobe Niku");
	}

	[Fact]
	public async Task BoundEntityFunctionWithParameter()
	{
		var price = await CreateClient().For("Products").Key(1).Function("Discounted").Set(new Entry { ["percent"] = 50m }).ExecuteAsScalarAsync<decimal>();
		price.Should().Be(9m);
	}

	[Fact]
	public async Task BoundEntityFunctionWithParameterTyped()
	{
		var price = await CreateClient().For<Product>().Key(1).Function("Discounted").Set(new { percent = 25m }).ExecuteAsScalarAsync<decimal>();
		price.Should().Be(13.5m);
	}

	[Fact]
	public async Task FunctionsThroughClientApi()
	{
		var client = CreateClient();
		(await client.ExecuteFunctionAsScalarAsync<int>("PassThroughInt", new Entry { ["value"] = 7 })).Should().Be(7);
		(await client.ExecuteFunctionAsArrayAsync<int>("PassThroughIntCollection", new Entry { ["numbers"] = new[] { 4, 5 } })).Should().Equal(4, 5);
		(await client.ExecuteFunctionAsEnumerableAsync("GetProductsByRating", new Entry { ["rating"] = 0 })).Should().HaveCount(Seed.Products);
		(await client.ExecuteFunctionAsSingleAsync("PassThroughAddress", new Entry { ["address"] = new Entry { ["City"] = "Oslo", ["Type"] = "Work" } }))!["City"].Should().Be("Oslo");
	}

	[Fact]
	public async Task FunctionCommandText()
	{
		var client = CreateClient();
		(await client.Unbound().Function("PassThroughString").Set(new Entry { ["value"] = "abc" }).GetCommandTextAsync()).Should().Be("PassThroughString(value='abc')");
		(await client.For("Products").Function("MostExpensive").GetCommandTextAsync()).Should().Be("Products/NorthwindModel.MostExpensive()");
		(await client.For("Products").Key(1).Function("Discounted").Set(new Entry { ["percent"] = 50 }).GetCommandTextAsync()).Should().Be("Products(1)/NorthwindModel.Discounted(percent=50)");
	}
}
