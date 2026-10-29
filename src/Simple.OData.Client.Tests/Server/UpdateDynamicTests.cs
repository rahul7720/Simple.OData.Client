using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

public sealed class UpdateDynamicTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	[Fact]
	public async Task UpdateByKey()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Key(product.ProductID).Set(x.UnitPrice = 123m).UpdateEntryAsync();
		product = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByFilter()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Filter(x.ProductName == "Test1").Set(x.UnitPrice = 123m).UpdateEntryAsync();
		dynamic product = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleWithResult()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		IEnumerable<dynamic> products = await client.For(x.Products).Filter(x.ProductName == "Test1").Set(x.UnitPrice = 123m).UpdateEntriesAsync();
		((decimal)products.Single().UnitPrice).Should().Be(123m);
	}

	[Fact]
	public async Task UpdateMultipleNoResult()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		IEnumerable<dynamic> products = await client.For(x.Products).Filter(x.ProductName == "Test1").Set(x.UnitPrice = 123m).UpdateEntriesAsync(false);
		products.Should().BeEmpty();
		dynamic product = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(123m);
	}

	[Fact]
	public async Task UpdateByObjectAsKey()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test1", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Key(product).Set(x.UnitPrice = 456m).UpdateEntryAsync();
		product = await client.For(x.Products).Filter(x.ProductName == "Test1").FindEntryAsync();
		((decimal)product.UnitPrice).Should().Be(456m);
	}

	[Fact]
	public async Task UpdateDate()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		var today = new DateTimeOffset(2018, 5, 20, 20, 30, 40, 677, TimeSpan.Zero);
		var tomorrow = today.AddDays(1);
		dynamic employee = await client.For(x.Employees).Set(x.FirstName = "Test1", x.LastName = "Test1", x.HireDate = today).InsertEntryAsync();
		await client.For(x.Employees).Key(employee.EmployeeID).Set(x.HireDate = tomorrow).UpdateEntryAsync();
		employee = await client.For(x.Employees).Key(employee.EmployeeID).FindEntryAsync();
		((DateTimeOffset)employee.HireDate).Should().Be(tomorrow);
	}

	[Fact]
	public async Task AddSingleAssociation()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test1").InsertEntryAsync();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test2", x.UnitPrice = 18m).InsertEntryAsync();
		await client.For(x.Products).Key(product.ProductID).Set(x.Category = category).UpdateEntryAsync();
		product = await client.For(x.Products).Filter(x.ProductID == product.ProductID).FindEntryAsync();
		((int)product.CategoryID).Should().Be((int)category.CategoryID);
		category = await client.For(x.Categories).Filter(x.CategoryID == category.CategoryID).Expand(x.Products).FindEntryAsync();
		((IEnumerable<dynamic>)category.Products).Should().ContainSingle();
	}

	[Fact]
	public async Task UpdateSingleAssociation()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test1").InsertEntryAsync();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test2", x.UnitPrice = 18m, x.CategoryID = 1).InsertEntryAsync();
		await client.For(x.Products).Key(product.ProductID).Set(x.Category = category).UpdateEntryAsync();
		product = await client.For(x.Products).Filter(x.ProductID == product.ProductID).FindEntryAsync();
		((int)product.CategoryID).Should().Be((int)category.CategoryID);
	}

	[Fact]
	public async Task RemoveSingleAssociation()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test6").InsertEntryAsync();
		dynamic product = await client.For(x.Products).Set(x.ProductName = "Test7", x.UnitPrice = 18m, x.Category = category).InsertEntryAsync();
		await client.For(x.Products).Key(product.ProductID).Set(x.Category = null).UpdateEntryAsync();
		product = await client.For(x.Products).Filter(x.ProductID == product.ProductID).FindEntryAsync();
		((object?)product.CategoryID).Should().BeNull();
	}

	[Fact]
	public async Task UpdateMultipleAssociations()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic category = await client.For(x.Categories).Set(x.CategoryName = "Test3").InsertEntryAsync();
		dynamic product1 = await client.For(x.Products).Set(x.ProductName = "Test4", x.UnitPrice = 18m, x.CategoryID = 1).InsertEntryAsync();
		dynamic product2 = await client.For(x.Products).Set(x.ProductName = "Test5", x.UnitPrice = 18m, x.CategoryID = 1).InsertEntryAsync();
		await client.For(x.Categories).Key(category.CategoryID).Set(x.Products = new[] { product1, product2 }).UpdateEntryAsync();
		category = await client.For(x.Categories).Filter(x.CategoryID == category.CategoryID).Expand(x.Products).FindEntryAsync();
		((IEnumerable<dynamic>)category.Products).Should().HaveCount(2);
	}

	[Fact]
	public async Task UpdateDerived()
	{
		var x = ODataDynamic.Expression;
		var client = CreateClient();
		dynamic ship = await client.For(x.Transport).As(x.Ship).Set(x.ShipName = "Test1").InsertEntryAsync();
		ship = await client.For(x.Transport).As(x.Ship).Key(ship.TransportID).Set(x.ShipName = "Test2").UpdateEntryAsync();
		((string)ship.ShipName).Should().Be("Test2");
	}
}
