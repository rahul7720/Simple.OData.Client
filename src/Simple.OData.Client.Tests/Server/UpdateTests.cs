using System.Net;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class UpdateTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private static IReadOnlyList<IDictionary<string, object?>> Items(IDictionary<string, object?> entry, string key) =>
		((IEnumerable<object?>)entry[key]!).Cast<IDictionary<string, object?>>().ToList();

	[Fact]
	public async Task UpdateByKey()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Key(product!["ProductID"]).Set(new { UnitPrice = 123m }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByKeyClearMetadataCache()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		client.Session.ClearMetadataCache();
		await client.For("Products").Key(product!["ProductID"]).Set(new { UnitPrice = 123m }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByFilter()
	{
		var client = CreateClient();
		await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Filter("ProductName eq 'Test1'").Set(new { UnitPrice = 123m }).UpdateEntryAsync();
		var product = await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleWithResult()
	{
		var client = CreateClient();
		await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var products = await client.For("Products").Filter("ProductName eq 'Test1'").Set(new { UnitPrice = 123m }).UpdateEntriesAsync();
		products.Single()["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleNoResult()
	{
		var client = CreateClient();
		await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var products = await client.For("Products").Filter("ProductName eq 'Test1'").Set(new { UnitPrice = 123m }).UpdateEntriesAsync(false);
		products.Should().BeEmpty();
		var product = await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByObjectAsKey()
	{
		var client = CreateClient();
		var product = await client.For("Products").Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Key(product!).Set(new { UnitPrice = 456m }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductName eq 'Test1'").FindEntryAsync();
		product!["UnitPrice"].Should().Be(456m);
	}

	[Fact]
	public async Task UpdateDate()
	{
		var client = CreateClient();
		var today = new DateTimeOffset(2018, 5, 20, 20, 30, 40, 677, TimeSpan.Zero);
		var tomorrow = today.AddDays(1);
		var employee = await client.For("Employees").Set(new { FirstName = "Test1", LastName = "Test1", HireDate = today }).InsertEntryAsync();
		await client.For("Employees").Key(employee!["EmployeeID"]).Set(new { HireDate = tomorrow }).UpdateEntryAsync();
		employee = await client.For("Employees").Key(employee["EmployeeID"]).FindEntryAsync();
		employee!["HireDate"].Should().Be(tomorrow);
	}

	[Fact]
	public async Task AddSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test1" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Test2", UnitPrice = 18m }).InsertEntryAsync();
		await client.For("Products").Key(product!["ProductID"]).Set(new { Category = category }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductID eq " + product["ProductID"]).FindEntryAsync();
		product!["CategoryID"].Should().Be(category!["CategoryID"]);
		category = await client.For("Categories").Filter("CategoryID eq " + category["CategoryID"]).Expand("Products").FindEntryAsync();
		Items(category!, "Products").Should().ContainSingle();
	}

	[Fact]
	public async Task UpdateSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test1" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Test2", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		await client.For("Products").Key(product!["ProductID"]).Set(new { Category = category }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductID eq " + product["ProductID"]).FindEntryAsync();
		product!["CategoryID"].Should().Be(category!["CategoryID"]);
		category = await client.For("Categories").Filter("CategoryID eq " + category["CategoryID"]).Expand("Products").FindEntryAsync();
		Items(category!, "Products").Should().ContainSingle();
	}

	[Fact]
	public async Task RemoveSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test6" }).InsertEntryAsync();
		var product = await client.For("Products").Set(new { ProductName = "Test7", UnitPrice = 18m, Category = category }).InsertEntryAsync();
		await client.For("Products").Key(product!["ProductID"]).Set(new { Category = (Category?)null }).UpdateEntryAsync();
		product = await client.For("Products").Filter("ProductID eq " + product["ProductID"]).FindEntryAsync();
		product!["CategoryID"].Should().BeNull();
	}

	[Fact]
	public async Task UpdateMultipleAssociations()
	{
		var client = CreateClient();
		var category = await client.For("Categories").Set(new { CategoryName = "Test3" }).InsertEntryAsync();
		var product1 = await client.For("Products").Set(new { ProductName = "Test4", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		var product2 = await client.For("Products").Set(new { ProductName = "Test5", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		await client.For("Categories").Key(category!["CategoryID"]).Set(new { Products = new[] { product1, product2 } }).UpdateEntryAsync();
		category = await client.For("Categories").Filter("CategoryID eq " + category["CategoryID"]).Expand("Products").FindEntryAsync();
		Items(category!, "Products").Should().HaveCount(2);
	}

	[Fact]
	public async Task UpdateDerived()
	{
		var client = CreateClient();
		var ship = await client.For("Transport").As("Ship").Set(new { ShipName = "Test1" }).InsertEntryAsync();
		ship = await client.For("Transport").As("Ship").Key(ship!["TransportID"]).Set(new { ShipName = "Test2" }).UpdateEntryAsync();
		ship!["ShipName"].Should().Be("Test2");
	}

	[Fact]
	public async Task UpdateWithPutWhenPreferred()
	{
		var client = CreateClient(x => x.PreferredUpdateMethod = ODataUpdateMethod.Put);
		var product = await client.For("Products").Set(new { ProductName = "PutMe", UnitPrice = 18m }).InsertEntryAsync();
		var updated = await client.For("Products").Key(product!["ProductID"]).Set(new { ProductName = "PutMe", UnitPrice = 99m }).UpdateEntryAsync();
		updated!["UnitPrice"].Should().Be(99m);
	}

	[Fact]
	public async Task UpdateWithIfMatchStaleEtagFails()
	{
		var client = CreateClient();
		var act = () => client.For("Products").Key(1).WithIfMatch("W/\"stale\"").Set(new { UnitPrice = 1m }).UpdateEntryAsync();
		var exception = (await act.Should().ThrowAsync<WebRequestException>()).Which;
		exception.Code.Should().Be(HttpStatusCode.PreconditionFailed);
	}

	[Fact]
	public async Task UpdateWithIfMatchCurrentEtagSucceeds()
	{
		var client = CreateClient(x => x.IncludeAnnotationsInResults = true);
		var product = await client.For("Products").Key(1).FindEntryAsync();
		var etag = ((ODataEntryAnnotations)product!["__annotations"]!).ETag!;
		var updated = await client.For("Products").Key(1).WithIfMatch(etag).Set(new { UnitPrice = 2m }).UpdateEntryAsync();
		updated!["UnitPrice"].Should().Be(2m);
		((ODataEntryAnnotations)updated["__annotations"]!).ETag.Should().NotBe(etag);
	}

	[Fact]
	public async Task UpdateSingleton()
	{
		var client = CreateClient();
		var company = await client.For("Company").Set(new { EmployeeCount = 10 }).UpdateEntryAsync();
		company!["EmployeeCount"].Should().Be(10);
		(await client.For("Company").FindEntryAsync())!["EmployeeCount"].Should().Be(10);
	}

	[Fact]
	public async Task UpdateComplexProperty()
	{
		var client = CreateClient();
		await client.For("Customers").Key("ALFKI").Set(new Dictionary<string, object?> { ["Address"] = new Dictionary<string, object?> { ["City"] = "Munich", ["Type"] = "Shipping" } }).UpdateEntryAsync();
		var customer = await client.For("Customers").Key("ALFKI").FindEntryAsync();
		((IDictionary<string, object?>)customer!["Address"]!)["City"].Should().Be("Munich");
	}
}
