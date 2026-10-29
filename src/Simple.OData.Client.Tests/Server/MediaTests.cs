using System.Text;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Media resource and stream property scenarios against the in-process server.</summary>
public sealed class MediaTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private static async Task<string> ReadAsync(Stream stream)
	{
		using var reader = new StreamReader(stream);
		return await reader.ReadToEndAsync();
	}

	[Fact]
	public async Task GetMediaStream()
	{
		using var stream = await CreateClient().For("Pictures").Key(1).Media().GetStreamAsync();
		(await ReadAsync(stream)).Should().Be("image-bytes");
	}

	[Fact]
	public async Task GetMediaStreamAsString()
	{
		(await CreateClient().For("Pictures").Key(1).Media().GetStreamAsStringAsync()).Should().Be("image-bytes");
	}

	[Fact]
	public async Task GetMediaStreamAsArray()
	{
		(await CreateClient().For("Pictures").Key(1).Media().GetStreamAsArrayAsync()).Should().Equal(Encoding.UTF8.GetBytes("image-bytes"));
	}

	[Fact]
	public async Task GetMediaStreamTyped()
	{
		(await CreateClient().For<Picture>().Key(1).Media().GetStreamAsStringAsync()).Should().Be("image-bytes");
	}

	[Fact]
	public async Task SetMediaStreamFromString()
	{
		var client = CreateClient();
		await client.For("Pictures").Key(1).Media().SetStreamAsync("new-image");
		(await client.For("Pictures").Key(1).Media().GetStreamAsStringAsync()).Should().Be("new-image");
	}

	[Fact]
	public async Task SetMediaStreamFromBytes()
	{
		var client = CreateClient();
		await client.For("Pictures").Key(1).Media().SetStreamAsync(Encoding.UTF8.GetBytes("png-bytes"), "image/png");
		(await client.For("Pictures").Key(1).Media().GetStreamAsArrayAsync()).Should().Equal(Encoding.UTF8.GetBytes("png-bytes"));
	}

	[Fact]
	public async Task SetMediaStreamFromStream()
	{
		var client = CreateClient();
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes("streamed"));
		await client.For("Pictures").Key(1).Media().SetStreamAsync(stream, "image/png");
		(await client.For("Pictures").Key(1).Media().GetStreamAsStringAsync()).Should().Be("streamed");
	}

	[Fact]
	public async Task GetStreamProperty()
	{
		(await CreateClient().For("Pictures").Key(1).Media("Content").GetStreamAsStringAsync()).Should().Be("content-bytes");
	}

	[Fact]
	public async Task GetStreamPropertyTyped()
	{
		(await CreateClient().For<Picture>().Key(1).Media(x => x.Content!).GetStreamAsStringAsync()).Should().Be("content-bytes");
	}

	[Fact]
	public async Task GetStreamPropertyDynamic()
	{
		var x = ODataDynamic.Expression;
		string content = await CreateClient().For(x.Pictures).Key(1).Media(x.Content).GetStreamAsStringAsync();
		content.Should().Be("content-bytes");
	}

	[Fact]
	public async Task SetStreamProperty()
	{
		var client = CreateClient();
		await client.For("Pictures").Key(1).Media("Content").SetStreamAsync("new-content");
		(await client.For("Pictures").Key(1).Media("Content").GetStreamAsStringAsync()).Should().Be("new-content");
		(await client.For("Pictures").Key(1).Media().GetStreamAsStringAsync()).Should().Be("image-bytes", because: "the media resource is independent of the stream property");
	}

	[Fact]
	public async Task WithMediaEnrichesTheMediaResource()
	{
		var picture = await CreateClient().For("Pictures").Key(1).WithMedia("Media").FindEntryAsync();
		picture!["Name"].Should().Be("Logo");
		(await ReadAsync((Stream)picture["Media"]!)).Should().Be("image-bytes");
	}

	[Fact]
	public async Task WithMediaEnrichesTheMediaResourceTyped()
	{
		var picture = await CreateClient().For<Picture>().Key(1).WithMedia(x => x.Media!).FindEntryAsync();
		picture!.Media.Should().Equal(Encoding.UTF8.GetBytes("image-bytes"));
	}

	[Fact]
	public async Task WithMediaEnrichesAStreamProperty()
	{
		var picture = await CreateClient().For<Picture>().Key(1).WithMedia(x => x.Content!).FindEntryAsync();
		picture!.Content.Should().Be("content-bytes");
	}

	[Fact]
	public async Task WithMediaEnrichesEveryEntryOfAFeed()
	{
		var pictures = await CreateClient().For("Pictures").WithMedia("Media").FindEntriesAsync();
		pictures.Should().ContainSingle();
		(await ReadAsync((Stream)pictures[0]["Media"]!)).Should().Be("image-bytes");
	}

	[Fact]
	public async Task InsertMediaEntityThenSetItsStream()
	{
		var client = CreateClient();
		var picture = await client.For("Pictures").Set(new { Name = "Fresh" }).InsertEntryAsync();
		await client.For("Pictures").Key(picture!["Id"]).Media().SetStreamAsync("fresh-bytes");
		(await client.For("Pictures").Key(picture["Id"]).Media().GetStreamAsStringAsync()).Should().Be("fresh-bytes");
	}

	[Fact]
	public async Task MediaThroughClientApi()
	{
		var client = CreateClient();
		using (var stream = await client.GetMediaStreamAsync("Pictures(1)/$value"))
		{
			(await ReadAsync(stream)).Should().Be("image-bytes");
		}

		using var content = new MemoryStream(Encoding.UTF8.GetBytes("api-content"));
		await client.SetMediaStreamAsync("Pictures(1)/Content", content, "text/plain");
		using var updated = await client.GetMediaStreamAsync("Pictures(1)/Content");
		(await ReadAsync(updated)).Should().Be("api-content");
	}

	[Fact]
	public async Task MediaCommandText()
	{
		var client = CreateClient();
		(await client.For("Pictures").Key(1).Media().GetCommandTextAsync()).Should().Be("Pictures(1)/$value");
		(await client.For("Pictures").Key(1).Media("Content").GetCommandTextAsync()).Should().Be("Pictures(1)/Content");
	}
}
