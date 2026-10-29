using System.Net;
using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Two-phase requests: <c>BuildRequestFor()</c>, <c>RunAsync()</c> / <c>FromResponse()</c> and the typed readers.</summary>
public sealed class RequestBuilderTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task FindEntriesRunAndReadAsCollection()
	{
		var requestClient = await CreateClient().For("Products").Filter("ProductName eq 'Chai'").BuildRequestFor().FindEntriesAsync();
		var request = requestClient.GetRequest();
		request.Method.Should().Be("GET");
		request.CommandText.Should().StartWith("Products?$filter=");
		request.RequestMessage.RequestUri!.AbsolutePath.Should().EndWith("/Products");

		using var response = await requestClient.RunAsync();
		response.ResponseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
		var products = await response.ReadAsCollectionAsync();
		products.Single()["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FindEntriesWithAnnotations()
	{
		var annotations = new ODataFeedAnnotations();
		var requestClient = await CreateClient().For<Product>().Top(3).BuildRequestFor().FindEntriesAsync(annotations);
		using var response = await requestClient.RunAsync();
		var products = await response.ReadAsCollectionAsync(annotations);
		products.Should().HaveCount(3);
		annotations.Count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task FindEntryTyped()
	{
		var requestClient = await CreateClient().For<Product>().Key(1).BuildRequestFor().FindEntryAsync();
		using var response = await requestClient.RunAsync();
		var product = await response.ReadAsSingleAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task FindScalar()
	{
		var requestClient = await CreateClient().For("Products").Count().BuildRequestFor().FindScalarAsync();
		using var response = await requestClient.RunAsync();
		(await response.ReadAsScalarAsync<int>()).Should().Be(Seed.Products);
	}

	[Fact]
	public async Task FromResponseReadsAResponseObtainedElsewhere()
	{
		var client = CreateClient();
		var requestClient = await client.For<Product>().Filter(x => x.CategoryID == 1).BuildRequestFor().FindEntriesAsync();
		using var http = CreateHttpClient();
		using var httpResponse = await http.SendAsync(requestClient.GetRequest().RequestMessage);
		using var response = requestClient.FromResponse(httpResponse);
		var products = await response.ReadAsCollectionAsync();
		products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task InsertUpdateDelete()
	{
		var client = CreateClient();
		var insert = await client.For("Products").Set(new Entry { ["ProductName"] = "Built", ["UnitPrice"] = 5m }).BuildRequestFor().InsertEntryAsync();
		insert.GetRequest().Method.Should().Be("POST");
		int id;
		using (var response = await insert.RunAsync())
		{
			response.ResponseMessage.StatusCode.Should().Be(HttpStatusCode.Created);
			var product = await response.ReadAsSingleAsync();
			id = (int)product!["ProductID"]!;
		}

		var update = await client.For("Products").Key(id).Set(new Entry { ["UnitPrice"] = 6m }).BuildRequestFor().UpdateEntryAsync();
		update.GetRequest().Method.Should().Be("PATCH");
		using (var response = await update.RunAsync())
		{
			(await response.ReadAsSingleAsync())!["UnitPrice"].Should().Be(6m);
		}

		var updateWithoutResult = await client.For("Products").Key(id).Set(new Entry { ["UnitPrice"] = 7m }).BuildRequestFor().UpdateEntryAsync(resultRequired: false);
		using (var response = await updateWithoutResult.RunAsync())
		{
			response.ResponseMessage.StatusCode.Should().Be(HttpStatusCode.NoContent);
		}

		var delete = await client.For("Products").Key(id).BuildRequestFor().DeleteEntryAsync();
		delete.GetRequest().Method.Should().Be("DELETE");
		using (var response = await delete.RunAsync())
		{
			response.ResponseMessage.StatusCode.Should().Be(HttpStatusCode.NoContent);
		}

		(await client.For("Products").Key(id).FindEntryAsync()).Should().BeNull();
	}

	[Fact]
	public async Task ResponseStreamIsAvailable()
	{
		var requestClient = await CreateClient().For("Products").Key(1).BuildRequestFor().FindEntryAsync();
		using var response = await requestClient.RunAsync();
		using var stream = await response.GetResponseStreamAsync();
		using var reader = new StreamReader(stream);
		(await reader.ReadToEndAsync()).Should().Contain("\"ProductName\":\"Chai\"");
	}
}
