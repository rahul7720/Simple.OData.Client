using System.Net;
using Simple.OData.Client.Http;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Http;

public sealed class RequestRunnerTests
{
	private static (Session Session, FakeHttpHandler Handler, RequestWriter Writer, RequestRunner Runner) Create(Action<ODataClientSettings>? configure = null)
	{
		var handler = new FakeHttpHandler();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", configure, handler);
		return (session, handler, new RequestWriter(session, null), new RequestRunner(session));
	}

	private const string Feed = """{"value":[]}""";

	[Fact]
	public async Task SendsODataVersionAndAcceptHeaders()
	{
		var (_, handler, writer, runner) = Create();
		handler.Enqueue(HttpStatusCode.OK, Feed);
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		var sent = handler.LastRequest!;
		sent.Headers.GetValues("OData-Version").Should().Equal("4.0");
		sent.Headers.GetValues("OData-MaxVersion").Should().Equal("4.01");
		sent.Headers.Accept.ToString().Should().Be("application/json; odata.metadata=minimal");

		(_, handler, writer, runner) = Create(s => s.ProtocolVersion = ODataProtocolVersion.V401);
		handler.Enqueue(HttpStatusCode.OK, Feed);
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		handler.LastRequest!.Headers.GetValues("OData-Version").Should().Equal("4.01");

		(_, handler, writer, runner) = Create(s => { s.MetadataLevel = ODataMetadataLevel.Full; s.Ieee754Compatible = true; });
		handler.Enqueue(HttpStatusCode.OK, Feed);
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		handler.LastRequest!.Headers.Accept.ToString().Should().Be("application/json; odata.metadata=full; IEEE754Compatible=true");

		(_, handler, writer, runner) = Create();
		handler.Enqueue(HttpStatusCode.OK, "42", "text/plain");
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products/$count", true), CancellationToken.None);
		handler.LastRequest!.Headers.Accept.Should().BeEmpty();
	}

	[Fact]
	public async Task ComposesPreferHeader()
	{
		var (_, handler, writer, runner) = Create(s =>
		{
			s.MaxPageSize = 10;
			s.TrackChanges = true;
			s.OmitNullValues = true;
			s.IncludeAnnotations = "*";
		});
		handler.Enqueue(HttpStatusCode.Created, """{"ProductID":1}""");
		var request = await writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "p" }, true);
		await runner.ExecuteRequestAsync(request, CancellationToken.None);
		handler.LastRequest!.Headers.GetValues("Prefer").Should().Equal("return=representation, odata.maxpagesize=10, odata.track-changes, omit-values=nulls, odata.include-annotations=\"*\"");

		(_, handler, writer, runner) = Create(s => s.RespondAsync = true);
		handler.Enqueue(HttpStatusCode.OK, Feed);
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		handler.LastRequest!.Headers.GetValues("Prefer").Should().Equal("respond-async");

		(_, handler, writer, runner) = Create();
		handler.Enqueue(HttpStatusCode.OK, Feed);
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		handler.LastRequest!.Headers.Contains("Prefer").Should().BeFalse();
	}

	[Fact]
	public void RequestPreferencesWinOverSettingsAndContinueOnErrorAppliesToBatch()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s => { s.MaxPageSize = 10; s.ContinueOnError = true; });
		var request = new ODataRequest("GET", session, "Products");
		request.AddPreference("odata.maxpagesize=5");
		PreferHeader.Compose(session.Settings, request).Should().Be("odata.maxpagesize=5");

		var batch = new ODataRequest("POST", session, "$batch");
		PreferHeader.Compose(session.Settings, batch).Should().Be("odata.maxpagesize=10, odata.continue-on-error");
	}

	[Fact]
	public async Task IfMatchFromRequestOverridesStar()
	{
		var (session, handler, _, runner) = Create();
		handler.Fallback = (_, _) => Task.FromResult(FakeHttpHandler.CreateResponse(HttpStatusCode.NoContent));

		await runner.ExecuteRequestAsync(new ODataRequest("PATCH", session, "Products(1)") { CheckOptimisticConcurrency = true }, CancellationToken.None);
		handler.LastRequest!.Headers.IfMatch.ToString().Should().Be("*");

		await runner.ExecuteRequestAsync(new ODataRequest("PATCH", session, "Products(1)") { CheckOptimisticConcurrency = true, IfMatch = "W/\"1\"" }, CancellationToken.None);
		handler.LastRequest!.Headers.IfMatch.ToString().Should().Be("W/\"1\"");

		await runner.ExecuteRequestAsync(new ODataRequest("DELETE", session, "Products(1)") { CheckOptimisticConcurrency = true }, CancellationToken.None);
		handler.LastRequest!.Headers.IfMatch.ToString().Should().Be("*");

		await runner.ExecuteRequestAsync(new ODataRequest("GET", session, "Products(1)") { CheckOptimisticConcurrency = true, IfNoneMatch = "W/\"2\"" }, CancellationToken.None);
		handler.LastRequest!.Headers.IfMatch.Should().BeEmpty();
		handler.LastRequest.Headers.IfNoneMatch.ToString().Should().Be("W/\"2\"");
	}

	[Fact]
	public async Task ThrowsWebRequestExceptionWithParsedError()
	{
		var (_, handler, writer, runner) = Create(s => s.WebRequestExceptionMessageSource = WebRequestExceptionMessageSource.Both);
		handler.Enqueue(HttpStatusCode.BadRequest, """{"error":{"code":"400","message":"Bad product"}}""", "application/json");
		var request = await writer.CreateGetRequestAsync("Products", false);
		var act = () => runner.ExecuteRequestAsync(request, CancellationToken.None);
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.BadRequest);
		exception.Error!.Code.Should().Be("400");
		exception.Error.Message.Should().Be("Bad product");
		exception.Response.Should().Contain("Bad product");
		exception.RequestUri!.AbsoluteUri.Should().EndWith("/Products");
		exception.Message.Should().Contain("400").And.Contain("Bad product");
	}

	[Fact]
	public async Task HonoursMaxConcurrentRequests()
	{
		var (_, handler, writer, runner) = Create(s => s.MaxConcurrentRequests = 2);
		var inFlight = 0;
		var maxInFlight = 0;
		handler.Fallback = async (_, ct) =>
		{
			var current = Interlocked.Increment(ref inFlight);
			InterlockedMax(ref maxInFlight, current);
			await Task.Delay(20, ct);
			Interlocked.Decrement(ref inFlight);
			return FakeHttpHandler.CreateResponse(HttpStatusCode.OK, Feed);
		};

		var tasks = Enumerable.Range(0, 6).Select(async _ => await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None));
		await Task.WhenAll(tasks);
		maxInFlight.Should().BeLessThanOrEqualTo(2).And.BeGreaterThan(0);
		handler.Requests.Should().HaveCount(6);
	}

	[Fact]
	public async Task RunsHooksAndTracing()
	{
		var traces = new List<string>();
		var afterSeen = new List<HttpStatusCode>();
		var (_, handler, writer, runner) = Create(s =>
		{
			s.BeforeRequest = r => r.Headers.TryAddWithoutValidation("X-Before", "1");
			s.BeforeRequestAsync = (r, _) => { r.Headers.TryAddWithoutValidation("X-Before-Async", "1"); return Task.CompletedTask; };
			s.AfterResponse = r => afterSeen.Add(r.StatusCode);
			s.AfterResponseAsync = (r, _) => { afterSeen.Add(r.StatusCode); return Task.CompletedTask; };
			s.OnTrace = (format, args) => traces.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
			s.TraceFilter = ODataTrace.All;
		});
		handler.Enqueue(HttpStatusCode.Created, """{"ProductID":1}""");
		await runner.ExecuteRequestAsync(await writer.CreateInsertRequestAsync("Products", "Products", new Dictionary<string, object?> { ["ProductName"] = "p" }, true), CancellationToken.None);
		handler.LastRequest!.Headers.Contains("X-Before").Should().BeTrue();
		handler.LastRequest.Headers.Contains("X-Before-Async").Should().BeTrue();
		afterSeen.Should().Equal(HttpStatusCode.Created, HttpStatusCode.Created);
		traces.Should().Contain(x => x.Contains("POST") && x.Contains("/Products"));
		traces.Should().Contain(x => x.Contains("\"ProductName\":\"p\""));
		traces.Should().Contain(x => x.Contains("\"ProductID\":1"));
	}

	[Fact]
	public async Task UsesRequestExecutorWhenConfigured()
	{
		HttpRequestMessage? executed = null;
		var (_, handler, writer, runner) = Create(s => s.RequestExecutor = request =>
		{
			executed = request;
			return Task.FromResult(FakeHttpHandler.CreateResponse(HttpStatusCode.OK, Feed));
		});
		await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);
		executed.Should().NotBeNull();
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public void WebRequestExceptionMessageFollowsSource()
	{
		WebRequestException.CreateFromStatusCode(HttpStatusCode.NotFound, WebRequestExceptionMessageSource.ReasonPhrase, "body").Message.Should().Be("NotFound");
		WebRequestException.CreateFromStatusCode(HttpStatusCode.NotFound, WebRequestExceptionMessageSource.ResponseContent, "body").Message.Should().Be("body");
		WebRequestException.CreateFromStatusCode(HttpStatusCode.NotFound, WebRequestExceptionMessageSource.Both, "body").Message.Should().Contain("404").And.Contain("body");
		var withError = WebRequestException.CreateFromStatusCode(HttpStatusCode.NotFound, WebRequestExceptionMessageSource.ReasonPhrase, null, new ODataErrorDetails { Code = "x", Message = "m" });
		withError.Error!.Message.Should().Be("m");
	}

	private static void InterlockedMax(ref int location, int value)
	{
		int current;
		while ((current = location) < value && Interlocked.CompareExchange(ref location, value, current) != current)
		{
		}
	}
}
