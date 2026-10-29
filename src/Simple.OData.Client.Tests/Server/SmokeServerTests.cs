using System.Net;
using System.Text.Json;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class SmokeServerTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task MetadataIsServed()
	{
		using var http = CreateHttpClient();
		using var response = await http.GetAsync(new Uri("$metadata", UriKind.Relative));
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await response.Content.ReadAsStringAsync()).Should().Contain("EntityContainer");
	}

	[Fact]
	public async Task ProductsFeedHasTwentyTwoEntries()
	{
		using var http = CreateHttpClient();
		using var response = await http.GetAsync(new Uri("Products", UriKind.Relative));
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		document.RootElement.GetProperty("value").GetArrayLength().Should().Be(22);
	}

	[Fact]
	public async Task ClientReadsMetadataAndProducts()
	{
		using var client = CreateClient();
		var products = await client.FindEntriesAsync("Products");
		products.Should().HaveCount(22);
		products[0]["ProductName"].Should().Be("Chai");
	}
}
