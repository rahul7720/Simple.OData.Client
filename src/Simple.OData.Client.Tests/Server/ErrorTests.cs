using System.Net;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Error responses surface as <see cref="WebRequestException"/> with the OData error details.</summary>
public sealed class ErrorTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task InvalidFilterSurfacesTheODataError()
	{
		var client = CreateClient();
		var act = () => client.For("Products").Filter("Foo eq 1").FindEntriesAsync();
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.BadRequest);
		exception.Error.Should().NotBeNull();
		exception.Error!.Message.Should().Contain("Foo");
		exception.Error.Code.Should().NotBeNull();
		exception.Response.Should().Contain("Foo");
		exception.RequestUri!.Query.Should().Contain("$filter=");
		exception.Message.Should().Be("Bad Request", "the reason phrase is the default message source");
	}

	[Fact]
	public async Task ResponseContentCanBeTheExceptionMessage()
	{
		var client = CreateClient(s => s.WebRequestExceptionMessageSource = WebRequestExceptionMessageSource.ResponseContent);
		var act = () => client.For<Product>().Filter("Foo eq 1").FindEntriesAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Message.Should().Contain("Foo");
	}

	[Fact]
	public async Task UnknownPropertiesInDynamicFiltersFailBeforeTheRequestIsSent()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		var act = () => (Task)client.For(x.Products).Filter(x.Foo == 1).FindEntriesAsync();
		(await act.Should().ThrowAsync<UnresolvableObjectException>()).Which.Message.Should().Contain("Foo");
		Requests.Should().NotContain(r => r.Path.EndsWith("/Products", StringComparison.Ordinal), "the filter is validated against the metadata first");
	}

	[Fact]
	public async Task NotFoundThrowsUnlessIgnored()
	{
		var strict = CreateClient(s => s.IgnoreResourceNotFoundException = false);
		var act = () => strict.For("Products").Key(99999).FindEntryAsync();
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.NotFound);
		exception.RequestUri!.AbsolutePath.Should().EndWith("/Products(99999)");

		(await CreateClient().For("Products").Key(99999).FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task NotFoundOnUpdateAndDelete()
	{
		var client = CreateClient(s => s.IgnoreResourceNotFoundException = false);
		var update = () => client.For("Products").Key(99999).Set(new { UnitPrice = 1m }).UpdateEntryAsync();
		(await update.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
		var delete = () => client.For("Products").Key(99999).DeleteEntryAsync();
		(await delete.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task PreconditionFailedCarriesTheStatus()
	{
		var client = CreateClient();
		var act = () => client.For("Products").Key(1).WithIfMatch("W/\"stale\"").Set(new { UnitPrice = 1m }).UpdateEntryAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(HttpStatusCode.PreconditionFailed);
	}
}
