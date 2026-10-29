using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Asynchronous requests: <c>Prefer: respond-async</c>, 202 Accepted and status monitor polling.</summary>
public sealed class AsyncRequestTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private ODataClient CreateAsyncClient() => CreateClient(s =>
	{
		s.RespondAsync = true;
		s.AsyncPollingInterval = TimeSpan.FromMilliseconds(1);
	});

	[Fact]
	public async Task RespondAsyncPollsTheStatusMonitor()
	{
		var products = await CreateAsyncClient().FindEntriesAsync("Products/NorthwindModel.Slow()");
		products.Should().HaveCount(Seed.Products);
		Request("GET", "/Products/NorthwindModel.Slow()").Headers["Prefer"].Should().Contain("respond-async");
		Requests.Should().Contain(x => x.Path.Contains("$monitor", StringComparison.Ordinal));
	}

	[Fact]
	public async Task RespondAsyncThroughTheFluentApi()
	{
		var products = await CreateAsyncClient().For("Products").Function("Slow").ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.Products);
		Requests.Should().Contain(x => x.Path.Contains("$monitor", StringComparison.Ordinal));
	}

	[Fact]
	public async Task RespondAsyncTyped()
	{
		var products = await CreateAsyncClient().For<Product>().Function("Slow").ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.Products);
		products.Should().Contain(x => x.ProductName == "Chai");
	}

	[Fact]
	public async Task WithoutThePreferenceTheServiceAnswersInline()
	{
		var products = await CreateClient().For("Products").Function("Slow").ExecuteAsEnumerableAsync();
		products.Should().HaveCount(Seed.Products);
		Requests.Should().NotContain(x => x.Path.Contains("$monitor", StringComparison.Ordinal));
	}
}
