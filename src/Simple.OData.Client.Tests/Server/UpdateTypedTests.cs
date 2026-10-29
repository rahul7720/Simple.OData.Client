using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class UpdateTypedTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task UpdateByKey()
	{
		var client = CreateClient();
		var product = await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Key(product!.ProductID).Set(new { UnitPrice = 123m }).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync();
		product!.UnitPrice.Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByFilter()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Filter(x => x.ProductName == "Test1").Set(new { UnitPrice = 123m }).UpdateEntryAsync();
		var product = await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync();
		product!.UnitPrice.Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleWithResult()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var products = await client.For<Product>().Filter(x => x.ProductName == "Test1").Set(new { UnitPrice = 123m }).UpdateEntriesAsync();
		products.Single().UnitPrice.Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleNoResult()
	{
		var client = CreateClient();
		await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		var products = await client.For<Product>().Filter(x => x.ProductName == "Test1").Set(new { UnitPrice = 123m }).UpdateEntriesAsync(false);
		products.Should().BeEmpty();
		var product = await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync();
		product!.UnitPrice.Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByObjectAsKey()
	{
		var client = CreateClient();
		var product = await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Key(product!).Set(new { UnitPrice = 456m }).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync();
		product!.UnitPrice.Should().Be(456m);
	}

	[Fact]
	public async Task UpdateObjectValue()
	{
		var client = CreateClient();
		var product = await client.For<Product>().Set(new { ProductName = "Test1", UnitPrice = 18m }).InsertEntryAsync();
		product!.UnitPrice = 456m;
		await client.For<Product>().Key(product).Set(product).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductName == "Test1").FindEntryAsync();
		product!.UnitPrice.Should().Be(456m);
	}

	[Fact]
	public async Task UpdateDate()
	{
		var client = CreateClient();
		var today = new DateTime(2018, 5, 20, 20, 30, 40, 677, DateTimeKind.Utc);
		var tomorrow = today.AddDays(1);
		var employee = await client.For<Employee>().Set(new { FirstName = "Test1", LastName = "Test1", HireDate = today }).InsertEntryAsync();
		await client.For<Employee>().Key(employee!.EmployeeID).Set(new { HireDate = tomorrow }).UpdateEntryAsync();
		employee = await client.For<Employee>().Key(employee.EmployeeID).FindEntryAsync();
		employee!.HireDate.ToUniversalTime().Should().Be(tomorrow);
	}

	[Fact]
	public async Task AddSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test1" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Test2", UnitPrice = 18m }).InsertEntryAsync();
		await client.For<Product>().Key(product!.ProductID).Set(new { Category = category }).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductID == product.ProductID).FindEntryAsync();
		product!.CategoryID.Should().Be(category!.CategoryID);
		category = await client.For<Category>().Filter(x => x.CategoryID == category.CategoryID).Expand(x => x.Products).FindEntryAsync();
		category!.Products.Should().ContainSingle();
	}

	[Fact]
	public async Task UpdateSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test1" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Test2", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		await client.For<Product>().Key(product!.ProductID).Set(new { Category = category }).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductID == product.ProductID).FindEntryAsync();
		product!.CategoryID.Should().Be(category!.CategoryID);
		category = await client.For<Category>().Filter(x => x.CategoryID == category.CategoryID).Expand(x => x.Products).FindEntryAsync();
		category!.Products.Should().ContainSingle();
	}

	[Fact]
	public async Task RemoveSingleAssociation()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test6" }).InsertEntryAsync();
		var product = await client.For<Product>().Set(new { ProductName = "Test7", UnitPrice = 18m, Category = category }).InsertEntryAsync();
		await client.For<Product>().Key(product!.ProductID).Set(new { Category = (Category?)null }).UpdateEntryAsync();
		product = await client.For<Product>().Filter(x => x.ProductID == product.ProductID).FindEntryAsync();
		product!.CategoryID.Should().BeNull();
	}

	[Fact]
	public async Task UpdateMultipleAssociations()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test3" }).InsertEntryAsync();
		var product1 = await client.For<Product>().Set(new { ProductName = "Test4", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		var product2 = await client.For<Product>().Set(new { ProductName = "Test5", UnitPrice = 18m, CategoryID = 1 }).InsertEntryAsync();
		await client.For<Category>().Key(category!.CategoryID).Set(new { Products = new[] { product1, product2 } }).UpdateEntryAsync();
		category = await client.For<Category>().Filter(x => x.CategoryID == category.CategoryID).Expand(x => x.Products).FindEntryAsync();
		category!.Products.Should().HaveCount(2);
	}

	[Fact]
	public async Task UpdateProductWithoutExpandingCategory()
	{
		var client = CreateClient();
		var category = await client.For<Category>().Set(new { CategoryName = "Test5" }).InsertEntryAsync();
		await client.For<Product>().Set(new { ProductName = "Test6", UnitPrice = 18m, CategoryID = category!.CategoryID }).InsertEntryAsync();
		var product = await client.For<ProductWithNoCategoryLink>("Products").Filter(x => x.ProductName == "Test6").FindEntryAsync();
		product!.ProductName = "Test7";
		await client.For<ProductWithNoCategoryLink>("Products").Key(product.ProductID).Set(product).UpdateEntryAsync();
		product = await client.For<ProductWithNoCategoryLink>("Products").Key(product.ProductID).FindEntryAsync();
		product!.ProductName.Should().Be("Test7");
		product.CategoryID.Should().Be(category.CategoryID);
	}

	[Fact]
	public async Task UpdateDerived()
	{
		var client = CreateClient();
		var ship = await client.For<Transport>().As<Ship>().Set(new { ShipName = "Test1" }).InsertEntryAsync();
		ship = await client.For<Transport>().As<Ship>().Key(ship!.TransportID).Set(new { ShipName = "Test2" }).UpdateEntryAsync();
		ship!.ShipName.Should().Be("Test2");
	}

	[Fact]
	public async Task UpdateComplexPropertyAndEnum()
	{
		var client = CreateClient();
		await client.For<Customer>().Key("ALFKI").Set(new { Address = new Address { City = "Munich", Type = AddressType.Shipping } }).UpdateEntryAsync();
		var customer = await client.For<Customer>().Key("ALFKI").FindEntryAsync();
		customer!.Address!.City.Should().Be("Munich");
		customer.Address.Type.Should().Be(AddressType.Shipping);
	}
}
