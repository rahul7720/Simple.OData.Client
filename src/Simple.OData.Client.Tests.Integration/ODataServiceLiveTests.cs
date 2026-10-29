using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests;

/// <summary>Read-write scenarios against a per-session copy of the public V4 OData reference service.</summary>
public sealed class ODataServiceLiveTests : LiveTestBase
{
	private static int _nextId = (int)(DateTime.UtcNow.Ticks % 100_000) + 10_000;

	private static Task<ODataClient> ClientAsync() => CreateClientAsync(LiveServices.ODataReadWrite);

	private static Entry CreateProduct(string name) => new()
	{
		["ID"] = Interlocked.Increment(ref _nextId),
		["Name"] = name,
		["Description"] = name + " description",
		["ReleaseDate"] = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
		["Rating"] = 1,
		["Price"] = 18d,
	};

	[LiveFact]
	public async Task InsertUpdateAndDelete()
	{
		var client = await ClientAsync();
		var product = await client.For("Products").Set(CreateProduct("Live insert")).InsertEntryAsync();
		product!["Name"].Should().Be("Live insert");
		var key = product["ID"];

		await client.For("Products").Key(key).Set(new { Price = 22d }).UpdateEntryAsync();
		(await client.For("Products").Key(key).FindEntryAsync())!["Price"].Should().Be(22d);

		await client.For("Products").Key(key).DeleteEntryAsync();
		(await client.For("Products").Key(key).FindEntryAsync()).Should().BeNull();
	}

	[LiveFact]
	public async Task InsertWithLinkToCategory()
	{
		var client = await ClientAsync();
		var category = await client.For("Categories").Set(new Entry { ["ID"] = Interlocked.Increment(ref _nextId), ["Name"] = "Live category" }).InsertEntryAsync();
		var product = await client.For("Products").Set(CreateProduct("Live linked")).InsertEntryAsync();
		await client.For("Products").Key(product!["ID"]).LinkEntryAsync("Categories", category!);

		var linked = await client.For("Products").Key(product["ID"]).Expand("Categories").FindEntryAsync();
		((IEnumerable<object?>)linked!["Categories"]!).Should().ContainSingle();
	}

	[LiveFact]
	public async Task BatchInsertsAndReadsBack()
	{
		var client = await ClientAsync();
		var batch = new ODataBatch(client);
		batch += c => c.For("Products").Set(CreateProduct("Live batch 1")).InsertEntryAsync(false);
		batch += c => c.For("Products").Set(CreateProduct("Live batch 2")).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		(await client.For("Products").Filter("Name eq 'Live batch 1'").FindEntryAsync()).Should().NotBeNull();
		(await client.For("Products").Filter("Name eq 'Live batch 2'").FindEntryAsync()).Should().NotBeNull();
	}

	[LiveFact]
	public async Task BatchWithResultsAndUpdate()
	{
		var client = await ClientAsync();
		IDictionary<string, object?>? inserted = null;
		var batch = new ODataBatch(client);
		batch += async c => inserted = await c.For("Products").Set(CreateProduct("Live batch result")).InsertEntryAsync();
		await batch.ExecuteAsync();
		inserted!["Name"].Should().Be("Live batch result");

		batch = new ODataBatch(client);
		batch += c => c.For("Products").Key(inserted["ID"]).Set(new { Price = 33d }).UpdateEntryAsync(false);
		await batch.ExecuteAsync();
		(await client.For("Products").Key(inserted["ID"]).FindEntryAsync())!["Price"].Should().Be(33d);
	}
}
