using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Typed query scenarios against the in-process server.</summary>
public sealed class FindTypedTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task SingleCondition()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task SingleConditionWithLocalVariable()
	{
		var name = "Chai";
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == name).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task CombinedConditions()
	{
		var employee = await CreateClient().For<Employee>().Filter(x => x.FirstName == "Nancy" && x.HireDate < DateTime.Now).FindEntryAsync();
		employee!.LastName.Should().Be("Davolio");
	}

	[Fact]
	public async Task CombinedConditionsWithMultipleFilters()
	{
		var employee = await CreateClient().For<Employee>().Filter(x => x.FirstName == "Nancy").Filter(x => x.HireDate < DateTime.Now).FindEntryAsync();
		employee!.LastName.Should().Be("Davolio");
	}

	[Fact]
	public async Task CombineAll()
	{
		var products = await CreateClient()
			.For<Product>()
			.OrderBy(x => x.ProductName)
			.ThenByDescending(x => x.UnitPrice)
			.Skip(2)
			.Top(1)
			.Expand(x => x.Category)
			.Select(x => x.Category)
			.FindEntriesAsync();
		products.Single().Category.CategoryName.Should().Be("Seafood");
	}

	[Fact]
	public async Task CombineAllReverse()
	{
		var products = await CreateClient()
			.For<Product>()
			.Select(x => x.Category)
			.Expand(x => x.Category)
			.Top(1)
			.Skip(2)
			.OrderBy(x => x.ProductName)
			.ThenByDescending(x => x.UnitPrice)
			.FindEntriesAsync();
		products.Single().Category.CategoryName.Should().Be("Seafood");
	}

	[Fact]
	public async Task MappedColumn()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new Product { ProductName = "Test1", UnitPrice = 18m, MappedEnglishName = "EnglishTest" }).InsertEntryAsync(false);
		var product = await client.For<Product>().Filter(x => x.ProductName == "Test1").Select(x => new { x.ProductID, x.ProductName, x.MappedEnglishName }).FindEntryAsync();
		product!.MappedEnglishName.Should().Be("EnglishTest");
	}

	[Fact]
	public async Task UnmappedColumnThrows()
	{
		var act = () => CreateClient().For<ProductWithUnmappedProperty>("Products").Set(new ProductWithUnmappedProperty { ProductName = "Test1", UnmappedName = "x" }).InsertEntryAsync();
		await act.Should().ThrowAsync<UnresolvableObjectException>();
	}

	[Fact]
	public async Task IgnoredUnmappedColumn()
	{
		var client = CreateClient(x => x.IgnoreUnmappedProperties = true);
		var product = await client.For<ProductWithUnmappedProperty>("Products").Set(new ProductWithUnmappedProperty { ProductName = "Test1", UnmappedName = "x" }).InsertEntryAsync();
		await client.For<ProductWithUnmappedProperty>("Products").Key(product!.ProductID).Set(new ProductWithUnmappedProperty { ProductName = "Test2" }).UpdateEntryAsync(false);
		product = await client.For<ProductWithUnmappedProperty>("Products").Key(product.ProductID).FindEntryAsync();
		product!.ProductName.Should().Be("Test2");
	}

	[Fact]
	public async Task RemappedColumn()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new ProductWithRemappedColumn { ProductName = "Test1", UnitPrice = 18m, MappedEnglishName = "EnglishTest" }).InsertEntryAsync(false);
		var product = await client.For<Product>().Filter(x => x.ProductName == "Test1").Select(x => new { x.ProductID, x.ProductName, x.MappedEnglishName }).FindEntryAsync();
		product!.MappedEnglishName.Should().Be("EnglishTest");
	}

	[Fact]
	public async Task Subclass()
	{
		var product = await CreateClient().For<ExtendedProduct>("Products").Filter(x => x.ProductName == "Chai").FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task StringContains()
	{
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Contains("ai")).FindEntriesAsync();
		products.Single().ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task StringContainsWithLocalVariable()
	{
		var text = "ai";
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Contains(text)).FindEntriesAsync();
		products.Single().ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task StringContainsWithArrayVariable()
	{
		var text = new[] { "ai" };
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Contains(text[0])).FindEntriesAsync();
		products.Single().ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task StringNotContains()
	{
		var products = await CreateClient().For<Product>().Filter(x => !x.ProductName.Contains("ai")).FindEntriesAsync();
		products.Should().HaveCount(Seed.Products - 1).And.NotContain(x => x.ProductName == "Chai");
	}

	[Fact]
	public async Task StringStartsWith()
	{
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.StartsWith("Ch")).FindEntriesAsync();
		products[0].ProductName.Should().Be("Chai");
		products.Should().HaveCount(4);
	}

	[Fact]
	public async Task LengthOfStringEqual()
	{
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Length == 4).FindEntriesAsync();
		products.Select(x => x.ProductName).Should().BeEquivalentTo(["Chai", "Tofu"]);
	}

	[Fact]
	public async Task SubstringWithPositionAndLengthEqual()
	{
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Substring(1, 2) == "ha").FindEntriesAsync();
		products.Select(x => x.ProductName).Should().BeEquivalentTo(["Chai", "Chang"]);
	}

	[Fact]
	public async Task SubstringWithPositionAndLengthEqualWithLocalVariable()
	{
		var text = "ha";
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName.Substring(1, 2) == text).FindEntriesAsync();
		products[0].ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task FilterIn()
	{
		var ids = new List<int> { 1, 2, 3 };
		var products = await CreateClient().For<Product>().Filter(x => ids.Contains(x.ProductID)).FindEntriesAsync();
		products.Should().HaveCount(3);
	}

	[Fact]
	public async Task FilterEnum()
	{
		var customers = await CreateClient().For<Customer>().Filter(x => x.Address!.Type == AddressType.Work).FindEntriesAsync();
		customers.Should().HaveCount(Seed.WorkAddressCustomers);
		customers.Should().OnlyContain(x => x.Address!.Type == AddressType.Work);
	}

	[Fact]
	public async Task FilterDateParts()
	{
		var employees = await CreateClient().For<Employee>().Filter(x => x.HireDate.Year == 1992).FindEntriesAsync();
		employees.Should().HaveCount(3);
	}

	[Fact]
	public async Task Search()
	{
		var products = await CreateClient().For<Product>().Search("cha").FindEntriesAsync();
		products.Select(x => x.ProductName).Should().BeEquivalentTo(["Chai", "Chang"]);
	}

	[Fact]
	public async Task TopOne()
	{
		var products = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").Top(1).FindEntriesAsync();
		products.Should().ContainSingle();
	}

	[Fact]
	public async Task Count()
	{
		var count = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").Count().FindScalarAsync<int>();
		count.Should().Be(1);
	}

	[Fact]
	public async Task Get()
	{
		var category = await CreateClient().For<Category>().Key(1).FindEntryAsync();
		category!.CategoryID.Should().Be(1);
	}

	[Fact]
	public async Task GetNonExisting()
	{
		(await CreateClient().For<Category>().Key(-1).FindEntryAsync()).Should().BeNull();
		var act = () => CreateClient(x => x.IgnoreResourceNotFoundException = false).For<Category>().Key(-1).FindEntryAsync();
		await act.Should().ThrowAsync<WebRequestException>();
	}

	[Fact]
	public async Task GetByGuidKey()
	{
		var item = await CreateClient().For<TypeWithGuidKey>().Key(Seed.FirstGuidKey).FindEntryAsync();
		item!.Name.Should().Be("first");
		var filtered = await CreateClient().For<TypeWithGuidKey>().Filter(x => x.Key == Seed.FirstGuidKey).FindEntryAsync();
		filtered!.Name.Should().Be("first");
	}

	[Fact]
	public async Task GetByCompoundKey()
	{
		var detail = await CreateClient().For<OrderDetail>("Order_Details").Key(10248, 11).FindEntryAsync();
		detail!.Quantity.Should().Be(12);
		var byObject = await CreateClient().For<OrderDetail>("Order_Details").Key(new OrderDetail { OrderID = 10248, ProductID = 11 }).FindEntryAsync();
		byObject!.ProductID.Should().Be(11);
	}

	[Fact]
	public async Task GetByAlternateKey()
	{
		var product = await CreateClient().For<Product>().Key(new { ProductName = "Chai" }).FindEntryAsync();
		product!.ProductID.Should().Be(1);
	}

	[Fact]
	public async Task Singleton()
	{
		var company = await CreateClient().For<Company>().FindEntryAsync();
		company!.Name.Should().Be("Northwind Traders");
		company.Headquarters!.Type.Should().Be(AddressType.Work);
	}

	[Fact]
	public async Task ContainedNavigation()
	{
		var addresses = await CreateClient().For<Customer>().Key("ALFKI").NavigateTo(x => x.Addresses).FindEntriesAsync();
		addresses.Should().HaveCount(2);
	}

	[Fact]
	public async Task OpenTypeDynamicProperties()
	{
		var animals = await CreateClient().For<Animal>("Animals").WithProperties(x => x.DynamicProperties).FindEntriesAsync();
		animals.Should().HaveCount(2);
		animals[0].DynamicProperties["Legs"].Should().Be(4);
		animals[1].DynamicProperties["CanFly"].Should().Be(true);
	}

	[Fact]
	public async Task SelectSingle()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").Select(x => x.ProductName).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
		product.ProductID.Should().Be(0);
	}

	[Fact]
	public async Task SelectMultiple()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").Select(x => new { x.ProductID, x.ProductName }).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
		product.ProductID.Should().Be(1);
	}

	[Fact]
	public async Task ExpandOne()
	{
		var products = await CreateClient().For<Product>().OrderBy(x => x.ProductID).Expand(x => x.Category).FindEntriesAsync();
		products[^1].Category.CategoryName.Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandManyAsArray()
	{
		var category = await CreateClient().For<Category>().Expand(x => x.Products).Filter(x => x.CategoryName == "Beverages").FindEntryAsync();
		category!.Products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandManyAsList()
	{
		var category = await CreateClient().For<CategoryWithList>("Categories").Expand(x => x.Products).Filter(x => x.CategoryName == "Beverages").FindEntryAsync();
		category!.Products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandManyAsIList()
	{
		var category = await CreateClient().For<CategoryWithIList>("Categories").Expand(x => x.Products).Filter(x => x.CategoryName == "Beverages").FindEntryAsync();
		category!.Products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandManyAsHashSet()
	{
		var category = await CreateClient().For<CategoryWithHashSet>("Categories").Expand(x => x.Products).Filter(x => x.CategoryName == "Beverages").FindEntryAsync();
		category!.Products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandManyAsICollection()
	{
		var category = await CreateClient().For<CategoryWithICollection>("Categories").Expand(x => x.Products).Filter(x => x.CategoryName == "Beverages").FindEntryAsync();
		category!.Products.Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandSecondLevel()
	{
		var products = await CreateClient().For<Product>().OrderBy(x => x.ProductID).Expand(x => x.Category.Products).FindEntriesAsync();
		products[^1].Category.Products.Should().HaveCount(2);
	}

	[Fact]
	public async Task ExpandMultipleLevelsWithCollection()
	{
		var products = await CreateClient().For<Product>().OrderBy(x => x.ProductID).Expand(x => x.Category.Products.Select(y => y.Category)).FindEntriesAsync();
		products[^1].Category.Products[0].Category.CategoryName.Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandWithSelect()
	{
		var products = await CreateClient().For<Product>().OrderBy(x => x.ProductID).Expand(x => x.Category).Select(x => new { x.ProductName, x.Category.CategoryName }).FindEntriesAsync();
		products[^1].Category.CategoryName.Should().Be("Meat/Poultry");
		products[^1].Category.CategoryID.Should().Be(0);
	}

	[Fact]
	public async Task ExpandWithInlineFilterAndOrderBy()
	{
		var category = await CreateClient().For<Category>().Key(2).Expand(x => x.Products.Where(p => p.UnitPrice > 20).OrderByDescending(p => p.UnitPrice)).FindEntryAsync();
		category!.Products.Select(x => x.ProductName).Should().Equal("Northwoods Cranberry Sauce", "Grandma's Boysenberry Spread", "Chef Anton's Cajun Seasoning", "Chef Anton's Gumbo Mix");
	}

	[Fact]
	public async Task OrderBySingle()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").OrderBy(x => x.ProductName).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task OrderByMultiple()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.ProductName == "Chai").OrderBy(x => new { x.ProductID, x.ProductName }).FindEntryAsync();
		product!.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task OrderByExpanded()
	{
		var products = await CreateClient().For<Product>().Expand(x => x.Category).OrderBy(x => new { x.Category.CategoryName }).FindEntriesAsync();
		products[^1].Category.CategoryName.Should().Be("Seafood");
	}

	[Fact]
	public async Task NavigateToSingle()
	{
		var category = await CreateClient().For<Product>().Key(new { ProductID = 2 }).NavigateTo<Category>().FindEntryAsync();
		category!.CategoryName.Should().Be("Beverages");
	}

	[Fact]
	public async Task NavigateToSingleByExpression()
	{
		var category = await CreateClient().For<Product>().Key(new { ProductID = 2 }).NavigateTo(x => x.Category).FindEntryAsync();
		category!.CategoryName.Should().Be("Beverages");
	}

	[Fact]
	public async Task NavigateToMultiple()
	{
		var products = await CreateClient().For<Category>().Key(2).NavigateTo<Product>().FindEntriesAsync();
		products.Should().HaveCount(Seed.CondimentsProducts);
	}

	[Fact]
	public async Task NavigateToRecursive()
	{
		var employee = await CreateClient()
			.For<Employee>()
			.Key(6)
			.NavigateTo<Employee>("Superior")
			.NavigateTo<Employee>("Superior")
			.NavigateTo<Employee>("Subordinates")
			.Key(3)
			.FindEntryAsync();
		employee!.FirstName.Should().Be("Janet");
	}

	[Fact]
	public async Task NavigateToRecursiveByExpression()
	{
		var employee = await CreateClient()
			.For<Employee>()
			.Key(6)
			.NavigateTo(x => x.Superior)
			.NavigateTo(x => x.Superior)
			.NavigateTo(x => x.Subordinates)
			.Key(3)
			.FindEntryAsync();
		employee!.FirstName.Should().Be("Janet");
	}

	[Fact]
	public async Task NavigateToRecursiveSingleClause()
	{
		var employee = await CreateClient().For<Employee>().Key(6).NavigateTo(x => x.Superior.Superior.Subordinates).Key(3).FindEntryAsync();
		employee!.FirstName.Should().Be("Janet");
	}

	[Fact]
	public async Task BaseClassEntries()
	{
		var transport = await CreateClient().For<Transport>().FindEntriesAsync();
		transport.Should().HaveCount(Seed.Transports);
	}

	[Fact]
	public async Task BaseClassEntriesMaterializeDerivedTypes()
	{
		var transport = await CreateClient(x => x.IncludeAnnotationsInResults = true).For<Transport>().FindEntriesAsync();
		transport.OfType<Ship>().Should().HaveCount(Seed.Ships);
		transport.OfType<Truck>().Should().ContainSingle().Which.TruckNumber.Should().Be("TR-1");
	}

	[Fact]
	public async Task AllDerivedClassEntries()
	{
		var ships = await CreateClient().For<Transport>().As<Ship>().FindEntriesAsync();
		ships.Should().HaveCount(Seed.Ships);
		ships[0].ShipName.Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntry()
	{
		var ship = await CreateClient().For<Transport>().As<Ship>().Filter(x => x.ShipName == "Titanic").FindEntryAsync();
		ship!.ShipName.Should().Be("Titanic");
	}

	[Fact]
	public async Task BaseClassEntryByKey()
	{
		var transport = await CreateClient().For<Transport>().Key(1).FindEntryAsync();
		transport!.TransportID.Should().Be(1);
	}

	[Fact]
	public async Task DerivedClassEntryByKey()
	{
		var ship = await CreateClient().For<Transport>().As<Ship>().Key(1).FindEntryAsync();
		ship!.ShipName.Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntryBaseAndDerivedFields()
	{
		var ship = await CreateClient().For<Transport>().As<Ship>().Filter(x => x.TransportID == 1 && x.ShipName == "Titanic").FindEntryAsync();
		ship!.ShipName.Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfDerivedClassEntry()
	{
		var ship = await CreateClient().For<Transport>().Filter(x => x is Ship).As<Ship>().FindEntryAsync();
		ship!.ShipName.Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfAssociation()
	{
		var employee = await CreateClient().For<Employee>().Filter(x => x.Superior is Employee).FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task CastToPrimitiveType()
	{
		var product = await CreateClient().For<Product>().Filter(x => x.CategoryID == (int)1L).FindEntryAsync();
		product.Should().NotBeNull();
	}

	[Fact]
	public async Task CastInstanceToEntityType()
	{
		var employee = await CreateClient().For<Employee>().Filter(x => x != null).FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task CastPropertyToEntityType()
	{
		var employee = await CreateClient().For<Employee>().Filter(x => x.Superior != null).FindEntryAsync();
		employee.Should().NotBeNull();
	}

	[Fact]
	public async Task FilterAny()
	{
		var orders = await CreateClient().For<Order>().Filter(x => x.OrderDetails.Any(y => y.Quantity > 30)).FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAnyDetailOver30);
	}

	[Fact]
	public async Task FilterAll()
	{
		var orders = await CreateClient().For<Order>().Filter(x => x.OrderDetails.All(y => y.Quantity >= 10)).FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAllDetailsAtLeast10);
	}

	[Fact]
	public async Task NameMatchResolverStrict()
	{
		var client = CreateClient(x => x.NameMatchResolver = ODataNameMatchResolver.Strict);
		var details = await client.For<Order_Details>().FindEntriesAsync();
		details.Should().NotBeEmpty();
		var act = () => client.For<OrderDetails>().FindEntriesAsync();
		await act.Should().ThrowAsync<UnresolvableObjectException>();
	}

	[Fact]
	public async Task NameMatchResolverNotStrict()
	{
		var client = CreateClient(x => x.NameMatchResolver = ODataNameMatchResolver.NotStrict);
		var details1 = await client.For<Order_Details>().FindEntriesAsync();
		details1.Should().NotBeEmpty();
		details1[0].OrderID.Should().BeGreaterThan(0);
		var details2 = await client.For<OrderDetails>().FindEntriesAsync();
		details2.Should().NotBeEmpty();
		details2[0].Order_ID.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task NameMatchResolverCaseInsensitive()
	{
		var client = CreateClient(x => x.NameMatchResolver = ODataNameMatchResolver.AlphanumericCaseInsensitive);
		var details = await client.For<orderDetails>().FindEntriesAsync();
		details.Should().NotBeEmpty();
		details[0].orderID.Should().BeGreaterThan(0);
	}

	private sealed class OrderDetails
	{
		public int Order_ID { get; set; }
		public int product_id { get; set; }
		public int quantity { get; set; }
	}

	private sealed class orderDetails
	{
		public int orderID { get; set; }
		public int productID { get; set; }
		public int quantity { get; set; }
	}
}
