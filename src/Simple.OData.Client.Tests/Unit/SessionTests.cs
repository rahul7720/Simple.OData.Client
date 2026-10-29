using System.Net;
using Simple.OData.Client.Metadata;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit;

public class SessionTests
{
	private const string CsdlJson = """
		{"$Version":"4.01","NS":{"P":{"$Kind":"EntityType","$Key":["Id"],"Id":{"$Type":"Edm.Int32"}},"C":{"$Kind":"EntityContainer","Ps":{"$Collection":true,"$Type":"NS.P"}}},"$EntityContainer":"NS.C"}
		""";

	[Fact]
	public void RequiresBaseUri()
	{
		var act = () => new Session(new ODataClientSettings());
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public async Task InitializesFromMetadataDocumentWithoutHttp()
	{
		var handler = new FakeHttpHandler();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", handler: handler);

		session.IsInitialized.Should().BeTrue();
		await session.InitializeAsync(CancellationToken.None);

		session.Metadata.GetEntityCollectionExactName("products").Should().Be("Products");
		session.Model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
		session.MetadataDocument.Should().Contain("NorthwindModel");
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public async Task InitializesFromMetadataModelWithoutHttp()
	{
		var handler = new FakeHttpHandler();
		var model = MetadataLoader.Parse(Resources.Read("TripPin.xml"));
		var settings = new ODataClientSettings(new HttpClient(handler)) { BaseUri = MetadataClientFactory.UniqueBaseUri(), MetadataModel = model };
		var session = new Session(settings);

		await session.InitializeAsync(CancellationToken.None);

		session.Model.Should().BeSameAs(model);
		session.Metadata.IsSingleton("Me").Should().BeTrue();
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public void MetadataIsNotAvailableBeforeInitialization()
	{
		var session = new Session(new ODataClientSettings(new HttpClient(new FakeHttpHandler())) { BaseUri = MetadataClientFactory.UniqueBaseUri() });

		session.IsInitialized.Should().BeFalse();
		var act = () => session.Metadata;
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public async Task ConcurrentInitializationSendsOneMetadataRequest()
	{
		var handler = new FakeHttpHandler().EnqueueMetadata("Northwind4.xml");
		var settings = new ODataClientSettings(new HttpClient(handler)) { BaseUri = MetadataClientFactory.UniqueBaseUri("svc/") };
		var session = new Session(settings);

		await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => session.InitializeAsync(CancellationToken.None).AsTask())));

		handler.Requests.Should().ContainSingle();
		var request = handler.Requests[0];
		request.Method.Should().Be(HttpMethod.Get);
		request.RequestUri!.AbsoluteUri.Should().Be(settings.BaseUri!.AbsoluteUri + "$metadata");
		request.Headers.Accept.ToString().Should().Contain("application/xml");
		request.Headers.GetValues("OData-MaxVersion").Should().ContainSingle("4.01");
		request.Headers.GetValues("OData-Version").Should().ContainSingle("4.0");
		session.Metadata.GetEntityCollectionExactName("Products").Should().Be("Products");
	}

	[Fact]
	public async Task CancelledMetadataDownloadLeavesCacheEmpty()
	{
		var baseUri = MetadataClientFactory.UniqueBaseUri("svc/");
		using var cts = new CancellationTokenSource();
		var blockingHandler = new FakeHttpHandler().Enqueue(async (_, ct) =>
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, ct);
			return new HttpResponseMessage(HttpStatusCode.OK);
		});
		var blockedSession = new Session(new ODataClientSettings(new HttpClient(blockingHandler)) { BaseUri = baseUri });

		var initialize = blockedSession.InitializeAsync(cts.Token).AsTask();
		cts.CancelAfter(50);
		var act = () => initialize;
		await act.Should().ThrowAsync<OperationCanceledException>();
		blockedSession.IsInitialized.Should().BeFalse();

		var handler = new FakeHttpHandler().EnqueueMetadata("Northwind4.xml");
		var session = new Session(new ODataClientSettings(new HttpClient(handler)) { BaseUri = baseUri });
		await session.InitializeAsync(CancellationToken.None);

		handler.Requests.Should().ContainSingle();
		session.Metadata.GetEntityCollectionExactName("Products").Should().Be("Products");
	}

	[Fact]
	public async Task MetadataRequestUsesSchemaVersionAndJsonAccept()
	{
		var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, CsdlJson, "application/json");
		var settings = new ODataClientSettings(new HttpClient(handler))
		{
			BaseUri = MetadataClientFactory.UniqueBaseUri("svc/"),
			MetadataFormat = MetadataFormat.Json,
			SchemaVersion = "1.0",
		};
		var session = new Session(settings);

		await session.InitializeAsync(CancellationToken.None);

		var request = handler.Requests.Should().ContainSingle().Subject;
		request.RequestUri!.AbsoluteUri.Should().Be(settings.BaseUri!.AbsoluteUri + "$metadata?$schemaversion=1.0");
		request.Headers.Accept.ToString().Should().Be("application/json");
		session.Metadata.GetEntityCollectionExactName("Ps").Should().Be("Ps");
	}

	[Fact]
	public async Task FailedMetadataRequestThrowsAndIsRetried()
	{
		var baseUri = MetadataClientFactory.UniqueBaseUri("svc/");
		var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.InternalServerError, "boom", "text/plain").EnqueueMetadata("Northwind4.xml");
		var session = new Session(new ODataClientSettings(new HttpClient(handler)) { BaseUri = baseUri });

		var act = () => session.InitializeAsync(CancellationToken.None).AsTask();
		await act.Should().ThrowAsync<HttpRequestException>();

		await session.InitializeAsync(CancellationToken.None);
		handler.Requests.Should().HaveCount(2);
		session.IsInitialized.Should().BeTrue();
	}

	[Fact]
	public async Task BeforeRequestAndAfterResponseHooksRunForMetadataRequest()
	{
		var handler = new FakeHttpHandler().EnqueueMetadata("Northwind4.xml");
		var hooks = new List<string>();
		var settings = new ODataClientSettings(new HttpClient(handler))
		{
			BaseUri = MetadataClientFactory.UniqueBaseUri("svc/"),
			BeforeRequest = r => { r.Headers.Add("x-test", "1"); hooks.Add("before"); },
			BeforeRequestAsync = (_, _) => { hooks.Add("beforeAsync"); return Task.CompletedTask; },
			AfterResponse = _ => hooks.Add("after"),
			AfterResponseAsync = (_, _) => { hooks.Add("afterAsync"); return Task.CompletedTask; },
		};
		var session = new Session(settings);

		await session.InitializeAsync(CancellationToken.None);

		hooks.Should().Equal("before", "beforeAsync", "after", "afterAsync");
		handler.Requests[0].Headers.GetValues("x-test").Should().ContainSingle("1");
	}

	[Fact]
	public void TraceInvokesOnTrace()
	{
		var messages = new List<string>();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s => s.OnTrace = (f, a) => messages.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, f, a)));

		session.Trace("{0} request: {1}", "GET", "Products");

		messages.Should().ContainSingle("GET request: Products");
	}

	[Fact]
	public void ClearMetadataCacheForcesReinitialization()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml");
		session.IsInitialized.Should().BeTrue();

		session.ClearMetadataCache();

		session.IsInitialized.Should().BeFalse();
	}
}

public class HttpConnectionTests
{
	[Fact]
	public void ExternalHttpClientIsUsedAsIs()
	{
		var httpClient = new HttpClient(new FakeHttpHandler());
		var settings = new ODataClientSettings(httpClient) { BaseUri = new Uri("http://localhost/") };

		using var connection = new Http.HttpConnection(settings);

		connection.HttpClient.Should().BeSameAs(httpClient);
	}

	[Fact]
	public void RequestTimeoutIsApplied()
	{
		var settings = new ODataClientSettings(new Uri("http://localhost/")) { RequestTimeout = TimeSpan.FromSeconds(5) };

		using var connection = new Http.HttpConnection(settings);

		connection.HttpClient.Timeout.Should().Be(TimeSpan.FromSeconds(5));
		connection.HttpClient.BaseAddress.Should().BeNull();
	}

	[Fact]
	public async Task OnCreateMessageHandlerIsUsed()
	{
		var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, "{}");
		var settings = new ODataClientSettings(new Uri("http://localhost/")) { OnCreateMessageHandler = () => handler };

		using var connection = new Http.HttpConnection(settings);
		using var response = await connection.HttpClient.GetAsync(new Uri("http://localhost/x"));

		handler.Requests.Should().ContainSingle();
	}

	[Fact]
	public void ConfigureHandlerReceivesSocketsHttpHandler()
	{
		SocketsHttpHandler? configured = null;
		var settings = new ODataClientSettings(new Uri("http://localhost/")) { ConfigureHandler = h => configured = h };

		using var connection = new Http.HttpConnection(settings);

		configured.Should().NotBeNull();
		configured!.AutomaticDecompression.Should().Be(DecompressionMethods.All);
	}
}
