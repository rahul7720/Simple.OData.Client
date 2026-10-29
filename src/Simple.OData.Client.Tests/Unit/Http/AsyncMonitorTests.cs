using System.Net;
using Simple.OData.Client.Http;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Http;

public sealed class AsyncMonitorTests
{
	[Fact]
	public async Task PollsUntilCompleteAndUnwrapsApplicationHttp()
	{
		var time = new ManualTimeProvider();
		var handler = new FakeHttpHandler();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s =>
		{
			s.RespondAsync = true;
			s.TimeProvider = time;
			s.AsyncPollingInterval = TimeSpan.FromMilliseconds(250);
		}, handler);
		var monitor = new Uri(session.BaseUri, "monitor/1");
		var wrapped = string.Join("\r\n",
			"HTTP/1.1 200 OK",
			"Content-Type: application/json;odata.metadata=minimal",
			"OData-Version: 4.0",
			"",
			$$$"""{"@odata.context":"{{{session.BaseUri}}}$metadata#Products","value":[{"ProductID":1,"ProductName":"Chai"}]}""");
		handler.Enqueue(HttpStatusCode.Accepted, headers: new Dictionary<string, string> { ["Location"] = monitor.AbsoluteUri });
		handler.Enqueue(HttpStatusCode.Accepted, headers: new Dictionary<string, string> { ["Location"] = monitor.AbsoluteUri, ["Retry-After"] = "1" });
		handler.Enqueue(HttpStatusCode.OK, wrapped, "application/http");

		var runner = new RequestRunner(session);
		var writer = new RequestWriter(session, null);
		var response = await runner.ExecuteRequestAsync(await writer.CreateGetRequestAsync("Products", false), CancellationToken.None);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
		handler.Requests.Should().HaveCount(3);
		handler.Requests[1].RequestUri.Should().Be(monitor);
		handler.Requests[2].Method.Should().Be(HttpMethod.Get);
		time.Delays.Should().Equal(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1));

		var result = await new ResponseReader(session).ReadAsync(response, null, CancellationToken.None);
		result.AsEntries(false).Should().ContainSingle().Which["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task ReturnsCompletedResponseAsIsWhenNotWrapped()
	{
		var handler = new FakeHttpHandler();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s => s.TimeProvider = new ManualTimeProvider(), handler);
		var monitor = new Uri(session.BaseUri, "monitor/2");
		handler.Enqueue(HttpStatusCode.Accepted, headers: new Dictionary<string, string> { ["Location"] = monitor.AbsoluteUri });
		handler.Enqueue(HttpStatusCode.OK, """{"value":[]}""");

		var response = await new RequestRunner(session).ExecuteRequestAsync(new ODataRequest("GET", session, "Products") { ResultRequired = true }, CancellationToken.None);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content!.ReadAsStringAsync()).Should().Be("""{"value":[]}""");
	}

	[Fact]
	public async Task AcceptedWithoutLocationIsReturned()
	{
		var handler = new FakeHttpHandler();
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", null, handler);
		handler.Enqueue(HttpStatusCode.Accepted);
		var response = await new RequestRunner(session).ExecuteRequestAsync(new ODataRequest("POST", session, "Products/NS.Reset"), CancellationToken.None);
		response.StatusCode.Should().Be(HttpStatusCode.Accepted);
		handler.Requests.Should().HaveCount(1);
	}
}
