using System.Net;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Fluent;

public sealed class ClientWithFakeHandlerTests
{
	private static (ODataClient Client, FakeHttpHandler Handler) Create(Action<ODataClientSettings>? configure = null)
	{
		var handler = new FakeHttpHandler();
		return (MetadataClientFactory.CreateClient("Northwind4.xml", configure, handler), handler);
	}

	private static string Context(ODataClient client, string fragment) => $"{{\"@odata.context\":\"{client.Session.BaseUri}$metadata#{fragment}\"";

	[Fact]
	public async Task FindEntriesReturnsTypedResults()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"value":[{"ProductID":1,"ProductName":"Chai","UnitPrice":18.0},{"ProductID":2,"ProductName":"Chang","UnitPrice":19.0}]}""");
		var products = await client.For<Product>().FindEntriesAsync();
		products.Should().HaveCount(2);
		products[1].ProductName.Should().Be("Chang");
		products[1].UnitPrice.Should().Be(19m);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("/Products");
		handler.LastRequest.Method.Should().Be(HttpMethod.Get);
	}

	[Fact]
	public async Task FindEntriesAsAsyncEnumerableFollowsNextLink()
	{
		var (client, handler) = Create();
		var next = new Uri(client.Session.BaseUri, "Products?$skiptoken=2");
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"@odata.nextLink":"{{{next}}}","value":[{"ProductID":1,"ProductName":"a"},{"ProductID":2,"ProductName":"b"}]}""");
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"value":[{"ProductID":3,"ProductName":"c"}]}""");

		var names = new List<string>();
		await foreach (var product in client.For<Product>().FindEntriesAsAsyncEnumerable())
		{
			names.Add(product.ProductName);
		}

		names.Should().Equal("a", "b", "c");
		handler.Requests.Should().HaveCount(2);
		handler.Requests[1].RequestUri.Should().Be(next);
	}

	[Fact]
	public async Task FindEntriesWithAnnotationsSetsCountAndNextLink()
	{
		var (client, handler) = Create();
		var next = new Uri(client.Session.BaseUri, "Products?$skiptoken=1");
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"@odata.count":7,"@odata.nextLink":"{{{next}}}","value":[{"ProductID":1,"ProductName":"a"}]}""");
		var annotations = new ODataFeedAnnotations();
		var products = await client.For<Product>().Top(1).FindEntriesAsync(annotations);
		products.Should().ContainSingle();
		annotations.Count.Should().Be(7);
		annotations.NextPageLink.Should().Be(next);
		handler.LastRequest.RequestUri!.Query.Should().Contain("$count=true").And.Contain("$top=1");

		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"value":[{"ProductID":2,"ProductName":"b"}]}""");
		var nextPage = await client.For<Product>().FindEntriesAsync(annotations.NextPageLink!, annotations);
		nextPage.Should().ContainSingle().Which.ProductName.Should().Be("b");
		annotations.NextPageLink.Should().BeNull();
	}

	[Fact]
	public async Task InsertEntryReturnsEntryOrNull()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.Created, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":10,"ProductName":"New"}""");
		var created = await client.For<Product>().Set(new { ProductName = "New" }).InsertEntryAsync();
		created!.ProductID.Should().Be(10);
		handler.LastRequest.Method.Should().Be(HttpMethod.Post);
		handler.LastRequestBody.Should().Contain("\"ProductName\":\"New\"");
		handler.LastRequest.Headers.GetValues("Prefer").Should().Equal("return=representation");

		handler.Enqueue(HttpStatusCode.NoContent);
		var silent = await client.For<Product>().Set(new { ProductName = "Quiet" }).InsertEntryAsync(resultRequired: true);
		silent.Should().BeNull();
		handler.Requests.Should().HaveCount(2);

		handler.Enqueue(HttpStatusCode.NoContent);
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":5,"ProductName":"Fetched"}""");
		var fetched = await client.For<Product>().Set(new { ProductID = 5, ProductName = "Fetched" }).InsertEntryAsync();
		fetched!.ProductName.Should().Be("Fetched");
		handler.LastRequest.Method.Should().Be(HttpMethod.Get);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products(5)");

		handler.Enqueue(HttpStatusCode.NoContent);
		(await client.For<Product>().Set(new { ProductName = "x" }).InsertEntryAsync(resultRequired: false)).Should().BeNull();
		handler.LastRequest.Headers.GetValues("Prefer").Should().Equal("return=minimal");
	}

	[Fact]
	public async Task UpdateEntryByKeyUsesPatch()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai","UnitPrice":2.0}""");
		var updated = await client.For<Product>().Key(1).Set(new { UnitPrice = 2m }).UpdateEntryAsync();
		updated!.UnitPrice.Should().Be(2m);
		handler.LastRequest.Method.Should().Be(HttpMethod.Patch);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products(1)");
		handler.LastRequestBody.Should().Contain("\"UnitPrice\":2");
	}

	[Fact]
	public async Task DeleteEntriesWithEachModeSendsSingleDelete()
	{
		var (client, handler) = Create(s => s.BulkOperationMode = BulkOperationMode.Each);
		handler.Enqueue(HttpStatusCode.NoContent);
		var count = await client.For<Product>().Filter(x => x.UnitPrice > 10).DeleteEntriesAsync();
		handler.Requests.Should().ContainSingle();
		handler.LastRequest.Method.Should().Be(HttpMethod.Delete);
		handler.LastRequest.RequestUri!.PathAndQuery.Should().EndWith("/Products/$each?$filter=UnitPrice%20gt%2010");
		count.Should().Be(-1);
	}

	[Fact]
	public async Task DeleteEntriesClientSideDeletesEachEntry()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"value":[{"ProductID":1},{"ProductID":2}]}""");
		handler.Enqueue(HttpStatusCode.NoContent);
		handler.Enqueue(HttpStatusCode.NoContent);
		var count = await client.For<Product>().Filter(x => x.UnitPrice > 10).DeleteEntriesAsync();
		count.Should().Be(2);
		handler.Requests.Should().HaveCount(3);
		handler.Requests[1].Method.Should().Be(HttpMethod.Delete);
		handler.Requests[1].RequestUri!.AbsoluteUri.Should().EndWith("Products(1)");
		handler.Requests[2].RequestUri!.AbsoluteUri.Should().EndWith("Products(2)");
	}

	[Fact]
	public async Task UpdateEntriesWithEachModeSendsSinglePatch()
	{
		var (client, handler) = Create(s => s.BulkOperationMode = BulkOperationMode.Each);
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"value":[{"ProductID":1,"UnitPrice":5.0},{"ProductID":2,"UnitPrice":5.0}]}""");
		var updated = await client.For<Product>().Filter(x => x.UnitPrice > 10).Set(new { UnitPrice = 5m }).UpdateEntriesAsync();
		handler.Requests.Should().ContainSingle();
		handler.LastRequest.Method.Should().Be(HttpMethod.Patch);
		handler.LastRequest.RequestUri!.PathAndQuery.Should().EndWith("/Products/$each?$filter=UnitPrice%20gt%2010");
		handler.LastRequestBody.Should().Contain("\"UnitPrice\":5");
		updated.Should().HaveCount(2);

		handler.Enqueue(HttpStatusCode.NoContent);
		(await client.For<Product>().Filter(x => x.UnitPrice > 10).Set(new { UnitPrice = 5m }).UpdateEntriesAsync(resultRequired: false)).Should().BeEmpty();
	}

	[Fact]
	public async Task FindDeltaAsyncReadsDelta()
	{
		var (client, handler) = Create();
		var b = client.Session.BaseUri;
		var deltaLink = new Uri(b, "Products?$deltatoken=1");
		handler.Enqueue(HttpStatusCode.OK, $$$"""{"@odata.context":"{{{b}}}$metadata#Products/$delta","@odata.deltaLink":"{{{b}}}Products?$deltatoken=2","value":[{"@odata.id":"{{{b}}}Products(1)","ProductID":1,"ProductName":"Changed"},{"@odata.context":"{{{b}}}$metadata#Products/$deletedEntity","id":"{{{b}}}Products(2)","reason":"deleted"}]}""");
		var delta = await client.FindDeltaAsync(deltaLink);
		delta.Changed.Should().ContainSingle().Which["ProductName"].Should().Be("Changed");
		delta.Deleted.Should().ContainSingle();
		delta.DeltaLink!.Query.Should().Contain("$deltatoken=2");
		handler.LastRequest.RequestUri.Should().Be(deltaLink);
	}

	[Fact]
	public async Task TrackChangesAndMaxPageSizeBecomePreferences()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products")}}},"@odata.deltaLink":"{{{client.Session.BaseUri}}}Products?$deltatoken=9","value":[]}""");
		var annotations = new ODataFeedAnnotations();
		await client.For<Product>().WithMaxPageSize(10).TrackChanges().FindEntriesAsync(annotations);
		handler.LastRequest.Headers.GetValues("Prefer").Should().Equal("odata.maxpagesize=10, odata.track-changes");
		annotations.DeltaLink!.Query.Should().Contain("$deltatoken=9");
	}

	[Fact]
	public async Task IgnoreResourceNotFoundReturnsEmptyResults()
	{
		var (client, handler) = Create(s => s.IgnoreResourceNotFoundException = true);
		handler.Enqueue(HttpStatusCode.NotFound, """{"error":{"code":"404","message":"nope"}}""", "application/json");
		(await client.For<Product>().Key(99).FindEntryAsync()).Should().BeNull();
		handler.Enqueue(HttpStatusCode.NotFound);
		(await client.For<Product>().Filter(x => x.UnitPrice > 1000).FindEntriesAsync()).Should().BeEmpty();

		var (strict, strictHandler) = Create();
		strictHandler.Enqueue(HttpStatusCode.NotFound, """{"error":{"code":"404","message":"nope"}}""", "application/json");
		var act = () => strict.For<Product>().Key(99).FindEntryAsync();
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.NotFound);
		exception.Error!.Message.Should().Be("nope");
	}

	[Fact]
	public async Task BatchExecutesViaSinglePost()
	{
		var (client, handler) = Create();
		var entry = $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai"}""";
		var body = string.Join("\r\n",
			"--batchresponse_1",
			"Content-Type: multipart/mixed; boundary=changesetresponse_1",
			"",
			"--changesetresponse_1",
			"Content-Type: application/http",
			"Content-Transfer-Encoding: binary",
			"Content-ID: 1",
			"",
			"HTTP/1.1 201 Created",
			"Content-Type: application/json;odata.metadata=minimal",
			"",
			$$$"""{{{Context(client, "Products/$entity")}}},"ProductID":10,"ProductName":"New"}""",
			"--changesetresponse_1--",
			"--batchresponse_1",
			"Content-Type: application/http",
			"Content-Transfer-Encoding: binary",
			"",
			"HTTP/1.1 200 OK",
			"Content-Type: application/json;odata.metadata=minimal",
			"",
			entry,
			"--batchresponse_1--",
			"");
		handler.Enqueue(HttpStatusCode.OK, body, "multipart/mixed; boundary=batchresponse_1");

		Product? inserted = null;
		Product? found = null;
		var batch = new ODataBatch(client);
		batch += async c => inserted = await c.For<Product>().Set(new { ProductName = "New" }).InsertEntryAsync();
		batch += async c => found = await c.For<Product>().Key(1).FindEntryAsync();
		await batch.ExecuteAsync();

		handler.Requests.Should().ContainSingle();
		handler.LastRequest.Method.Should().Be(HttpMethod.Post);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("/$batch");
		handler.LastRequestBody.Should().Contain("POST ").And.Contain("GET ").And.Contain("\"ProductName\":\"New\"");
		inserted!.ProductID.Should().Be(10);
		found!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task LinkAndUnlinkEntries()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.NoContent);
		await client.For<Product>().Key(1).LinkEntryAsync(x => x.Category, new Category { CategoryID = 2 });
		handler.LastRequest.Method.Should().Be(HttpMethod.Put);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products(1)/Category/$ref");
		handler.LastRequestBody.Should().Contain("Categories(2)");

		handler.Enqueue(HttpStatusCode.NoContent);
		await client.For<Product>().Key(1).UnlinkEntryAsync<Category>();
		handler.LastRequest.Method.Should().Be(HttpMethod.Delete);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products(1)/Category/$ref");
	}

	[Fact]
	public async Task MediaStreams()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, "hello", "text/plain");
		var text = await client.For<Product>().Key(1).Media("Notes").GetStreamAsStringAsync();
		text.Should().Be("hello");
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products(1)/Notes");
		handler.LastRequest.Headers.Accept.Should().BeEmpty();

		handler.Enqueue(HttpStatusCode.NoContent);
		await client.For<Product>().Key(1).Media("Photo").SetStreamAsync(new byte[] { 1, 2 }, "image/png", false);
		handler.LastRequest.Method.Should().Be(HttpMethod.Put);
		handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("image/png");
	}

	[Fact]
	public async Task FunctionsAndActions()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Collection(Edm.Int32)")}}},"value":[1,2,3]}""");
		var numbers = await client.Unbound().Function("PassThroughIntCollection").Set(new { numbers = new[] { 1, 2, 3 } }).ExecuteAsArrayAsync<int>();
		numbers.Should().Equal(1, 2, 3);
		handler.LastRequest.Method.Should().Be(HttpMethod.Get);
		handler.LastRequest.RequestUri!.PathAndQuery.Should().EndWith("PassThroughIntCollection(numbers=@p1)?@p1=[1,2,3]");

		var things = MetadataClientFactory.CreateClient("Northwind4.xml", null, handler);
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Collection(Edm.Int32)")}}},"value":[4]}""");
		(await things.ExecuteFunctionAsArrayAsync<int>("PassThroughIntCollection", new Dictionary<string, object?> { ["numbers"] = new[] { 4 } })).Should().Equal(4);
	}

	[Fact]
	public async Task UntypedApiAndDynamicEntries()
	{
		var (client, handler) = Create();
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai"}""");
		var entry = await client.GetEntryAsync("Products", new Dictionary<string, object?> { ["ProductID"] = 1 });
		entry!["ProductName"].Should().Be("Chai");

		var x = ODataDynamic.Expression;
		handler.Enqueue(HttpStatusCode.OK, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai"}""");
		dynamic product = await client.For(x.Products).Key(1).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");

		handler.Enqueue(HttpStatusCode.OK, "42", "text/plain");
		(await client.For<Product>().Count().FindScalarAsync<int>()).Should().Be(42);
		handler.LastRequest.RequestUri!.AbsoluteUri.Should().EndWith("Products/$count");
	}

	[Fact]
	public async Task RequestBuilderExposesRequestAndParsesResponse()
	{
		var (client, handler) = Create();
		var request = await client.For<Product>().Key(1).BuildRequestFor().FindEntryAsync();
		request.GetRequest().CommandText.Should().Be("Products(1)");
		var response = FakeHttpHandler.CreateResponse(HttpStatusCode.OK, $$$"""{{{Context(client, "Products/$entity")}}},"ProductID":1,"ProductName":"Chai"}""");
		using var withResponse = request.FromResponse(response);
		(await withResponse.ReadAsSingleAsync())!.ProductName.Should().Be("Chai");
		handler.Requests.Should().BeEmpty();
	}
}
