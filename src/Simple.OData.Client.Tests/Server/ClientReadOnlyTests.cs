using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Command-text based (untyped) client API against the in-process server.</summary>
public sealed class ClientReadOnlyTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task FindEntries()
	{
		var products = await CreateClient().FindEntriesAsync("Products");
		products.Should().HaveCount(Seed.Products);
	}

	[Fact]
	public async Task FindEntriesNonExisting()
	{
		var products = await CreateClient().FindEntriesAsync("Products?$filter=ProductID eq -1");
		products.Should().BeEmpty();
	}

	[Fact]
	public async Task FindEntriesSelect()
	{
		var products = await CreateClient().FindEntriesAsync("Products?$select=ProductName");
		products[0].Should().ContainSingle().Which.Key.Should().Be("ProductName");
	}

	[Fact]
	public async Task FindEntriesWithAnnotations()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().FindEntriesAsync("Products?$top=5&$count=true", annotations);
		products.Should().HaveCount(5);
		annotations.Count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task FindEntriesFilterAny()
	{
		var orders = await CreateClient().FindEntriesAsync("Orders?$filter=Order_Details/any(d:d/Quantity gt 30)");
		orders.Should().HaveCount(Seed.OrdersWithAnyDetailOver30);
	}

	[Fact]
	public async Task FindEntriesFilterAll()
	{
		var orders = await CreateClient().FindEntriesAsync("Orders?$filter=Order_Details/all(d:d/Quantity ge 10)");
		orders.Should().HaveCount(Seed.OrdersWithAllDetailsAtLeast10);
	}

	[Fact]
	public async Task FindEntry()
	{
		var product = await CreateClient().FindEntryAsync("Products?$filter=ProductName eq 'Chai'");
		product!["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FindScalar()
	{
		var count = await CreateClient().FindScalarAsync("Products/$count");
		Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture).Should().Be(Seed.Products);
		var name = await CreateClient().FindScalarAsync("Products(1)/ProductName");
		name.Should().Be("Chai");
	}

	[Fact]
	public async Task GetEntryExisting()
	{
		var product = await CreateClient().GetEntryAsync("Products", new Entry { ["ProductID"] = 1 });
		product!["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task GetEntryExistingCompoundKey()
	{
		var detail = await CreateClient().GetEntryAsync("Order_Details", new Entry { ["OrderID"] = 10248, ["ProductID"] = 11 });
		detail!["ProductID"].Should().Be(11);
	}

	[Fact]
	public async Task GetEntryNonExisting()
	{
		var act = () => CreateClient(x => x.IgnoreResourceNotFoundException = false).GetEntryAsync("Products", new Entry { ["ProductID"] = -1 });
		await act.Should().ThrowAsync<WebRequestException>();
	}

	[Fact]
	public async Task GetEntryNonExistingIgnoreException()
	{
		var product = await CreateClient().GetEntryAsync("Products", new Entry { ["ProductID"] = -1 });
		product.Should().BeNull();
	}

	[Fact]
	public async Task ExecuteScalarFunctionWithStringParameter()
	{
		var result = await CreateClient().ExecuteFunctionAsScalarAsync<string>("PassThroughString", new Entry { ["value"] = "abc" });
		result.Should().Be("abc");
	}

	[Fact]
	public async Task ExecuteScalarFunctionWithIntParameter()
	{
		var result = await CreateClient().ExecuteFunctionAsScalarAsync<int>("PassThroughInt", new Entry { ["value"] = 42 });
		result.Should().Be(42);
	}

	[Fact]
	public async Task ExecuteScalarFunctionWithLongParameter()
	{
		var result = await CreateClient().ExecuteFunctionAsScalarAsync<long>("PassThroughLong", new Entry { ["value"] = 1L });
		result.Should().Be(1L);
	}

	[Fact]
	public async Task ExecuteScalarFunctionWithDateTimeParameter()
	{
		var dateTime = new DateTimeOffset(2013, 1, 1, 12, 13, 14, 789, TimeSpan.Zero);
		var result = await CreateClient().ExecuteFunctionAsScalarAsync<DateTimeOffset>("PassThroughDateTime", new Entry { ["value"] = dateTime });
		result.Should().Be(dateTime);
	}

	[Fact]
	public async Task ExecuteScalarFunctionWithGuidParameter()
	{
		var guid = new Guid("7d8f1758-00d4-4c53-a2e3-8fca73ebb92c");
		var result = await CreateClient().ExecuteFunctionAsScalarAsync<Guid>("PassThroughGuid", new Entry { ["value"] = guid });
		result.Should().Be(guid);
	}

	[Fact]
	public async Task FindEntryExistingDynamicFilter()
	{
		var client = CreateClient();
		var x = ODataDynamic.Expression;
		var filter = await (Task<string>)client.GetCommandTextAsync("Products", x.ProductName == "Chai");
		var product = await client.FindEntryAsync(filter);
		product!["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FindBaseClassEntryDynamicFilter()
	{
		var client = CreateClient();
		var x = ODataDynamic.Expression;
		var filter = await (Task<string>)client.GetCommandTextAsync("Transport", x.TransportID == 1);
		var ship = await client.FindEntryAsync(filter);
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task FindDerivedClassEntryDynamicFilter()
	{
		var client = CreateClient();
		var x = ODataDynamic.Expression;
		var filter = await (Task<string>)client.GetCommandTextAsync("Transport/NorthwindModel.Ship", x.ShipName == "Titanic");
		var ship = await client.FindEntryAsync(filter);
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task FindEntryExistingTypedFilter()
	{
		var client = CreateClient();
		var filter = await client.GetCommandTextAsync<Product>("Products", x => x.ProductName == "Chai");
		var product = await client.FindEntryAsync(filter);
		product!["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FindBaseClassEntryTypedFilter()
	{
		var client = CreateClient();
		var filter = await client.GetCommandTextAsync<Transport>("Transport", x => x.TransportID == 1);
		var ship = await client.FindEntryAsync(filter);
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task FindDerivedClassEntryTypedFilter()
	{
		var client = CreateClient();
		var filter = await client.GetCommandTextAsync<Ship>("Transport/NorthwindModel.Ship", x => x.ShipName == "Titanic");
		var ship = await client.FindEntryAsync(filter);
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task GetMetadata()
	{
		var client = CreateClient();
		var model = await client.GetMetadataAsync();
		model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
		(await client.GetMetadataDocumentAsync()).Should().Contain("EntityContainer");
	}
}
