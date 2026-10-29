using System.Net;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class ErrorReaderTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4.xml");

	[Fact]
	public async Task ParsesODataError()
	{
		const string body = """{"error":{"code":"400","message":"Bad","target":"ProductName","details":[{"code":"1","message":"d","target":"t"}],"innererror":{"message":"inner","type":"System.Exception"}}}""";
		var error = await new ResponseReader(Session).TryReadErrorAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.BadRequest, body, "application/json"), CancellationToken.None);
		error.Should().NotBeNull();
		error!.Code.Should().Be("400");
		error.Message.Should().Be("Bad");
		error.Target.Should().Be("ProductName");
		error.Details.Should().ContainSingle();
		error.Details[0].Code.Should().Be("1");
		error.Details[0].Message.Should().Be("d");
		error.Details[0].Target.Should().Be("t");
		error.InnerError.Should().Contain("inner");
	}

	[Fact]
	public async Task ReturnsNullForNonErrorBodies()
	{
		var reader = new ResponseReader(Session);
		(await reader.TryReadErrorAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.InternalServerError, "boom", "text/plain"), CancellationToken.None)).Should().BeNull();
		(await reader.TryReadErrorAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.InternalServerError, "<html>boom</html>", "text/html"), CancellationToken.None)).Should().BeNull();
		(await reader.TryReadErrorAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.InternalServerError), CancellationToken.None)).Should().BeNull();
		(await reader.TryReadErrorAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.BadRequest, """{"value":1}""", "application/json"), CancellationToken.None)).Should().BeNull();
	}

	[Fact]
	public async Task ReadAsyncExposesErrorPayload()
	{
		const string body = """{"error":{"code":"404","message":"Missing"}}""";
		var response = await new ResponseReader(Session).ReadAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.NotFound, body, "application/json"), null, CancellationToken.None);
		response.StatusCode.Should().Be(404);
		response.Error!.Code.Should().Be("404");
		response.Error.Message.Should().Be("Missing");
		response.Feed.Should().BeNull();
	}
}
