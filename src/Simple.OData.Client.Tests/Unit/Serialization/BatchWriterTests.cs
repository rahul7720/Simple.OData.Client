using System.Collections.Concurrent;
using System.Text.Json;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class BatchWriterTests
{
	private static (Session Session, BatchWriter Batch, RequestWriter Writer) Create(Action<ODataClientSettings>? configure = null)
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", configure);
		var batch = new BatchWriter(session, new ConcurrentDictionary<object, IDictionary<string, object?>>());
		return (session, batch, new RequestWriter(session, new Lazy<BatchWriter>(() => batch)));
	}

	private static async Task<string> BodyAsync(HttpRequestMessage message) => await message.Content!.ReadAsStringAsync();

	[Fact]
	public async Task MultipartBatchGroupsChangesIntoChangesetsAndReferencesContentIds()
	{
		var (session, batch, writer) = Create();
		var category = new Dictionary<string, object?> { ["CategoryName"] = "Beverages" };
		batch.BatchEntries[category] = category;

		var insertCategory = await writer.CreateInsertRequestAsync("Categories", "Categories", category, true);
		insertCategory.RequestMessage.Content.Should().BeNull("operation bodies are written into the batch, not the request");

		var product = new Dictionary<string, object?> { ["ProductName"] = "Chai", ["Category"] = category };
		await writer.CreateInsertRequestAsync("Products", "Products", product, true, new Dictionary<string, string> { ["Header1"] = "HeaderValue1" });
		await writer.CreateUpdateRequestAsync("Products", "Products(1)", new Dictionary<string, object?> { ["ProductID"] = 1 }, new Dictionary<string, object?> { ["UnitPrice"] = 2m }, false);
		await writer.CreateGetRequestAsync("Products(1)", false);
		await writer.CreateDeleteRequestAsync("Products", "Products(2)");

		batch.HasOperations.Should().BeTrue();
		batch.LastOperationId.Should().Be(5);
		var message = await batch.EndBatchAsync();
		message.Method.Should().Be(HttpMethod.Post);
		message.RequestUri!.AbsoluteUri.Should().Be(session.BaseUri.AbsoluteUri + "$batch");
		message.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/mixed");

		var body = await BodyAsync(message);
		body.Should().Contain("--batch_").And.Contain("--changeset_");
		body.Should().Contain("Content-ID: 1").And.Contain("Content-ID: 2").And.Contain("Content-ID: 3");
		body.Should().Contain("\"Category@odata.bind\":\"$1\"");
		body.Should().Contain($"POST {session.BaseUri}Categories HTTP/1.1").And.Contain($"POST {session.BaseUri}Products HTTP/1.1");
		body.Should().Contain($"PATCH {session.BaseUri}Products(1) HTTP/1.1");
		body.Should().Contain($"GET {session.BaseUri}Products(1) HTTP/1.1");
		body.Should().Contain($"DELETE {session.BaseUri}Products(2) HTTP/1.1");
		body.Should().Contain("Prefer: return=representation").And.Contain("Prefer: return=minimal");
		body.IndexOf("GET ", StringComparison.Ordinal).Should().BeGreaterThan(body.IndexOf("PATCH ", StringComparison.Ordinal));
	}

	[Fact]
	public async Task OperationHeadersAreWrittenIntoMultipartParts()
	{
		var (_, batch, writer) = Create();
		var headers = new Dictionary<string, string> { ["x-custom"] = "yes" };
		await writer.CreateGetRequestAsync("Products(1)", false, headers);
		await writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "Chai" }, true, headers);
		var message = await batch.EndBatchAsync();
		message.Headers.Accept.ToString().Should().Be("multipart/mixed", "the response should come back in the request's format");
		var body = await BodyAsync(message);
		System.Text.RegularExpressions.Regex.Matches(body, "x-custom: yes").Should().HaveCount(2);
	}

	[Fact]
	public async Task OperationHeadersAreWrittenIntoJsonRequests()
	{
		var (_, batch, writer) = Create(s => s.BatchFormat = BatchFormat.Json);
		var headers = new Dictionary<string, string> { ["x-custom"] = "yes" };
		await writer.CreateGetRequestAsync("Products(1)", false, headers);
		await writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "Chai" }, true, headers);
		var message = await batch.EndBatchAsync();
		message.Headers.Accept.ToString().Should().Be("application/json");
		using var document = JsonDocument.Parse(await BodyAsync(message));
		var requests = document.RootElement.GetProperty("requests");
		requests.GetArrayLength().Should().Be(2);
		foreach (var request in requests.EnumerateArray())
		{
			request.GetProperty("headers").GetProperty("x-custom").GetString().Should().Be("yes");
		}
	}

	[Fact]
	public async Task ContentIdsAreMappedThroughBatchEntries()
	{
		var (_, batch, writer) = Create();
		var typed = new Category { CategoryName = "Condiments" };
		var data = new Dictionary<string, object?> { ["CategoryName"] = "Condiments" };
		batch.BatchEntries[typed] = data;
		await writer.CreateInsertRequestAsync("Categories", "Categories", data, false);
		batch.GetContentId(data, null).Should().Be("1");
		batch.GetContentId(null, typed).Should().Be("1");
		batch.GetContentId(new Dictionary<string, object?>(), new object()).Should().BeNull();
	}

	[Fact]
	public async Task JsonBatchPayloadContainsRequestsArrayAndAtomicityGroup()
	{
		var (session, batch, writer) = Create(s => s.BatchFormat = BatchFormat.Json);
		await writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "Chai" }, true);
		await writer.CreateDeleteRequestAsync("Products", "Products(2)");
		await writer.CreateGetRequestAsync("Products", false);
		var message = await batch.EndBatchAsync();

		message.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
		message.Headers.GetValues("OData-Version").Should().Equal("4.01");
		var json = JsonDocument.Parse(await BodyAsync(message)).RootElement;
		var requests = json.GetProperty("requests");
		requests.GetArrayLength().Should().Be(3);
		requests[0].GetProperty("method").GetString().Should().Be("POST");
		requests[0].GetProperty("id").GetString().Should().Be("1");
		requests[0].GetProperty("url").GetString().Should().EndWith("Products");
		requests[0].GetProperty("body").GetProperty("ProductName").GetString().Should().Be("Chai");
		requests[0].GetProperty("atomicityGroup").GetString().Should().Be(requests[1].GetProperty("atomicityGroup").GetString());
		requests[2].GetProperty("method").GetString().Should().Be("GET");
		requests[2].TryGetProperty("atomicityGroup", out _).Should().BeFalse();
		_ = session;
	}

	[Fact]
	public async Task CreateBatchRequestRunsActionsAndMapsResponseIndexes()
	{
		var (session, batch, writer) = Create();
		var responseIndexes = new List<int>();
		var request = await batch.CreateBatchRequestAsync(
			[
				() => writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "Chai" }, true),
				() => Task.CompletedTask,
				() => writer.CreateGetRequestAsync("Products", false),
			],
			responseIndexes,
			new Dictionary<string, string> { ["X-Batch"] = "yes" });

		request.Should().NotBeNull();
		request!.Method.Should().Be("POST");
		request.CommandText.Should().Be("$batch");
		request.RequestMessage.RequestUri!.AbsoluteUri.Should().Be(session.BaseUri.AbsoluteUri + "$batch");
		request.RequestMessage.Headers.GetValues("X-Batch").Should().Equal("yes");
		responseIndexes.Should().Equal(0, -1, 1);
	}

	[Fact]
	public async Task CreateBatchRequestWithoutOperationsReturnsNull()
	{
		var (_, batch, _) = Create();
		var request = await batch.CreateBatchRequestAsync([() => Task.CompletedTask], [], null);
		request.Should().BeNull();
	}

	[Fact]
	public void ODataBatchCollectsActionsAndHeaders()
	{
		var batch = new ODataBatch(new Uri("http://localhost/odata/"));
		batch += client => Task.CompletedTask;
		batch += client => Task.CompletedTask;
		batch.WithHeader("X-One", "1").WithHeaders(new Dictionary<string, string> { ["X-Two"] = "2" });
		batch.Actions.Should().HaveCount(2);
		batch.Headers.Should().Equal(new Dictionary<string, string> { ["X-One"] = "1", ["X-Two"] = "2" });
		batch.Settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/odata/");
	}
}
