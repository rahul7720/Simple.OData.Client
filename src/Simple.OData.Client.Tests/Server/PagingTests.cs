using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Server-driven paging through <c>odata.maxpagesize</c>, next links and <c>$count</c>.</summary>
public sealed class PagingTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private int ProductPageRequests => Requests.Count(x => x.Method == "GET" && x.Path.EndsWith("/Products", StringComparison.Ordinal));

	[Fact]
	public async Task MaxPageSizeProducesANextLink()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().For("Products").WithMaxPageSize(5).FindEntriesAsync(annotations);
		products.Should().HaveCount(5);
		annotations.NextPageLink.Should().NotBeNull();
		Request("GET", "/Products").Headers["Prefer"].Should().Contain("odata.maxpagesize=5");
	}

	[Fact]
	public async Task NextLinkCanBeFollowedManually()
	{
		var client = CreateClient();
		var annotations = new ODataFeedAnnotations();
		var firstPage = await client.For("Products").WithMaxPageSize(5).OrderBy("ProductID").FindEntriesAsync(annotations);
		var secondPage = await client.For("Products").WithMaxPageSize(5).FindEntriesAsync(annotations.NextPageLink!, annotations);
		secondPage.Should().HaveCount(5);
		secondPage.Select(x => x["ProductID"]).Should().NotIntersectWith(firstPage.Select(x => x["ProductID"]));
		annotations.NextPageLink.Should().NotBeNull("three more pages remain");
	}

	[Fact]
	public async Task AsyncEnumerableFollowsEveryPage()
	{
		var ids = new List<int>();
		await foreach (var product in CreateClient().For("Products").WithMaxPageSize(5).FindEntriesAsAsyncEnumerable())
		{
			ids.Add((int)product["ProductID"]!);
		}

		ids.Should().HaveCount(Seed.Products).And.OnlyHaveUniqueItems();
		ProductPageRequests.Should().Be(5, "22 products in pages of 5");
	}

	[Fact]
	public async Task AsyncEnumerableFollowsEveryPageTyped()
	{
		var names = new List<string>();
		await foreach (var product in CreateClient().For<Product>().WithMaxPageSize(10).FindEntriesAsAsyncEnumerable())
		{
			names.Add(product.ProductName);
		}

		names.Should().HaveCount(Seed.Products).And.Contain("Chai");
		ProductPageRequests.Should().Be(3);
	}

	[Fact]
	public async Task AsyncEnumerableFollowsEveryPageDynamic()
	{
		var x = ODataDynamic.Expression;
		var count = 0;
		IAsyncEnumerable<ODataEntry> products = CreateClient().For(x.Products).WithMaxPageSize(10).FindEntriesAsAsyncEnumerable();
		await foreach (var product in products)
		{
			count++;
		}

		count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task WithCountReportsTheTotalAcrossPages()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().For("Products").WithMaxPageSize(5).WithCount().FindEntriesAsync(annotations);
		products.Should().HaveCount(5);
		annotations.Count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task HeadersTravelWithEveryPage()
	{
		var count = 0;
		await foreach (var _ in CreateClient().For("Products").WithHeader("x-custom-header", "custom-value").WithMaxPageSize(10).FindEntriesAsAsyncEnumerable())
		{
			count++;
		}

		count.Should().Be(Seed.Products);
		Requests.Where(x => x.Method == "GET" && x.Path.EndsWith("/Products", StringComparison.Ordinal)).Should().HaveCount(3)
			.And.AllSatisfy(x => x.HasHeader("x-custom-header", "custom-value").Should().BeTrue());
	}

	[Fact]
	public async Task ClientPagingWithSkipAndTop()
	{
		var products = await CreateClient().For("Products").OrderBy("ProductID").Skip(20).Top(5).FindEntriesAsync();
		products.Should().HaveCount(2);
		products[0]["ProductID"].Should().Be(21);
	}
}
