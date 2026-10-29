using System.Net;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class ResponseReaderTests
{
	private static readonly Session Northwind = MetadataClientFactory.CreateSession("Northwind4.xml");
	private static readonly Session Crm = MetadataClientFactory.CreateSession("DynamicsCRM.xml");

	private static string Open(Session session, string fragment) => $"{{\"@odata.context\":\"{session.BaseUri}$metadata#{fragment}\"";

	private static HttpResponseMessage Json(string body, string contentType = "application/json;odata.metadata=minimal", IDictionary<string, string>? headers = null) =>
		FakeHttpHandler.CreateResponse(HttpStatusCode.OK, body, contentType, headers);

	private static Task<ODataResponse> ReadAsync(Session session, HttpResponseMessage response, EntityCollection? expected = null) =>
		new ResponseReader(session).ReadAsync(response, expected, CancellationToken.None);

	[Fact]
	public async Task ReadsFeedWithCountAndNextLink()
	{
		var body = $$$"""{{{Open(Northwind, "Products")}}},"@odata.count":2,"@odata.nextLink":"{{{Northwind.BaseUri}}}Products?$skiptoken=2","value":[{"ProductID":1,"ProductName":"Chai"},{"ProductID":2,"ProductName":"Chang"}]}""";
		var response = await ReadAsync(Northwind, Json(body));
		response.StatusCode.Should().Be(200);
		var entries = response.AsEntries(false);
		entries.Should().HaveCount(2);
		entries[0]["ProductName"].Should().Be("Chai");
		entries[0]["ProductID"].Should().Be(1);
		response.Feed!.Annotations!.Count.Should().Be(2);
		response.Feed.Annotations.NextPageLink!.Query.Should().Contain("$skiptoken=2");
	}

	[Fact]
	public async Task ReadsFourOhOneResponseWithShortControlInformation()
	{
		// a 4.01 service answers OData-MaxVersion: 4.01 with OData-Version: 4.01 and prefix-less control information
		var body = $$$"""{"@context":"{{{Northwind.BaseUri}}}$metadata#Products","@count":2,"value":[{"@etag":"W/\"1\"","ProductID":1,"ProductName":"Chai"},{"@etag":"W/\"2\"","ProductID":2,"ProductName":"Chang"}]}""";
		var response = await ReadAsync(Northwind, Json(body, headers: new Dictionary<string, string> { ["OData-Version"] = "4.01" }));
		var entries = response.AsEntries(false);
		entries.Should().HaveCount(2);
		entries[1]["ProductName"].Should().Be("Chang");
		response.Feed!.Annotations!.Count.Should().Be(2);
	}

	[Fact]
	public async Task ReadsSingleEntryWithAnnotationsAndEtagHeader()
	{
		var body = $$$"""{{{Open(Northwind, "Products/$entity")}}},"@odata.etag":"W/\"1\"","ProductID":1,"ProductName":"Chai","UnitPrice":18.5}""";
		var response = await ReadAsync(Northwind, Json(body, headers: new Dictionary<string, string> { ["ETag"] = "W/\"1\"" }));
		var entry = response.AsEntry(false)!;
		entry["ProductName"].Should().Be("Chai");
		entry["UnitPrice"].Should().Be(18.5m);
		entry.Should().NotContainKey(ReservedNames.Annotations);
		response.ETag.Should().Be("W/\"1\"");

		var annotations = response.Feed!.Entries[0].Annotations!;
		annotations.ETag.Should().Be("W/\"1\"");
		annotations.TypeName.Should().Be("NorthwindModel.Product");
		annotations.Id.Should().EndWith("Products(1)");
		var withAnnotations = response.AsEntry(true)!;
		withAnnotations[ReservedNames.Annotations].Should().BeSameAs(annotations);
	}

	[Fact]
	public async Task ReadsExpandedNavigationProperties()
	{
		var body = $$$"""{{{Open(Northwind, "Products(Category())/$entity")}}},"ProductID":1,"Category":{"CategoryID":2,"CategoryName":"Beverages"}}""";
		var entry = (await ReadAsync(Northwind, Json(body))).AsEntry(false)!;
		entry["Category"].Should().BeAssignableTo<IDictionary<string, object?>>().Which["CategoryName"].Should().Be("Beverages");

		body = $$$"""{{{Open(Northwind, "Categories(Products())/$entity")}}},"CategoryID":2,"Products":[{"ProductID":1,"ProductName":"Chai"},{"ProductID":2,"ProductName":"Chang"}]}""";
		entry = (await ReadAsync(Northwind, Json(body))).AsEntry(false)!;
		var products = entry["Products"].Should().BeAssignableTo<IEnumerable<IDictionary<string, object?>>>().Which.ToList();
		products.Should().HaveCount(2);
		products[1]["ProductName"].Should().Be("Chang");

		body = $$$"""{{{Open(Northwind, "Products(Category())/$entity")}}},"ProductID":1,"Category":null}""";
		entry = (await ReadAsync(Northwind, Json(body))).AsEntry(false)!;
		entry["Category"].Should().BeNull();
	}

	[Fact]
	public async Task ReadsFeedWithMetadataNone()
	{
		var products = new EntityCollection("Products");
		var feed = await ReadAsync(Northwind, Json("""{"value":[{"ProductID":1,"ProductName":"Chai"}]}""", "application/json;odata.metadata=none"), products);
		feed.AsEntries(false).Should().ContainSingle().Which["ProductName"].Should().Be("Chai");

		var entry = await ReadAsync(Northwind, Json("""{"ProductID":1,"ProductName":"Chai"}""", "application/json;odata.metadata=none"), products);
		entry.AsEntry(false)!["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task ReadsPrimitiveCollection()
	{
		var body = $$$"""{{{Open(Northwind, "Collection(Edm.Int32)")}}},"value":[1,2,3]}""";
		var response = await ReadAsync(Northwind, Json(body));
		response.AsArray<int>().Should().Equal(1, 2, 3);
		response.AsArray<string>().Should().Equal("1", "2", "3");
	}

	[Fact]
	public async Task ReadsRawAndPropertyValues()
	{
		var raw = await ReadAsync(Northwind, Json("42", "text/plain"));
		raw.AsScalar<int>().Should().Be(42);
		raw.AsScalar<string>().Should().Be("42");

		var property = await ReadAsync(Northwind, Json($$$"""{{{Open(Northwind, "Edm.String")}}},"value":"Chai"}"""));
		property.AsScalar<string>().Should().Be("Chai");

		var nullValue = await ReadAsync(Northwind, Json($$$"""{{{Open(Northwind, "Edm.String")}}},"value":null}"""));
		nullValue.AsScalar<string>().Should().BeNull();
	}

	[Fact]
	public async Task ReadsComplexTypeResult()
	{
		var body = Resources.Read("ExampleActionComplexType.json");
		var response = await ReadAsync(Crm, Json(body));
		response.AsEntry(false)!["SomeProperty"].Should().Be("MyPropertyValue");
	}

	[Fact]
	public async Task ReadsNestedFeedLinkAnnotations()
	{
		var onlyLink = await ReadAsync(Crm, Json(Resources.Read("AccountTasksOnlyLink.json")));
		var entry = onlyLink.Feed!.Entries[0];
		entry.LinkAnnotations.Should().NotBeNull();
		entry.LinkAnnotations!.NextPageLink.Should().NotBeNull();
		entry.Data["Account_Tasks"].Should().BeAssignableTo<IEnumerable<IDictionary<string, object?>>>().Which.Should().BeEmpty();

		var withData = await ReadAsync(Crm, Json(Resources.Read("AccountTasksAndLink.json")));
		entry = withData.Feed!.Entries[0];
		entry.LinkAnnotations!.NextPageLink.Should().NotBeNull();
		entry.Data["Account_Tasks"].Should().BeAssignableTo<IEnumerable<IDictionary<string, object?>>>().Which.Should().NotBeEmpty();
	}

	[Fact]
	public async Task ReadsTypedValuesFromInlineModel()
	{
		var session = TestModels.SerializationSession();
		var body = $$$"""{{{Open(session, "Things/$entity")}}},"Id":1,"Color":"Red","Tags":["a","b"],"Location":{"City":"Berlin"},"Addresses":[{"City":"Paris"}],"When":"2020-01-02","At":"10:30:00","Span":"PT1H","Big":5,"Price":1.5}""";
		var entry = (await ReadAsync(session, Json(body))).AsEntry(false)!;
		entry["Color"].Should().Be("Red");
		entry["Tags"].Should().BeAssignableTo<IEnumerable<object?>>().Which.Should().Equal("a", "b");
		entry["Location"].Should().BeAssignableTo<IDictionary<string, object?>>().Which["City"].Should().Be("Berlin");
		entry["Addresses"].Should().BeAssignableTo<IEnumerable<IDictionary<string, object?>>>().Which.Single()["City"].Should().Be("Paris");
		entry["When"].Should().Be(new DateOnly(2020, 1, 2));
		entry["At"].Should().Be(new TimeOnly(10, 30));
		entry["Span"].Should().Be(TimeSpan.FromHours(1));
		entry["Big"].Should().Be(5L);
		entry["Price"].Should().Be(1.5m);
	}

	[Fact]
	public async Task ReadsOpenTypeDynamicProperties()
	{
		var session = MetadataClientFactory.CreateSession("OpenTypes.xml");
		var body = $$$"""{{{Open(session, "TheRecords/$entity")}}},"Dyn":5,"DynText":"x","DynObj":{"a":1}}""";
		var entry = (await ReadAsync(session, Json(body))).AsEntry(false)!;
		entry["Dyn"].Should().Be(5);
		entry["DynText"].Should().Be("x");
		entry["DynObj"].Should().BeAssignableTo<IDictionary<string, object?>>().Which["a"].Should().Be(1);
	}

	[Fact]
	public async Task NoContentResponseHasNoFeed()
	{
		var response = await ReadAsync(Northwind, FakeHttpHandler.CreateResponse(HttpStatusCode.NoContent, headers: new Dictionary<string, string> { ["OData-EntityId"] = "Products(1)" }));
		response.StatusCode.Should().Be(204);
		response.Feed.Should().BeNull();
		response.AsEntries(false).Should().BeEmpty();
		response.AsEntry(false).Should().BeNull();
		response.ODataEntityId.Should().Be("Products(1)");
	}

	[Fact]
	public async Task ReadsMultipartBatchResponse()
	{
		var entry = $$$"""{{{Open(Northwind, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai"}""";
		var body = string.Join("\r\n",
			"--batchresponse_1",
			"Content-Type: application/http",
			"Content-Transfer-Encoding: binary",
			"",
			"HTTP/1.1 200 OK",
			"Content-Type: application/json;odata.metadata=minimal",
			"",
			entry,
			"--batchresponse_1",
			"Content-Type: application/http",
			"Content-Transfer-Encoding: binary",
			"",
			"HTTP/1.1 404 Not Found",
			"Content-Type: application/json",
			"",
			"""{"error":{"code":"404","message":"Not found"}}""",
			"--batchresponse_1",
			"Content-Type: application/http",
			"Content-Transfer-Encoding: binary",
			"",
			"HTTP/1.1 204 No Content",
			"",
			"",
			"--batchresponse_1--",
			"");
		var response = await ReadAsync(Northwind, Json(body, "multipart/mixed; boundary=batchresponse_1"));
		response.Batch.Should().HaveCount(3);
		response.Batch![0].AsEntry(false)!["ProductName"].Should().Be("Chai");
		response.Batch[1].StatusCode.Should().Be(404);
		response.Batch[1].Error!.Message.Should().Be("Not found");
		response.Batch[1].Exception.Should().NotBeNull();
		response.Batch[2].StatusCode.Should().Be(204);
	}
}
