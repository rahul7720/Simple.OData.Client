using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Custom request headers reach the server for every kind of request.</summary>
public sealed class WithHeaderTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private const string Name = "x-custom-header";
	private const string Value = "custom-value";

	private void AssertHeader(string method, string pathSuffix) =>
		Request(method, pathSuffix).HasHeader(Name, Value).Should().BeTrue($"{method} {pathSuffix} should carry {Name}");

	[Fact]
	public async Task BuildRequestForCarriesHeadersAndDoesNotDuplicateThemOnRerun()
	{
		var requestClient = await CreateClient().For("Categories").WithHeader(Name, Value).WithHeader("x-other", "1").Key(1).BuildRequestFor().FindEntryAsync();
		var request = requestClient.GetRequest();
		request.RequestMessage.Headers.GetValues(Name).Should().ContainSingle().Which.Should().Be(Value);
		request.RequestMessage.Headers.GetValues("x-other").Should().ContainSingle().Which.Should().Be("1");

		using (var response = await requestClient.RunAsync())
		{
			(await response.ReadAsSingleAsync())!["CategoryName"].Should().Be("Beverages");
		}

		AssertHeader("GET", "/Categories(1)");
		Server.ClearRequests();
		using (await requestClient.RunAsync())
		{
		}

		Request("GET", "/Categories(1)").Headers[Name].Should().Be(Value, "re-running must not duplicate the header");
	}

	[Fact]
	public async Task WithHeadersDictionary()
	{
		await CreateClient().For("Products").WithHeaders(new Dictionary<string, string> { [Name] = Value, ["x-second"] = "2" }).Key(1).FindEntryAsync();
		var request = Request("GET", "/Products(1)");
		request.HasHeader(Name, Value).Should().BeTrue();
		request.HasHeader("x-second", "2").Should().BeTrue();
	}

	[Fact]
	public async Task FindEntry()
	{
		await CreateClient().For("Categories").WithHeader(Name, Value).Key(1).FindEntryAsync();
		AssertHeader("GET", "/Categories(1)");
	}

	[Fact]
	public async Task FindEntryTyped()
	{
		await CreateClient().For<Category>().WithHeader(Name, Value).Key(1).FindEntryAsync();
		AssertHeader("GET", "/Categories(1)");
	}

	[Fact]
	public async Task FindEntryDynamic()
	{
		var x = ODataDynamic.Expression;
		await CreateClient().For(x.Categories).WithHeader(Name, Value).Key(1).FindEntryAsync();
		AssertHeader("GET", "/Categories(1)");
	}

	[Fact]
	public async Task FindEntries()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Filter("ProductName eq 'Chai'").FindEntriesAsync();
		AssertHeader("GET", "/Products");
	}

	[Fact]
	public async Task FindEntriesWithAnnotations()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().For("Products").WithHeader(Name, Value).Top(5).FindEntriesAsync(annotations);
		products.Should().HaveCount(5);
		annotations.Count.Should().Be(Seed.Products);
		var request = Request("GET", "/Products");
		request.Query.Should().Contain("$count=true");
		request.HasHeader(Name, Value).Should().BeTrue();
	}

	[Fact]
	public async Task FindScalar()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Count().FindScalarAsync<int>();
		AssertHeader("GET", "/Products/$count");
	}

	[Fact]
	public async Task InsertEntry()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 18m }).InsertEntryAsync();
		AssertHeader("POST", "/Products");
	}

	[Fact]
	public async Task UpdateEntry()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Key(1).Set(new Entry { ["UnitPrice"] = 19m }).UpdateEntryAsync();
		AssertHeader("PATCH", "/Products(1)");
	}

	[Fact]
	public async Task DeleteEntry()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Key(1).DeleteEntryAsync();
		AssertHeader("DELETE", "/Products(1)");
	}

	[Fact]
	public async Task LinkAndUnlinkEntry()
	{
		var client = CreateClient();
		await client.For("Products").WithHeader(Name, Value).Key(1).LinkEntryAsync("Category", new Entry { ["CategoryID"] = 2 });
		AssertHeader("PUT", "/Products(1)/Category/$ref");
		await client.For("Products").WithHeader(Name, Value).Key(1).UnlinkEntryAsync("Category");
		AssertHeader("DELETE", "/Products(1)/Category/$ref");
	}

	[Fact]
	public async Task Function()
	{
		await CreateClient().Unbound().Function("PassThroughInt").WithHeader(Name, Value).Set(new Entry { ["value"] = 1 }).ExecuteAsScalarAsync<int>();
		AssertHeader("GET", "/PassThroughInt(value=1)");
	}

	[Fact]
	public async Task Action()
	{
		await CreateClient().For("Products").WithHeader(Name, Value).Key(1).Action("Rate").Set(new Entry { ["rating"] = 1 }).ExecuteAsync();
		AssertHeader("POST", "/Products(1)/NorthwindModel.Rate");
	}

	[Fact]
	public async Task MediaStreams()
	{
		var client = CreateClient();
		await client.For("Pictures").WithHeader(Name, Value).Key(1).Media().GetStreamAsStringAsync();
		AssertHeader("GET", "/Pictures(1)/$value");
		await client.For("Pictures").WithHeader(Name, Value).Key(1).Media().SetStreamAsync("stream_data");
		AssertHeader("PUT", "/Pictures(1)/$value");
	}

	[Fact]
	public async Task BatchHeadersAndOperationHeaders()
	{
		var batch = new ODataBatch(CreateClient()).WithHeader("x-batch-header", "batch-value");
		batch += c => c.For("Products").WithHeader(Name, Value).Key(1).FindEntryAsync();
		batch += c => c.For("Products").WithHeader(Name, Value).Set(new Entry { ["ProductName"] = "Test1", ["UnitPrice"] = 10m }).InsertEntryAsync(false);
		await batch.ExecuteAsync();

		Requests.Single(x => x.IsBatch).HasHeader("x-batch-header", "batch-value").Should().BeTrue();
		AssertHeader("GET", "/Products(1)");
		AssertHeader("POST", "/Products");
	}

	[Fact]
	public async Task ClientApiHeaders()
	{
		var client = CreateClient(s => s.BeforeRequest = r => r.Headers.TryAddWithoutValidation(Name, Value));
		await client.FindEntryAsync("Products(1)");
		AssertHeader("GET", "/Products(1)");
	}
}
