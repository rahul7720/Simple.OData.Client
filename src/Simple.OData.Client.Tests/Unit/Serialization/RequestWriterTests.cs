using System.Text.Json;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class RequestWriterTests
{
	private static RequestWriter Writer(Action<ODataClientSettings>? configure = null) =>
		new(MetadataClientFactory.CreateSession("Northwind4.xml", configure), null);

	private static RequestWriter ThingsWriter(Action<ODataClientSettings>? configure = null) =>
		new(TestModels.SerializationSession(configure), null);

	private static async Task<string> BodyAsync(ODataRequest request) => await request.RequestMessage.Content!.ReadAsStringAsync();

	private static async Task<JsonElement> JsonAsync(ODataRequest request) => JsonDocument.Parse(await BodyAsync(request)).RootElement;

	private static Dictionary<string, object?> AllProductProperties(int productId) => new()
	{
		["ProductID"] = productId,
		["SupplierID"] = 2,
		["CategoryID"] = 3,
		["ProductName"] = "Chai",
		["QuantityPerUnit"] = "10",
		["UnitPrice"] = 20m,
		["UnitsInStock"] = (short)100,
		["UnitsOnOrder"] = (short)1000,
		["ReorderLevel"] = (short)500,
		["Discontinued"] = false,
	};

	private static readonly Dictionary<string, object?> ProductKey = new() { ["ProductID"] = 1 };

	[Fact]
	public async Task UpdateWithPartialPropertiesUsesPatch()
	{
		var request = await Writer().CreateUpdateRequestAsync("Products", "Products(1)", ProductKey, new Dictionary<string, object?> { ["ProductName"] = "Chai" }, false);
		request.Method.Should().Be("PATCH");
		var json = await JsonAsync(request);
		json.GetProperty("ProductName").GetString().Should().Be("Chai");
		json.TryGetProperty("UnitPrice", out _).Should().BeFalse();
	}

	[Fact]
	public async Task UpdateWithAllPropertiesUsesPatchByDefault()
	{
		var request = await Writer().CreateUpdateRequestAsync("Products", "Products(1)", ProductKey, AllProductProperties(1), false);
		request.Method.Should().Be("PATCH");
	}

	[Fact]
	public async Task UpdateWithPreferredPutUsesPut()
	{
		var request = await Writer(s => s.PreferredUpdateMethod = ODataUpdateMethod.Put).CreateUpdateRequestAsync("Products", "Products(1)", ProductKey, AllProductProperties(1), false);
		request.Method.Should().Be("PUT");
	}

	[Fact]
	public async Task UpdateWithChangedKeyUsesPut()
	{
		var request = await Writer().CreateUpdateRequestAsync("Products", "Products(1)", ProductKey, AllProductProperties(10), false);
		request.Method.Should().Be("PUT");
	}

	[Fact]
	public async Task InsertCreatesPostWithHeaders()
	{
		var request = await Writer().CreateInsertRequestAsync("Employees", "Employees",
			new Dictionary<string, object?> { ["FirstName"] = "John", ["LastName"] = "Smith" }, true,
			new Dictionary<string, string> { ["header1"] = "header1Value", ["header2"] = "header2Value" });
		request.Method.Should().Be("POST");
		request.Headers["header1"].Should().Be("header1Value");
		request.RequestMessage.Headers.GetValues("header2").Should().Equal("header2Value");
		request.RequestMessage.RequestUri!.AbsoluteUri.Should().EndWith("/Northwind4.xml/Employees");
		request.RequestMessage.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
		request.RequestMessage.Content.Headers.ContentLength.Should().BeGreaterThan(0);
		var json = await JsonAsync(request);
		json.GetProperty("FirstName").GetString().Should().Be("John");
	}

	[Fact]
	public async Task InsertWritesTemporalValues()
	{
		var birthDate = new DateTime(1980, 5, 6, 7, 8, 9, DateTimeKind.Unspecified);
		var json = await JsonAsync(await Writer().CreateInsertRequestAsync("Employees", "Employees", new Dictionary<string, object?>
		{
			["FirstName"] = "John",
			["BirthDate"] = birthDate,
			["HireDate"] = new DateTime(2020, 1, 2),
		}, false));
		json.GetProperty("BirthDate").GetString().Should().StartWith("1980-05-06T07:08:09");
		json.GetProperty("HireDate").GetString().Should().Be("2020-01-02");

		json = await JsonAsync(await Writer().CreateInsertRequestAsync("Employees", "Employees", new Dictionary<string, object?>
		{
			["BirthDate"] = null,
			["HireDate"] = new DateOnly(2021, 3, 4),
		}, false));
		json.GetProperty("BirthDate").ValueKind.Should().Be(JsonValueKind.Null);
		json.GetProperty("HireDate").GetString().Should().Be("2021-03-04");
	}

	[Fact]
	public async Task InsertWritesComplexCollectionEnumAndDurationValues()
	{
		var json = await JsonAsync(await ThingsWriter().CreateInsertRequestAsync("Things", "Things", new Dictionary<string, object?>
		{
			["Name"] = "thing",
			["Tags"] = new[] { "a", "b" },
			["Location"] = new { City = "Berlin", Zip = "10115" },
			["Addresses"] = new object[] { new Dictionary<string, object?> { ["City"] = "Paris" } },
			["Color"] = TestColor.Blue,
			["When"] = new DateOnly(2020, 1, 2),
			["At"] = new TimeOnly(10, 30),
			["Span"] = TimeSpan.FromMinutes(90),
			["Price"] = 1.5m,
			["Big"] = 5L,
		}, false));
		json.GetProperty("Tags").EnumerateArray().Select(x => x.GetString()).Should().Equal("a", "b");
		json.GetProperty("Location").GetProperty("City").GetString().Should().Be("Berlin");
		json.GetProperty("Addresses")[0].GetProperty("City").GetString().Should().Be("Paris");
		json.GetProperty("Color").GetString().Should().Be("Blue");
		json.GetProperty("When").GetString().Should().Be("2020-01-02");
		json.GetProperty("At").GetString().Should().StartWith("10:30:00");
		json.GetProperty("Span").GetString().Should().Be("PT1H30M");
		json.GetProperty("Price").GetDecimal().Should().Be(1.5m);
		json.GetProperty("Big").GetInt64().Should().Be(5);
	}

	[Fact]
	public async Task InsertWritesEnumFromString()
	{
		var json = await JsonAsync(await ThingsWriter().CreateInsertRequestAsync("Things", "Things", new Dictionary<string, object?> { ["Color"] = "Red" }, false));
		json.GetProperty("Color").GetString().Should().Be("Red");
	}

	[Fact]
	public async Task Ieee754CompatibleWritesLongAndDecimalAsStrings()
	{
		var json = await JsonAsync(await ThingsWriter(s => s.Ieee754Compatible = true).CreateInsertRequestAsync("Things", "Things",
			new Dictionary<string, object?> { ["Price"] = 1.5m, ["Big"] = 5L }, false));
		json.GetProperty("Price").GetString().Should().Be("1.5");
		json.GetProperty("Big").GetString().Should().Be("5");
	}

	[Fact]
	public async Task InsertWritesTypeAnnotationPerProtocolVersion()
	{
		var v4 = await BodyAsync(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "p" }, false));
		v4.Should().Contain("\"@odata.type\":\"#NorthwindModel.Product\"");

		var v401 = await BodyAsync(await Writer(s => s.ProtocolVersion = ODataProtocolVersion.V401).CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "p" }, false));
		v401.Should().Contain("\"@type\":\"#NorthwindModel.Product\"").And.NotContain("@odata.type");
	}

	[Fact]
	public async Task InsertWithKeyedNavigationValueWritesBind()
	{
		var json = await JsonAsync(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?>
		{
			["ProductName"] = "p",
			["Category"] = new { CategoryID = 1 },
		}, false));
		json.GetProperty("Category@odata.bind").GetString().Should().EndWith("Categories(1)");
		json.TryGetProperty("Category", out _).Should().BeFalse();

		json = await JsonAsync(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?>
		{
			["ProductName"] = "p",
			["Category"] = new Category { CategoryID = 2, CategoryName = "Beverages" },
		}, false));
		json.GetProperty("Category@odata.bind").GetString().Should().EndWith("Categories(2)");
	}

	[Fact]
	public async Task InsertWithKeyedCollectionWritesBindArray()
	{
		var json = await JsonAsync(await Writer().CreateInsertRequestAsync("Categories", "Categories", new Dictionary<string, object?>
		{
			["CategoryName"] = "c",
			["Products"] = new object[] { new { ProductID = 1 }, new Dictionary<string, object?> { ["ProductID"] = 2 } },
		}, false));
		json.GetProperty("Products@odata.bind").EnumerateArray().Select(x => x.GetString()!).Should().SatisfyRespectively(
			x => x.Should().EndWith("Products(1)"),
			x => x.Should().EndWith("Products(2)"));
	}

	[Fact]
	public async Task InsertWritesDeepNestedEntities()
	{
		var json = await JsonAsync(await Writer().CreateInsertRequestAsync("Categories", "Categories", new Dictionary<string, object?>
		{
			["CategoryName"] = "c",
			["Products"] = new object[] { new { ProductName = "p1" }, new { ProductName = "p2", UnitPrice = 3m } },
		}, false));
		var products = json.GetProperty("Products");
		products.GetArrayLength().Should().Be(2);
		products[0].GetProperty("ProductName").GetString().Should().Be("p1");
		products[1].GetProperty("UnitPrice").GetDecimal().Should().Be(3m);
		json.TryGetProperty("Products@odata.bind", out _).Should().BeFalse();

		json = await JsonAsync(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?>
		{
			["ProductName"] = "p",
			["Category"] = new { CategoryName = "new" },
		}, false));
		json.GetProperty("Category").GetProperty("CategoryName").GetString().Should().Be("new");
	}

	[Fact]
	public async Task InsertWithNullNavigationValueOmitsIt()
	{
		var json = await JsonAsync(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?>
		{
			["ProductName"] = "p",
			["Category"] = null,
		}, false));
		json.TryGetProperty("Category", out _).Should().BeFalse();
		json.TryGetProperty("Category@odata.bind", out _).Should().BeFalse();
	}

	[Fact]
	public async Task InsertIntoDerivedCollectionStripsTypeSegmentAndAnnotatesType()
	{
		var request = await Writer().CreateInsertRequestAsync("Transport/NorthwindModel.Ships", "Transport/NorthwindModel.Ships", new Dictionary<string, object?> { ["ShipName"] = "s" }, false);
		request.CommandText.Should().Be("Transport");
		(await BodyAsync(request)).Should().Contain("\"@odata.type\":\"#NorthwindModel.Ships\"");
	}

	[Fact]
	public async Task PatchUnderV401WritesNestedDeltaCollection()
	{
		var request = await Writer(s => s.ProtocolVersion = ODataProtocolVersion.V401).CreateUpdateRequestAsync("Categories", "Categories(1)",
			new Dictionary<string, object?> { ["CategoryID"] = 1 },
			new Dictionary<string, object?>
			{
				["CategoryName"] = "renamed",
				["Products"] = new object[] { new { ProductID = 5, ProductName = "p5" }, new { ProductName = "new" } },
			}, false);
		request.Method.Should().Be("PATCH");
		var json = await JsonAsync(request);
		json.GetProperty("CategoryName").GetString().Should().Be("renamed");
		var delta = json.GetProperty("Products@delta");
		delta.GetArrayLength().Should().Be(2);
		delta[0].GetProperty("@id").GetString().Should().EndWith("Products(5)");
		delta[0].GetProperty("ProductName").GetString().Should().Be("p5");
		delta[1].TryGetProperty("@id", out _).Should().BeFalse();
	}

	[Fact]
	public async Task PatchUnderV4WithKeyedCollectionWritesBind()
	{
		var json = await JsonAsync(await Writer().CreateUpdateRequestAsync("Categories", "Categories(1)",
			new Dictionary<string, object?> { ["CategoryID"] = 1 },
			new Dictionary<string, object?> { ["Products"] = new object[] { new { ProductID = 5, ProductName = "ignored" } } }, false));
		json.GetProperty("Products@odata.bind")[0].GetString().Should().EndWith("Products(5)");
	}

	[Fact]
	public async Task DeleteRequestHasNoContent()
	{
		var request = await Writer().CreateDeleteRequestAsync("Products", "Products(1)");
		request.Method.Should().Be("DELETE");
		request.RequestMessage.Content.Should().BeNull();
		request.CheckOptimisticConcurrency.Should().BeFalse();
		request.Preferences.Should().BeEmpty();
	}

	[Fact]
	public async Task LinkRequests()
	{
		var single = await Writer().CreateLinkRequestAsync("Products", "Category", "Products(1)", "Categories(2)");
		single.Method.Should().Be("PUT");
		single.CommandText.Should().Be("Products(1)/Category/$ref");
		single.IsLink.Should().BeTrue();
		var body = await BodyAsync(single);
		body.Should().Contain("@odata.id").And.Contain("Categories(2)");

		var collection = await Writer().CreateLinkRequestAsync("Categories", "Products", "Categories(1)", "Products(2)");
		collection.Method.Should().Be("POST");
		collection.CommandText.Should().Be("Categories(1)/Products/$ref");
	}

	[Fact]
	public async Task UnlinkRequests()
	{
		var single = await Writer().CreateUnlinkRequestAsync("Products", "Category", "Products(1)", null);
		single.Method.Should().Be("DELETE");
		single.CommandText.Should().Be("Products(1)/Category/$ref");
		single.RequestMessage.Content.Should().BeNull();

		var collection = await Writer().CreateUnlinkRequestAsync("Categories", "Products", "Categories(1)", "Products(2)");
		collection.CommandText.Should().Be("Categories(1)/Products(2)/$ref", because: "OData 4.01 addresses a collection member reference through a key segment");

		var keyAsSegment = await Writer(s => s.KeyAsSegment = true).CreateUnlinkRequestAsync("Categories", "Products", "Categories/1", "Products/2");
		keyAsSegment.CommandText.Should().Be("Categories/1/Products/2/$ref");
	}

	[Fact]
	public async Task FunctionAndParameterlessActionRequests()
	{
		var function = await Writer().CreateFunctionRequestAsync("PassThroughIntCollection(numbers=@p1)?@p1=[1,2]", "PassThroughIntCollection");
		function.Method.Should().Be("GET");
		function.ResultRequired.Should().BeTrue();
		function.RequestMessage.Content.Should().BeNull();

		var action = await ThingsWriter().CreateActionRequestAsync("Things/NS.Reset", "Reset", "NS.Thing", null, false);
		action.Method.Should().Be("POST");
		action.RequestMessage.Content.Should().BeNull();
	}

	[Fact]
	public async Task PreferencesFollowResultRequired()
	{
		(await Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "p" }, true)).Preferences.Should().Equal("return=representation");
		var update = await Writer().CreateUpdateRequestAsync("Products", "Products(1)", ProductKey, new Dictionary<string, object?> { ["ProductName"] = "p" }, false);
		update.Preferences.Should().Equal("return=minimal");
		update.RequestMessage.Headers.GetValues("Prefer").Should().Equal("return=minimal");
		(await Writer().CreateGetRequestAsync("Products", false)).Preferences.Should().BeEmpty();
	}

	[Fact]
	public async Task GetRequestFlags()
	{
		var request = await Writer().CreateGetRequestAsync("Products/$count", true);
		request.Method.Should().Be("GET");
		request.ReturnsScalarResult.Should().BeTrue();
		request.ResultRequired.Should().BeTrue();
		request.RequestMessage.Content.Should().BeNull();
	}

	[Fact]
	public async Task PutStreamCopiesBytesAndContentType()
	{
		var bytes = new byte[] { 1, 2, 3, 4 };
		var request = await Writer().CreatePutRequestAsync("Products(1)/Photo", new MemoryStream(bytes), "image/png", false);
		request.Method.Should().Be("PUT");
		request.RequestMessage.Content!.Headers.ContentType!.MediaType.Should().Be("image/png");
		(await request.RequestMessage.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

		var text = await Writer().CreatePutRequestAsync("Products(1)/Notes", new MemoryStream("hello"u8.ToArray()), "text/plain", true);
		(await BodyAsync(text)).Should().Be("hello");
		text.CheckOptimisticConcurrency.Should().BeTrue();
	}

	[Fact]
	public async Task UnknownPropertyThrowsUnlessIgnored()
	{
		var act = () => Writer().CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["Nope"] = 1 }, false);
		await act.Should().ThrowAsync<UnresolvableObjectException>();
		var request = await Writer(s => s.IgnoreUnmappedProperties = true).CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["Nope"] = 1, ["ProductName"] = "p" }, false);
		(await JsonAsync(request)).TryGetProperty("Nope", out _).Should().BeFalse();
	}

	private enum TestColor
	{
		Red = 1,
		Blue = 2,
	}
}
