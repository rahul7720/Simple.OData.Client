using BenchmarkDotNet.Attributes;

namespace Simple.OData.Client.Benchmarks;

/// <summary>Reading a canned 20-person TripPin feed (with expanded trips and friends) into dictionaries, typed objects and dynamic entries.</summary>
[MemoryDiagnoser]
public class FeedMaterializationBenchmarks
{
	private ODataClient _client = null!;
	private IBoundClient<ODataEntry> _dynamicCommand = null!;

	[GlobalSetup]
	public async Task Setup()
	{
		_client = Canned.CreateClient();
		await _client.GetMetadataAsync();
		var x = ODataDynamic.Expression;
		_dynamicCommand = _client.For(x.People);
	}

	[Benchmark(Baseline = true)]
	public Task<IReadOnlyList<IDictionary<string, object?>>> Untyped() => _client.For("People").FindEntriesAsync();

	[Benchmark]
	public Task<IReadOnlyList<Person>> Typed() => _client.For<Person>("People").FindEntriesAsync();

	[Benchmark]
	public Task<IReadOnlyList<ODataEntry>> Dynamic() => _dynamicCommand.FindEntriesAsync();

	[Benchmark]
	public async Task<long?> TypedWithFeedAnnotations()
	{
		var annotations = new ODataFeedAnnotations();
		await _client.For<Person>("People").FindEntriesAsync(annotations);
		return annotations.Count;
	}

	[Benchmark]
	public Task<IReadOnlyList<IDictionary<string, object?>>> UntypedThroughClientApi() => _client.FindEntriesAsync("People");
}
