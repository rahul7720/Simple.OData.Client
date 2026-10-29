using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Dynamic C# query scenarios against the in-process server.</summary>
public sealed class FindDynamicTests(TestServerFixture fixture) : ServerTestBase(fixture)
{
	private static readonly dynamic X = ODataDynamic.Expression;

	[Fact]
	public async Task SingleCondition()
	{
		var x = X;
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task SingleConditionWithVariable()
	{
		var x = X;
		var name = "Chai";
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == name).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task CombinedConditions()
	{
		var x = X;
		dynamic employee = await CreateClient().For(x.Employees).Filter(x.FirstName == "Nancy" && x.HireDate < DateTime.Now).FindEntryAsync();
		((string)employee.LastName).Should().Be("Davolio");
	}

	[Fact]
	public async Task CombinedConditionsWithMultipleFilters()
	{
		var x = X;
		dynamic employee = await CreateClient().For(x.Employees).Filter(x.FirstName == "Nancy").Filter(x.HireDate < DateTime.Now).FindEntryAsync();
		((string)employee.LastName).Should().Be("Davolio");
	}

	[Fact]
	public async Task CombineAll()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient()
			.For(x.Products)
			.OrderBy(x.ProductName)
			.ThenByDescending(x.UnitPrice)
			.Skip(2)
			.Top(1)
			.Expand(x.Category)
			.Select(x.Category)
			.FindEntriesAsync();
		((string)products.Single().Category.CategoryName).Should().Be("Seafood");
	}

	[Fact]
	public async Task CombineAllReverse()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient()
			.For(x.Products)
			.Select(x.Category)
			.Expand(x.Category)
			.Top(1)
			.Skip(2)
			.OrderBy(x.ProductName)
			.ThenByDescending(x.UnitPrice)
			.FindEntriesAsync();
		((string)products.Single().Category.CategoryName).Should().Be("Seafood");
	}

	[Fact]
	public async Task StringContains()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName.Contains("ai")).FindEntriesAsync();
		((string)products.Single().ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task StringContainsWithLocalVariable()
	{
		var x = X;
		var text = "ai";
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName.Contains(text)).FindEntriesAsync();
		((string)products.Single().ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task StringNotContains()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(!x.ProductName.Contains("ai")).FindEntriesAsync();
		products.Should().HaveCount(Seed.Products - 1);
	}

	[Fact]
	public async Task StringStartsWith()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName.StartsWith("Ch")).FindEntriesAsync();
		((string)products.First().ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task LengthOfStringEqual()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName.Length == 4).FindEntriesAsync();
		((string)products.First().ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task SubstringWithPositionAndLengthEqual()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName.Substring(1, 2) == "ha").FindEntriesAsync();
		((string)products.First().ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task TopOne()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").Top(1).FindEntriesAsync();
		products.Should().ContainSingle();
	}

	[Fact]
	public async Task Count()
	{
		var x = X;
		int count = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").Count().FindScalarAsync<int>();
		count.Should().Be(1);
	}

	[Fact]
	public async Task Get()
	{
		var x = X;
		dynamic category = await CreateClient().For(x.Categories).Key(1).FindEntryAsync();
		((int)category.CategoryID).Should().Be(1);
	}

	[Fact]
	public async Task GetNonExisting()
	{
		var x = X;
		dynamic? category = await CreateClient().For(x.Categories).Key(-1).FindEntryAsync();
		((object?)category).Should().BeNull();
	}

	[Fact]
	public async Task SelectSingle()
	{
		var x = X;
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").Select(x.ProductName).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task SelectMultiple()
	{
		var x = X;
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").Select(x.ProductID, x.ProductName).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
		((int)product.ProductID).Should().Be(1);
	}

	[Fact]
	public async Task ExpandOne()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).OrderBy(x.ProductID).Expand(x.Category).FindEntriesAsync();
		((string)products.Last().Category.CategoryName).Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandMany()
	{
		var x = X;
		dynamic category = await CreateClient().For(x.Categories).Expand(x.Products).Filter(x.CategoryName == "Beverages").FindEntryAsync();
		((IEnumerable<dynamic>)category.Products).Should().HaveCount(Seed.BeveragesProducts);
	}

	[Fact]
	public async Task ExpandSecondLevel()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).OrderBy(x.ProductID).Expand(x.Category.Products).FindEntriesAsync();
		((IEnumerable<dynamic>)products.Last().Category.Products).Should().HaveCount(2);
	}

	[Fact]
	public async Task ExpandMultipleLevelsWithCollection()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).OrderBy(x.ProductID).Expand(x.Category.Products.Category).FindEntriesAsync();
		((string)((IEnumerable<dynamic>)products.Last().Category.Products).First().Category.CategoryName).Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task ExpandWithSelect()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).OrderBy(x.ProductID).Expand(x.Category).Select(x.ProductName, x.Category.CategoryName).FindEntriesAsync();
		((string)products.Last().Category.CategoryName).Should().Be("Meat/Poultry");
	}

	[Fact]
	public async Task OrderBySingle()
	{
		var x = X;
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").OrderBy(x.ProductName).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task OrderByMultiple()
	{
		var x = X;
		dynamic product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").OrderBy(x.ProductID, x.ProductName).FindEntryAsync();
		((string)product.ProductName).Should().Be("Chai");
	}

	[Fact]
	public async Task OrderByExpanded()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Products).Expand(x.Category).OrderBy(x.Category.CategoryName).FindEntriesAsync();
		((string)products.Last().Category.CategoryName).Should().Be("Seafood");
	}

	[Fact]
	public async Task NavigateToSingle()
	{
		var x = X;
		dynamic category = await CreateClient().For(x.Products).Key(2).NavigateTo(x.Category).FindEntryAsync();
		((string)category.CategoryName).Should().Be("Beverages");
	}

	[Fact]
	public async Task NavigateToMultiple()
	{
		var x = X;
		IEnumerable<dynamic> products = await CreateClient().For(x.Categories).Key(2).NavigateTo(x.Products).FindEntriesAsync();
		products.Should().HaveCount(Seed.CondimentsProducts);
	}

	[Fact]
	public async Task NavigateToRecursive()
	{
		var x = X;
		dynamic employee = await CreateClient()
			.For(x.Employees)
			.Key(6)
			.NavigateTo(x.Superior)
			.NavigateTo(x.Superior)
			.NavigateTo(x.Subordinates)
			.Key(3)
			.FindEntryAsync();
		((string)employee.FirstName).Should().Be("Janet");
	}

	[Fact]
	public async Task NavigateToRecursiveSingleClause()
	{
		var x = X;
		dynamic employee = await CreateClient().For(x.Employees).Key(6).NavigateTo(x.Superior.Superior.Subordinates).Key(3).FindEntryAsync();
		((string)employee.FirstName).Should().Be("Janet");
	}

	[Fact]
	public async Task BaseClassEntries()
	{
		var x = X;
		IEnumerable<dynamic> transport = await CreateClient().For(x.Transport).FindEntriesAsync();
		transport.Should().HaveCount(Seed.Transports);
	}

	[Fact]
	public async Task AllDerivedClassEntries()
	{
		var x = X;
		IEnumerable<dynamic> ships = await CreateClient().For(x.Transport).As(x.Ship).FindEntriesAsync();
		ships.Should().HaveCount(Seed.Ships);
		((string)ships.First().ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntry()
	{
		var x = X;
		dynamic ship = await CreateClient().For(x.Transport).As(x.Ship).Filter(x.ShipName == "Titanic").FindEntryAsync();
		((string)ship.ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task BaseClassEntryByKey()
	{
		var x = X;
		dynamic transport = await CreateClient().For(x.Transport).Key(1).FindEntryAsync();
		((string)transport.ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntryByKey()
	{
		var x = X;
		dynamic ship = await CreateClient().For(x.Transport).As(x.Ship).Key(1).FindEntryAsync();
		((string)ship.ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task DerivedClassEntryBaseAndDerivedFields()
	{
		var x = X;
		dynamic ship = await CreateClient().For(x.Transport).As(x.Ship).Filter(x.TransportID == 1 && x.ShipName == "Titanic").FindEntryAsync();
		((string)ship.ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfDerivedClassEntry()
	{
		var x = X;
		dynamic ship = await CreateClient().For(x.Transport).Filter(x.Is(x.Ship)).As(x.Ship).FindEntryAsync();
		((string)ship.ShipName).Should().Be("Titanic");
	}

	[Fact]
	public async Task IsOfAssociation()
	{
		var x = X;
		dynamic employee = await CreateClient().For(x.Employees).Filter(x.Superior.Is(x.Employee)).FindEntryAsync();
		((object)employee).Should().NotBeNull();
	}

	[Fact]
	public async Task FilterAny()
	{
		var x = X;
		IEnumerable<dynamic> orders = await CreateClient().For(x.Orders).Filter(x.Order_Details.Any(x.Quantity > 30)).FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAnyDetailOver30);
	}

	[Fact]
	public async Task FilterAll()
	{
		var x = X;
		IEnumerable<dynamic> orders = await CreateClient().For(x.Orders).Filter(x.Order_Details.All(x.Quantity >= 10)).FindEntriesAsync();
		orders.Should().HaveCount(Seed.OrdersWithAllDetailsAtLeast10);
	}

	[Fact]
	public async Task OpenTypeDynamicProperties()
	{
		var x = X;
		dynamic animal = await CreateClient().For(x.Animals).Key(1).FindEntryAsync();
		((int)animal.Legs).Should().Be(4);
		((string)animal.Sound).Should().Be("Woof");
	}

	[Fact]
	public async Task EnumFilter()
	{
		var x = X;
		IEnumerable<dynamic> customers = await CreateClient().For(x.Customers).Filter(x.Address.Type == AddressType.Work).FindEntriesAsync();
		customers.Should().HaveCount(Seed.WorkAddressCustomers);
	}

	[Fact]
	public async Task ImplicitCastToType()
	{
		var x = X;
		Product product = await CreateClient().For(x.Products).Filter(x.ProductName == "Chai").FindEntryAsync();
		product.ProductName.Should().Be("Chai");
	}

	[Fact]
	public async Task ImplicitCastToTypeNullResult()
	{
		var x = X;
		Product? product = await CreateClient().For(x.Products).Filter(x.ProductName == "XYZ").FindEntryAsync();
		product.Should().BeNull();
	}
}
