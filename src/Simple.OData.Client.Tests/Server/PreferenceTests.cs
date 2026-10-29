using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary><c>Prefer</c> header driven behaviour and metadata levels.</summary>
public sealed class PreferenceTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task OmitNullValuesDropsNullPropertiesFromThePayload()
	{
		var client = CreateClient(s => s.OmitNullValues = true);
		var product = await client.For("Products").Key(1).FindEntryAsync();
		product.Should().NotContainKey("Rating", "the service omitted the null value");
		product!["ProductName"].Should().Be("Chai");
		Request("GET", "/Products(1)").Headers["Prefer"].Should().Contain("omit-values=nulls");
	}

	[Fact]
	public async Task OmitNullValuesRoundTripsAnEntryWithNulls()
	{
		var client = CreateClient(s => s.OmitNullValues = true);
		var inserted = await client.For("Products").Set(new { ProductName = "Nulls", UnitPrice = 1m }).InsertEntryAsync();
		inserted.Should().NotContainKey("EnglishName");
		var product = await client.For<ProductWithRating>("Products").Key(inserted!["ProductID"]).FindEntryAsync();
		product!.ProductName.Should().Be("Nulls");
		product.Rating.Should().BeNull();
	}

	[Fact]
	public async Task IncludeAnnotationsExposesEntryAnnotations()
	{
		var client = CreateClient(s =>
		{
			s.IncludeAnnotations = "*";
			s.IncludeAnnotationsInResults = true;
		});
		var product = await client.For("Products").Key(1).FindEntryAsync();
		product.Should().ContainKey("__annotations");
		var annotations = product!["__annotations"].Should().BeOfType<ODataEntryAnnotations>().Which;
		annotations.ETag.Should().NotBeNullOrEmpty();
		Request("GET", "/Products(1)").Headers["Prefer"].Should().Contain("odata.include-annotations=\"*\"");
	}

	[Fact]
	public async Task FullMetadataExposesTypeAndId()
	{
		var client = CreateClient(s =>
		{
			s.MetadataLevel = ODataMetadataLevel.Full;
			s.IncludeAnnotationsInResults = true;
		});
		var product = await client.For("Products").Key(1).FindEntryAsync();
		var annotations = (ODataEntryAnnotations)product!["__annotations"]!;
		annotations.TypeName.Should().Be("NorthwindModel.Product");
		annotations.Id.Should().EndWith("Products(1)");
		Request("GET", "/Products(1)").Headers["Accept"].Should().Contain("odata.metadata=full");
	}

	[Fact]
	public async Task AnnotationsAreLeftOutByDefault()
	{
		var product = await CreateClient().For("Products").Key(1).FindEntryAsync();
		product.Should().NotContainKey("__annotations");
	}

	[Fact]
	public async Task ReturnMinimalOnInsertStillYieldsTheEntity()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Minimal", UnitPrice = 1m }).InsertEntryAsync(resultRequired: false);
		product.Should().BeNull("no result was requested");
		Request("POST", "/Products").Headers["Prefer"].Should().Contain("return=minimal");
	}
}
