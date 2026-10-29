using BenchmarkDotNet.Attributes;

namespace Simple.OData.Client.Benchmarks;

/// <summary>Turning fluent commands into request URLs (metadata is preloaded, nothing is sent).</summary>
[MemoryDiagnoser]
public class UriBuildingBenchmarks
{
	private ODataClient _client = null!;

	[GlobalSetup]
	public async Task Setup()
	{
		_client = new ODataClient(new ODataClientSettings { BaseUri = Canned.ServiceRoot, MetadataDocument = Canned.Metadata });
		await _client.GetMetadataAsync();
	}

	[Benchmark(Baseline = true)]
	public Task<string> UntypedFilterExpandSelect() =>
		_client.For("People").Filter("FirstName eq 'Russell' and Gender eq Microsoft.OData.SampleService.Models.TripPin.PersonGender'Male'")
			.Expand("Trips", "Friends").Select("UserName", "FirstName", "LastName").OrderBy("LastName").Top(10).GetCommandTextAsync();

	[Benchmark]
	public Task<string> TypedFilterExpandSelect() =>
		_client.For<Person>("People").Filter(x => x.FirstName == "Russell" && x.Gender == PersonGender.Male)
			.Expand(x => new { x.Trips, x.Friends }).Select(x => new { x.UserName, x.FirstName, x.LastName }).OrderBy(x => x.LastName).Top(10).GetCommandTextAsync();

	[Benchmark]
	public Task<string> DynamicFilterExpandSelect()
	{
		var x = ODataDynamic.Expression;
		IBoundClient<ODataEntry> command = _client.For(x.People).Filter(x.FirstName == "Russell" && x.Gender == PersonGender.Male)
			.Expand(x.Trips, x.Friends).Select(x.UserName, x.FirstName, x.LastName).OrderBy(x.LastName).Top(10);
		return command.GetCommandTextAsync();
	}

	[Benchmark]
	public Task<string> TypedKeyLookup() => _client.For<Person>("People").Key("russellwhyte").GetCommandTextAsync();

	[Benchmark]
	public Task<string> TypedNestedExpandWithOptions() =>
		_client.For<Person>("People").Key("russellwhyte")
			.Expand(x => x.Trips.Select(t => t.Name)).Expand(x => x.Friends.OrderBy(f => f.LastName)).GetCommandTextAsync();
}
