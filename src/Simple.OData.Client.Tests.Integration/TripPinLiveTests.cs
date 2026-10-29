namespace Simple.OData.Client.Tests;

internal class GenderCount
{
	public PersonGender Gender { get; set; }
	public int Count { get; set; }
}

/// <summary>Scenarios against a per-session TripPin RESTier service: server paging, aggregation and an external HttpClient.</summary>
public sealed class TripPinLiveTests : LiveTestBase
{
	private static Task<ODataClient> ClientAsync(Action<ODataClientSettings>? configure = null) => CreateClientAsync(LiveServices.TripPinReadWrite, configure);

	[LiveFact]
	public async Task PagingWithAnnotationsCoversEveryPerson()
	{
		var client = await ClientAsync(s => s.IncludeAnnotationsInResults = true);
		var annotations = new ODataFeedAnnotations();
		var people = await client.For<PersonWithAnnotations>("People").WithCount().FindEntriesAsync(annotations);
		var total = people.Count;
		while (annotations.NextPageLink is not null)
		{
			people = await client.For<PersonWithAnnotations>("People").FindEntriesAsync(annotations.NextPageLink, annotations);
			total += people.Count;
			people.Should().AllSatisfy(x => x.Annotations.Id.Should().NotBeNull());
		}

		total.Should().Be((int)annotations.Count!.Value);
	}

	[LiveFact]
	public async Task AsyncEnumerableFollowsNextLinks()
	{
		var names = new List<string>();
		await foreach (var person in (await ClientAsync()).For<Person>("People").FindEntriesAsAsyncEnumerable())
		{
			names.Add(person.UserName);
		}

		names.Should().HaveCountGreaterThan(8).And.OnlyHaveUniqueItems();
	}

	[LiveFact]
	public async Task KeyLookupAndExpand()
	{
		var person = await (await ClientAsync()).For<Person>("People").Key("russellwhyte").Expand(x => x.Trips).FindEntryAsync();
		person!.FirstName.Should().Be("Russell");
		person.Trips.Should().NotBeEmpty();
	}

	[LiveFact]
	public async Task FilterByEnum()
	{
		var women = await (await ClientAsync()).For<Person>("People").Filter(x => x.Gender == PersonGender.Female).FindEntriesAsync();
		women.Should().NotBeEmpty().And.AllSatisfy(x => x.Gender.Should().Be(PersonGender.Female));
	}

	[LiveFact]
	public async Task ApplyGroupByGender()
	{
		var groups = await (await ClientAsync()).For<Person>("People").Apply(b => b.GroupBy((p, a) => new GenderCount { Gender = p.Gender, Count = a.Count() })).FindEntriesAsync();
		groups.Should().NotBeEmpty().And.AllSatisfy(x => x.Count.Should().BePositive());
	}

	[LiveFact]
	public async Task ApplyGroupByGenderDynamic()
	{
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		IEnumerable<dynamic> groups = await (await ClientAsync()).For(x.People).Apply(ODataDynamicDataAggregation.Builder.GroupBy(new { x.Gender, Count = a.Count() })).FindEntriesAsync();
		groups.Should().NotBeEmpty();
		((int)groups.First().Count).Should().BePositive();
	}

	[LiveFact]
	public async Task ExternalHttpClient()
	{
		var root = await LiveServices.ResolveAsync(LiveServices.TripPinReadWrite);
		using var http = new HttpClient { BaseAddress = root };
		var client = new ODataClient(new ODataClientSettings(http) { IgnoreUnmappedProperties = true });
		var person = await client.For<Person>("People").Key("russellwhyte").FindEntryAsync();
		person!.UserName.Should().Be("russellwhyte");
	}

	[LiveFact]
	public async Task UpdateRoundTrip()
	{
		var client = await ClientAsync();
		await client.For<Person>("People").Key("russellwhyte").Set(new { LastName = "Whyte-Live" }).UpdateEntryAsync();
		(await client.For<Person>("People").Key("russellwhyte").FindEntryAsync())!.LastName.Should().Be("Whyte-Live");
	}
}
