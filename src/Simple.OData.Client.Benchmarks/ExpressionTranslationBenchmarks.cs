using BenchmarkDotNet.Attributes;

namespace Simple.OData.Client.Benchmarks;

/// <summary>Translating LINQ and dynamic filter expressions into <c>$filter</c> text.</summary>
[MemoryDiagnoser]
public class ExpressionTranslationBenchmarks
{
	private ODataClient _client = null!;

	[GlobalSetup]
	public async Task Setup()
	{
		_client = new ODataClient(new ODataClientSettings { BaseUri = Canned.ServiceRoot, MetadataDocument = Canned.Metadata });
		await _client.GetMetadataAsync();
	}

	[Benchmark(Baseline = true)]
	public Task<string> SimpleComparison() =>
		_client.For<Person>("People").Filter(x => x.UserName == "russellwhyte").GetCommandTextAsync();

	[Benchmark]
	public Task<string> StringFunctionsAndLogic() =>
		_client.For<Person>("People").Filter(x => x.FirstName.StartsWith("R") && x.LastName.Contains("hy") || x.Emails.Any(e => e.EndsWith("contoso.com"))).GetCommandTextAsync();

	[Benchmark]
	public Task<string> NavigationAnyAll() =>
		_client.For<Person>("People").Filter(x => x.Trips.Any(t => t.Budget > 1000 && t.Tags.Any(tag => tag == "Business")) && x.Friends.All(f => f.Gender != PersonGender.Unknown)).GetCommandTextAsync();

	[Benchmark]
	public Task<string> DateAndArithmetic() =>
		_client.For<Person>("People").Filter(x => x.Trips.Any(t => t.StartsAt > new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) && t.Budget * 2 < 5000)).GetCommandTextAsync();

	[Benchmark]
	public Task<string> DynamicExpression()
	{
		var x = ODataDynamic.Expression;
		IBoundClient<ODataEntry> command = _client.For(x.People).Filter(x.FirstName.StartsWith("R") && x.LastName.Contains("hy") || x.Trips.Any(x.Budget > 1000));
		return command.GetCommandTextAsync();
	}
}
