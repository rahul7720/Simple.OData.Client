using Simple.OData.Client.Tests.Infrastructure;
using Entry = System.Collections.Generic.Dictionary<string, object?>;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Untyped (dictionary) query scenarios against the in-process server.</summary>
public sealed class FindTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private static IDictionary<string, object?> Nested(IDictionary<string, object?> entry, string key) => (IDictionary<string, object?>)entry[key]!;

	private static IReadOnlyList<IDictionary<string, object?>> Items(IDictionary<string, object?> entry, string key) =>
		((IEnumerable<object?>)entry[key]!).Cast<IDictionary<string, object?>>().ToList();

	[Fact]
	public async Task Filter()
	{
		var products = await CreateClient().For("Products").Filter("ProductName eq 'Chai'").FindEntriesAsync();
		products.Single()["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FilterStringExpression()
	{
		var products = await CreateClient().For("Products").Filter("contains(ProductName,'ai')").FindEntriesAsync();
		products.Single()["ProductName"].Should().Be("Chai");
	}

	[Fact]
	public async Task FilterIn()
	{
		var products = await CreateClient().For("Products").Filter("ProductID in (1,2,3)").FindEntriesAsync();
		products.Should().HaveCount(3);
	}

	[Fact]
	public async Task FilterStringFunctions()
	{
		var products = await CreateClient().For("Products").Filter("startswith(ProductName,'Ch')").FindEntriesAsync();
		products.Select(x => x["ProductName"]).Should().BeEquivalentTo(["Chai", "Chang", "Chef Anton's Cajun Seasoning", "Chef Anton's Gumbo Mix"]);
	}

	[Fact]
	public async Task FilterDateFunctions()
	{
		var employees = await CreateClient().For("Employees").Filter("year(HireDate) eq 1992").FindEntriesAsync();
		employees.Should().HaveCount(3);
	}

	[Fact]
	public async Task FilterEnum()
	{
		var customers = await CreateClient().For("Customers").Filter("Address/Type eq NorthwindModel.AddressType'Work'").FindEntriesAsync();
		customers.Should().HaveCount(Seed.WorkAddressCustomers);
	}

	[Fact]
	public async Task Search()
	{
		var products = await CreateClient().For("Products").Search("cha").FindEntriesAsync();
		products.Select(x => x["ProductName"]).Should().BeEquivalentTo(["Chai", "Chang"]);
	}

	[Fact]
	public async Task Get()
	{
		var category = await CreateClient().For("Categories").Key(1).FindEntryAsync();
		category!["CategoryID"].Should().Be(1);
	}

	[Fact]
	public async Task GetNonExistingIsNullWhenIgnored()
	{
		var category = await CreateClient().For("Categories").Key(-1).FindEntryAsync();
		category.Should().BeNull();
	}

	[Fact]
	public async Task GetNonExistingThrows()
	{
		var client = CreateClient(x => x.IgnoreResourceNotFoundException = false);
		var act = () => client.For("Categories").Key(-1).FindEntryAsync();
		(await act.Should().ThrowAsync<WebRequestException>()).Which.Code.Should().Be(System.Net.HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GetByGuidKey()
	{
		var item = await CreateClient().For("TypeWithGuidKey").Key(Seed.FirstGuidKey).FindEntryAsync();
		item!["Name"].Should().Be("first");
	}

	[Fact]
	public async Task GetByCompoundKey()
	{
		var detail = await CreateClient().For("Order_Details").Key(10248, 11).FindEntryAsync();
		detail!["ProductID"].Should().Be(11);
		var named = await CreateClient().For("Order_Details").Key(new Entry { ["OrderID"] = 10248, ["ProductID"] = 11 }).FindEntryAsync();
		named!["Quantity"].Should().Be((short)12);
	}

	[Fact]
	public async Task GetByAlternateKey()
	{
		var product = await CreateClient().For("Products").Key(new Entry { ["ProductName"] = "Chai" }).FindEntryAsync();
		product!["ProductID"].Should().Be(1);
	}

	[Fact]
	public async Task Singleton()
	{
		var company = await CreateClient().For("Company").FindEntryAsync();
		company!["Name"].Should().Be("Northwind Traders");
		Nested(company, "Headquarters")["City"].Should().Be("Seattle");
	}

	[Fact]
	public async Task ContainedNavigation()
	{
		var addresses = await CreateClient().For("Customers").Key("ALFKI").NavigateTo("Addresses").FindEntriesAsync();
		addresses.Should().HaveCount(2);
		var address = await CreateClient().For("Customers").Key("ALFKI").NavigateTo("Addresses").Key(2).FindEntryAsync();
		address!["Street"].Should().Be("PO Box ALFKI");
	}

	[Fact]
	public async Task OpenTypeDynamicProperties()
	{
		var animal = await CreateClient().For("Animals").Key(1).FindEntryAsync();
		animal!["Name"].Should().Be("Dog");
		animal["Legs"].Should().Be(4);
		animal["Sound"].Should().Be("Woof");
	}

	[Fact]
	public async Task SkipOne()
	{
		var products = await CreateClient().For("Products").Skip(1).FindEntriesAsync();
		products.Should().HaveCount(Seed.Products - 1);
	}

	[Fact]
	public async Task TopOne()
	{
		var products = await CreateClient().For("Products").Top(1).FindEntriesAsync();
		products.Should().ContainSingle();
	}

	[Fact]
	public async Task SkipOneTopOne()
	{
		var products = await CreateClient().For("Products").Skip(1).Top(1).FindEntriesAsync();
		products.Should().ContainSingle().Which["ProductID"].Should().Be(2);
	}

	[Fact]
	public async Task OrderBy()
	{
		var products = await CreateClient().For("Products").OrderBy("ProductName").FindEntriesAsync();
		products[0]["ProductName"].Should().Be("Alice Mutton");
	}

	[Fact]
	public async Task OrderByDescending()
	{
		var products = await CreateClient().For("Products").OrderByDescending("ProductName").FindEntriesAsync();
		products[0]["ProductName"].Should().Be("Uncle Bob's Organic Dried Pears");
	}

	[Fact]
	public async Task OrderByExpanded()
	{
		var products = await CreateClient().For("Products").Expand("Category").OrderBy("Category/CategoryName").FindEntriesAsync();
		Nested(products[^1], "Category")["CategoryName"].Should().Be("Seafood");
	}

	[Fact]
	public async Task SelectSingle()
	{
		var product = await CreateClient().For("Products").Select("ProductName").FindEntryAsync();
		product!.Keys.Should().Contain("ProductName").And.NotContain("ProductID");
	}

	[Fact]
	public async Task SelectSingleHomogenize()
	{
		var product = await CreateClient().For("Products").Select("Product_Name").FindEntryAsync();
		product!.Keys.Should().Contain("ProductName").And.NotContain("ProductID");
	}

	[Fact]
	public async Task SelectMultiple()
	{
		var product = await CreateClient().For("Products").Select("ProductID", "ProductName").FindEntryAsync();
		product!.Keys.Should().Contain(["ProductID", "ProductName"]);
	}

	[Fact]
	public async Task SelectMultipleSingleString()
	{
		var product = await CreateClient().For("Products").Select("ProductID, ProductName").FindEntryAsync();
		product!.Should().HaveCount(2);
		product.Keys.Should().Contain(["ProductID", "ProductName"]);
	}

	[Fact]
	public async Task ExpandOne()
	{
		var products = await CreateClient().For("Products").OrderBy("ProductID").Expand("Category").FindEntriesAsync();
		Nested(products[^1], "Category")["CategoryName"].Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandMany()
	{
		var category = await CreateClient().For("Categories").Expand("Products").Filter("CategoryName eq 'Beverages'").FindEntryAsync();
		Items(category!, "Products").Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandSecondLevel()
	{
		var products = await CreateClient().For("Products").OrderBy("ProductID").Expand("Category/Products").FindEntriesAsync();
		Items(Nested(products[^1], "Category"), "Products").Should().HaveCount(2);
	}

	[Fact]
	public async Task ExpandMultipleLevelsWithCollection()
	{
		var products = await CreateClient().For("Products").OrderBy("ProductID").Expand("Category/Products/Category").FindEntriesAsync();
		var firstSibling = Items(Nested(products[^1], "Category"), "Products")[0];
		Nested(firstSibling, "Category")["CategoryName"].Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandMultipleLevelsWithCollectionAndSelect()
	{
		var products = await CreateClient()
			.For("Products")
			.OrderBy("ProductID")
			.Expand("Category/Products/Category")
			.Select("Category/Products/Category/CategoryName")
			.FindEntriesAsync();
		var product = products[^1];
		product.Should().HaveCount(1);
		var deepest = Nested(Items(Nested(product, "Category"), "Products")[0], "Category");
		deepest.Should().HaveCount(1);
		deepest["CategoryName"].Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandWithSelect()
	{
		var products = await CreateClient()
			.For("Products")
			.OrderBy("ProductID")
			.Expand("Category/Products")
			.Select("ProductName", "Category/CategoryName")
			.FindEntriesAsync();
		var product = products[^1];
		product.Should().HaveCount(2);
		var category = Nested(product, "Category");
		category["CategoryName"].Should().Be("Meat/Poultry");
		category.Keys.Should().NotContain("Description");
	}

	[Fact]
	public async Task Count()
	{
		var count = await CreateClient().For("Products").Count().FindScalarAsync<int>();
		count.Should().Be(Seed.Products);
	}

	[Fact]
	public async Task FilterCount()
	{
		var count = await CreateClient().For("Products").Filter("ProductName eq 'Chai'").Count().FindScalarAsync<int>();
		count.Should().Be(1);
	}

	[Fact]
	public async Task TotalCount()
	{
		var annotations = new ODataFeedAnnotations();
		var products = await CreateClient().For("Products").FindEntriesAsync(annotations);
		annotations.Count.Should().Be(Seed.Products);
		products.Should().HaveCount(Seed.Products);
	}

	[Fact]
	public async Task CombineAll()
	{
		var products = await CreateClient()
			.For("Products")
			.OrderBy("ProductName")
			.Skip(2)
			.Top(1)
			.Expand("Category")
			.Select("Category")
			.FindEntriesAsync();
		Nested(products.Single(), "Category")["CategoryName"].Should().Be("Seafood");
	}

	[Fact]
	public async Task CombineAllReverse()
	{
		var products = await CreateClient()
			.For("Products")
			.Select("Category")
			.Expand("Category")
			.Top(1)
			.Skip(2)
			.OrderBy("ProductName")
			.FindEntriesAsync();
		Nested(products.Single(), "Category")["CategoryName"].Should().Be("Seafood");
	}

	[Fact]
	public async Task NavigateToSingle()
	{
		var category = await CreateClient().For("Products").Key(new Entry { ["ProductID"] = 2 }).NavigateTo("Category").FindEntryAsync();
		category!["CategoryName"].Should().Be("Beverages");
	}

	[Fact]
	public async Task NavigateToMultiple()
	{
		var products = await CreateClient().For("Categories").Key(2).NavigateTo("Products").FindEntriesAsync();
		products.Should().HaveCount(Seed.CondimentsProducts);
	}

	[Fact]
	public async Task NavigateToRecursive()
	{
		var employee = await CreateClient()
			.For("Employees")
			.Key(6)
			.NavigateTo("Superior")
			.NavigateTo("Superior")
			.NavigateTo("Subordinates")
			.Key(3)
			.FindEntryAsync();
		employee!["FirstName"].Should().Be("Janet");
	}

	[Fact]
	public async Task NavigateToRecursiveSingleClause()
	{
		var employee = await CreateClient().For("Employees").Key(6).NavigateTo("Superior/Superior/Subordinates").Key(3).FindEntryAsync();
		employee!["FirstName"].Should().Be("Janet");
	}

	[Fact]
	public async Task BaseClassEntries()
	{
		var transport = await CreateClient().For("Transport").FindEntriesAsync();
		transport.Should().HaveCount(Seed.Transports);
		transport.Should().NotContain(x => x.ContainsKey("__annotations"));
	}

	[Fact]
	public async Task BaseClassEntriesWithAnnotations()
	{
		var transport = await CreateClient(x => x.IncludeAnnotationsInResults = true).For("Transport").FindEntriesAsync();
		transport.Should().HaveCount(Seed.Transports);
		transport.Should().OnlyContain(x => x.ContainsKey("__annotations"));
	}

	[Fact]
	public async Task AllDerivedClassEntries()
	{
		var ships = await CreateClient().For("Transport").As("Ship").FindEntriesAsync();
		ships.Should().HaveCount(Seed.Ships);
		ships[0]["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task AllDerivedClassEntriesWithAnnotations()
	{
		var ships = await CreateClient(x => x.IncludeAnnotationsInResults = true).For("Transport").As("Ship").FindEntriesAsync();
		var ship = ships[0];
		ship["ShipName"].Should().Be("Titanic");
		((ODataEntryAnnotations)ship["__annotations"]!).TypeName.Should().Be("NorthwindModel.Ship");
	}

	[Fact]
	public async Task DerivedClassEntry()
	{
		var ship = await CreateClient().For("Transport").As("Ship").Filter("ShipName eq 'Titanic'").FindEntryAsync();
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task BaseClassEntryByKey()
	{
		var transport = await CreateClient().For("Transport").Key(1).FindEntryAsync();
		transport!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntryByKey()
	{
		var ship = await CreateClient().For("Transport").As("Ship").Key(1).FindEntryAsync();
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntryBaseAndDerivedFields()
	{
		var ship = await CreateClient().For("Transport").As("Ship").Filter("TransportID eq 1 and ShipName eq 'Titanic'").FindEntryAsync();
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfDerivedClassEntry()
	{
		var ship = await CreateClient().For("Transport").Filter("isof('NorthwindModel.Ship')").FindEntryAsync();
		ship!["ShipName"].Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfAssociation()
	{
		var employee = await CreateClient().For("Employees").Filter("isof(Superior, 'NorthwindModel.Employee')").FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task CastToPrimitiveType()
	{
		var product = await CreateClient().For("Products").Filter("ProductID eq cast(1, 'Edm.Int32')").FindEntryAsync();
		product.Should().NotBeNull();
	}

	[Fact]
	public async Task CastInstanceToEntityType()
	{
		var employee = await CreateClient().For("Employees").Filter("cast('NorthwindModel.Employee') ne null").FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task CastPropertyToEntityType()
	{
		var employee = await CreateClient().For("Employees").Filter("cast(Superior, 'NorthwindModel.Employee') ne null").FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task FilterAny()
	{
		var orders = await CreateClient().For("Orders").Filter("Order_Details/any(d:d/Quantity gt 30)").FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAnyDetailOver30);
	}

	[Fact]
	public async Task FilterAll()
	{
		var orders = await CreateClient().For("Orders").Filter("Order_Details/all(d:d/Quantity ge 10)").FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAllDetailsAtLeast10);
	}
}
