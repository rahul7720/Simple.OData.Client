using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Loading the service metadata as CSDL JSON.</summary>
public sealed class MetadataJsonTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task JsonMetadataIsRequestedParsedAndUsable()
	{
		ODataClient.ClearMetadataCache();
		try
		{
			var client = CreateClient(s => s.MetadataFormat = MetadataFormat.Json);
			var model = await client.GetMetadataAsync();
			model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
			model.FindDeclaredType("NorthwindModel.Product").Should().NotBeNull();

			var metadataRequest = Request("GET", "/$metadata");
			metadataRequest.Headers["Accept"].Should().Contain("application/json");
			(await client.GetMetadataDocumentAsync()).TrimStart().Should().StartWith("{");

			var product = await client.For<Product>().Filter(x => x.ProductName == "Chai").FindEntryAsync();
			product!.ProductID.Should().Be(1);
			Request("GET", "/Products(ProductName='Chai')").Should().NotBeNull("alternate keys survive the JSON CSDL round trip");
		}
		finally
		{
			ODataClient.ClearMetadataCache();
		}
	}

	[Fact]
	public async Task XmlMetadataIsTheDefault()
	{
		ODataClient.ClearMetadataCache();
		var client = CreateClient();
		(await client.GetMetadataDocumentAsync()).TrimStart().Should().StartWith("<");
		Request("GET", "/$metadata").Headers["Accept"].Should().Contain("application/xml");
	}
}
