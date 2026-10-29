using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Change tracking: <c>odata.track-changes</c>, delta links and delta payloads.</summary>
public sealed class DeltaTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task TrackChangesReturnsADeltaLink()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().For("Products").TrackChanges().FindEntriesAsync(annotations);
		products.Should().HaveCount(Seed.Products);
		annotations.DeltaLink.Should().NotBeNull();
		annotations.DeltaLink!.AbsoluteUri.Should().Contain("Changes()").And.Contain("$deltatoken=1");
		Request("GET", "/Products").Headers["Prefer"].Should().Contain("odata.track-changes");
	}

	[Fact]
	public async Task TrackChangesTyped()
	{
		var annotations = new ODataFeedAnnotations();
		await CreateClient().For<Product>().TrackChanges().FindEntriesAsync(annotations);
		annotations.DeltaLink.Should().NotBeNull();
	}

	[Fact]
	public async Task FindDeltaReadsChangedAndDeletedEntries()
	{
		var client = CreateClient();
		var annotations = new ODataFeedAnnotations();
		await client.For("Products").TrackChanges().FindEntriesAsync(annotations);

		var delta = await client.FindDeltaAsync(annotations.DeltaLink!);
		delta.Changed.Should().ContainSingle().Which["ProductName"].Should().Be("Chai (changed)");
		delta.Changed[0]["ProductID"].Should().Be(1);
		delta.Deleted.Should().ContainSingle().Which.Id.Should().EndWith("Products(2)");
		delta.AddedLinks.Should().BeEmpty();
		delta.DeletedLinks.Should().BeEmpty();
		delta.DeltaLink.Should().NotBeNull();
		delta.DeltaLink!.Query.Should().Contain("$deltatoken=2");
	}

	[Fact]
	public async Task DeltaLinkCanBeFollowedAgain()
	{
		var client = CreateClient();
		var annotations = new ODataFeedAnnotations();
		await client.For("Products").TrackChanges().FindEntriesAsync(annotations);
		var first = await client.FindDeltaAsync(annotations.DeltaLink!);
		var second = await client.FindDeltaAsync(first.DeltaLink!);
		second.Changed.Should().ContainSingle();
		Requests.Count(x => x.Path.Contains("Changes()", StringComparison.Ordinal)).Should().Be(2);
	}
}
