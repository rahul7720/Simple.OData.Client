using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Fluent;

public sealed class CommandTextTests
{
	private static readonly ODataClient Client = MetadataClientFactory.CreateClient("Northwind4.xml");

	private static Task<string> Text<T>(IBoundClient<T> command) where T : class => command.GetCommandTextAsync();

	/// <summary>Dynamically bound chains yield <c>dynamic</c>; extension methods such as Should() cannot be dispatched on them.</summary>
	private static async Task Check(dynamic command, string expected)
	{
		string text = await command.GetCommandTextAsync();
		text.Should().Be(expected);
	}

	// ---- expansion ------------------------------------------------------------------------------------------

	[Fact]
	public async Task ExpandSubordinates() =>
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates))).Should().Be("Employees?$expand=Subordinates");

	[Fact]
	public async Task ExpandAll()
	{
		(await Text(Client.For<Employee>().Expand("*"))).Should().Be("Employees?$expand=*");
		(await Text(Client.For<Employee>().Expand("*").Select("*"))).Should().Be("Employees?$expand=*&$select=*");
	}

	[Fact]
	public async Task ExpandSubordinatesAndSuperior()
	{
		(await Text(Client.For<Employee>().Expand(x => new { x.Subordinates, x.Superior }))).Should().Be("Employees?$expand=Subordinates,Superior");
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates).Expand(x => x.Superior))).Should().Be("Employees?$expand=Subordinates,Superior");
	}

	[Fact]
	public async Task ExpandNestedSubordinates()
	{
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates.Select(y => y.Subordinates)))).Should().Be("Employees?$expand=Subordinates($expand=Subordinates)");
		(await Text(Client.For<Employee>().Expand("Subordinates/*"))).Should().Be("Employees?$expand=Subordinates($expand=*)");
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates.Select(y => y.Subordinates.Select(z => z.Subordinates)))))
			.Should().Be("Employees?$expand=Subordinates($expand=Subordinates($expand=Subordinates))");
		(await Text(Client.For<Employee>().Expand("Subordinates/Subordinates/*"))).Should().Be("Employees?$expand=Subordinates($expand=Subordinates($expand=*))");
	}

	[Fact]
	public async Task ExpandWithSelectAndOrderBy()
	{
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates).Select(x => new { x.LastName, x.Subordinates }).OrderBy(x => x.LastName)))
			.Should().Be("Employees?$expand=Subordinates&$select=LastName&$orderby=LastName");
		(await Text(Client.For<Employee>().Expand("*").Select(x => new { x.LastName, x.Subordinates }).OrderBy(x => x.LastName)))
			.Should().Be("Employees?$expand=*&$select=LastName&$orderby=LastName");
		(await Text(Client.For<Employee>()
				.Expand(x => x.Subordinates.Select(y => y.Subordinates))
				.Select(x => new { x.LastName, x.Subordinates })
				.Select(x => x.Subordinates.Select(y => new { y.LastName, y.Subordinates }))))
			.Should().Be("Employees?$expand=Subordinates($select=LastName;$expand=Subordinates)&$select=LastName");
		(await Text(Client.For<Employee>()
				.Expand(x => x.Subordinates.Select(y => y.Subordinates))
				.Select(x => new { x.LastName, x.Subordinates })
				.Select(x => x.Subordinates.Select(y => new { y.LastName, y.Subordinates }))
				.OrderBy(x => x.LastName)
				.OrderBy(x => x.Subordinates.Select(y => y.LastName))))
			.Should().Be("Employees?$expand=Subordinates($select=LastName;$orderby=LastName;$expand=Subordinates)&$select=LastName&$orderby=LastName");
		(await Text(Client.For<Employee>()
				.Expand(x => x.Subordinates.Select(y => y.Subordinates.Select(z => z.Subordinates)))
				.Select(x => new { x.LastName, x.Subordinates })
				.Select(x => x.Subordinates.Select(y => new { y.LastName, y.Subordinates }))
				.Select(x => x.Subordinates.Select(y => y.Subordinates.Select(z => new { z.LastName, z.Subordinates })))
				.OrderBy(x => x.LastName)
				.OrderBy(x => x.Subordinates.Select(y => y.LastName))
				.OrderBy(x => x.Subordinates.Select(y => y.Subordinates.Select(z => z.LastName)))))
			.Should().Be("Employees?$expand=Subordinates($select=LastName;$orderby=LastName;$expand=Subordinates($select=LastName;$orderby=LastName;$expand=Subordinates))&$select=LastName&$orderby=LastName");
	}

	[Fact]
	public void ExpandWithInnerOrderByInSelectThrows()
	{
		var act = () => Client.For<Employee>()
			.Expand(x => x.Subordinates.Select(y => y.Subordinates))
			.Select(x => x.Subordinates.Select(y => new { y.LastName, y.Subordinates }).OrderBy(y => y.LastName));
		act.Should().Throw<NotSupportedException>();
	}

	[Fact]
	public async Task ExpandWithDeepOrderBy()
	{
		(await Text(Client.For<Employee>().Expand(x => x.Subordinates).Select(x => new { x.LastName, x.Subordinates }).OrderBy(x => x.Superior.LastName)))
			.Should().Be("Employees?$expand=Subordinates&$select=LastName&$orderby=Superior%2FLastName");
		(await Text(Client.For<Employee>().Select(x => new { x.LastName, x.Subordinates }).OrderBy(x => x.Superior.LastName)))
			.Should().Be("Employees?$select=LastName,Subordinates&$orderby=Superior%2FLastName");
	}

	[Fact]
	public async Task ExpandCategoryWithSelectAndOrderBy()
	{
		(await Text(Client.For<Product>().Expand(p => p.Category).Select(p => new { p.ProductName, p.Category.CategoryName })))
			.Should().Be("Products?$expand=Category($select=CategoryName)&$select=ProductName");
		(await Text(Client.For<Product>().Expand("*").Select(p => new { p.ProductName, p.Category.CategoryName })))
			.Should().Be("Products?$expand=*&$select=ProductName");
		(await Text(Client.For<Product>().Expand(p => p.Category).Select(p => new { p.ProductName, p.Category.CategoryName }).OrderBy(p => p.Category.CategoryName).ThenBy(p => p.ProductName)))
			.Should().Be("Products?$expand=Category($select=CategoryName)&$select=ProductName&$orderby=Category%2FCategoryName,ProductName");
	}

	[Fact]
	public async Task ExpandSuperiorChains()
	{
		(await Text(Client.For<Employee>().Expand(x => x.Superior.Subordinates))).Should().Be("Employees?$expand=Superior($expand=Subordinates)");
		(await Text(Client.For<Employee>().Expand(x => x.Superior).Expand(x => x.Superior.Subordinates).Expand(x => x.Superior.Superior)))
			.Should().Be("Employees?$expand=Superior($expand=Subordinates,Superior)");
		(await Text(Client.For<Employee>().Expand(x => x.Superior.Superior.Superior))).Should().Be("Employees?$expand=Superior($expand=Superior($expand=Superior))");
	}

	[Fact]
	public async Task ExpandWithLevels()
	{
		(await Text(Client.For<Employee>().Expand(ODataExpandOptions.ByValue(2), x => x.Subordinates))).Should().Be("Employees?$expand=Subordinates($levels=2)");
		(await Text(Client.For<Employee>().Expand(ODataExpandOptions.ByReference(2), x => x.Subordinates))).Should().Be("Employees?$expand=Subordinates/$ref($levels=2)");
	}

	[Fact]
	public async Task ExpandWithInlineFilterOrderByAndSelect()
	{
		(await Text(Client.For<Category>().Expand(x => x.Products.Where(p => p.UnitPrice > 18)))).Should().Be("Categories?$expand=Products($filter=UnitPrice%20gt%2018)");
		(await Text(Client.For<Category>().Expand(x => x.Products.OrderBy(p => p.UnitPrice).ThenBy(p => p.ProductName)))).Should().Be("Categories?$expand=Products($orderby=UnitPrice,ProductName)");
		(await Text(Client.For<Category>().Expand(x => x.Products.OrderBy(p => p.Category.CategoryName)))).Should().Be("Categories?$expand=Products($orderby=Category%2FCategoryName)");
		(await Text(Client.For<Category>().Expand(x => x.Products.OrderByDescending(p => p.UnitPrice).ThenByDescending(p => p.ProductName))))
			.Should().Be("Categories?$expand=Products($orderby=UnitPrice%20desc,ProductName%20desc)");
		(await Text(Client.For<Category>()
				.Expand(x => x.Products.Select(p => new { p.ProductName, p.UnitPrice }))
				.Expand(x => x.Products.OrderByDescending(p => p.UnitPrice).ThenByDescending(p => p.ProductName))))
			.Should().Be("Categories?$expand=Products($select=ProductName,UnitPrice;$orderby=UnitPrice%20desc,ProductName%20desc)");

		const string expected = "Employees?$expand=Superior($expand=Subordinates($orderby=LastName;$expand=Superior))";
		(await Text(Client.For<Employee>().Expand(x => x.Superior.Subordinates.Select(s => s.Superior)).Expand(x => x.Superior.Subordinates.OrderBy(s => s.LastName)))).Should().Be(expected);
		(await Text(Client.For<Employee>().Expand(x => x.Superior.Subordinates.OrderBy(s => s.LastName)).Expand(x => x.Superior.Subordinates.Select(s => s.Superior)))).Should().Be(expected);
		(await Text(Client.For<Employee>().Expand(x => x.Superior.Subordinates.OrderBy(s => s.LastName).Select(s => s.Superior)))).Should().Be(expected);
	}

	[Fact]
	public async Task ExpandOnFunctionResult()
	{
		var x = ODataDynamic.Expression;
		var client = MetadataClientFactory.CreateClient("TripPin.xml");
		string text = await client.For(x.Person).Key("scottketchum").NavigateTo(x.Trip).Key(0).Function("GetInvolvedPeople").Expand(x.Photos).GetCommandTextAsync();
		text.Should().Be("People('scottketchum')/Trips(0)/Microsoft.OData.SampleService.Models.TripPin.GetInvolvedPeople()?$expand=Photo");
	}

	[Fact]
	public async Task ExpandFunctionMultipleLevelsWithSelect()
	{
		var client = MetadataClientFactory.CreateClient("ClientProductSku.xml");
		string[] expand =
		[
			"Product/ProductCategory/Category/CategorySalesArea",
			"ClientProductSkuPriceList",
			"ClientProductSkuSalesArea",
			"Product/SupplierProductSkuClient/SupplierProductSku/SupplierProductSkuPriceList/SupplierPriceList",
			"Product/SupplierProductSkuClient/SupplierProductSku/SupplierProductSkuOnHand/Warehouse",
		];
		string[] select =
		[
			"PartNo", "ClientId", "ErpName", "EanCode",
			"Product/Id", "Product/ManufacturerId",
			"Product/ProductCategory/IsPrimary",
			"Product/ProductCategory/Category/Code",
			"ClientProductSkuPriceList/CurrencyId",
		];
		var text = await client.For("ClientProductSku").Function("GetCreateUpdateSkuDelta").Set(new { clientId = 35, offsetInMinutes = 2000 }).Expand(expand).Select(select).GetCommandTextAsync();
		text.Should().Be("ClientProductSkus/FunctionService.GetCreateUpdateSkuDelta(clientId=35,offsetInMinutes=2000)?" +
			"$expand=Product($select=Id,ManufacturerId;$expand=ProductCategory($select=IsPrimary;$expand=Category($select=Code;$expand=CategorySalesArea)),SupplierProductSkuClient($expand=SupplierProductSku($expand=SupplierProductSkuPriceList($expand=SupplierPriceList),SupplierProductSkuOnHand($expand=Warehouse))))," +
			"ClientProductSkuPriceList($select=CurrencyId)," +
			"ClientProductSkuSalesArea&" +
			"$select=PartNo,ClientId,ErpName,EanCode");
	}

	// ---- any/all ----------------------------------------------------------------------------------------------

	[Fact]
	public async Task FilterAnyAll()
	{
		(await Text(Client.For<Employee>().Filter(x => x.Subordinates.Any(y => y.EmployeeID == 1)))).Should().Be("Employees?$filter=Subordinates%2Fany%28x1%3Ax1%2FEmployeeID%20eq%201%29");
		(await Text(Client.For<Employee>().Filter(x => x.Subordinates.Any(y => y.EmployeeID == 1 && y.FirstName == "abc"))))
			.Should().Be("Employees?$filter=Subordinates%2Fany%28x1%3Ax1%2FEmployeeID%20eq%201%20and%20x1%2FFirstName%20eq%20'abc'%29");
		(await Text(Client.For<Employee>().Filter(x => x.Subordinates.Any(y => y.EmployeeID == 1 && y.FirstName.Contains("abc")))))
			.Should().Be("Employees?$filter=Subordinates%2Fany%28x1%3Ax1%2FEmployeeID%20eq%201%20and%20contains%28x1%2FFirstName%2C'abc'%29%29");
		(await Text(Client.For<Product>().Filter(x => x.Category.Products.Any(y => y.ProductID == 1)))).Should().Be("Products?$filter=Category%2FProducts%2Fany%28x1%3Ax1%2FProductID%20eq%201%29");
		(await Text(Client.For<Product>().Filter(x => x.Category.Products.Any(y => y.Category.Products.Any(z => z.ProductID == 1)))))
			.Should().Be("Products?$filter=Category%2FProducts%2Fany%28x1%3Ax1%2FCategory%2FProducts%2Fany%28x2%3Ax2%2FProductID%20eq%201%29%29");
	}

	// ---- typed filter as key ----------------------------------------------------------------------------------

	[Fact]
	public async Task TypedFilterAsKey()
	{
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1))).Should().Be("Products(1)");
		(await Text(Client.For<Product>().Filter(x => x.ProductID != 1))).Should().Be("Products?$filter=ProductID%20ne%201");
		(await Text(Client.For<Product>().Filter(x => x.ProductID != 1).Filter(x => x.ProductID != 2))).Should().Be("Products?$filter=ProductID%20ne%201%20and%20ProductID%20ne%202");
		(await Text(Client.For<Product>().Filter(x => x.ProductID != 1 || x.ProductID != 2).Filter(x => x.ProductID != 3)))
			.Should().Be("Products?$filter=%28ProductID%20ne%201%20or%20ProductID%20ne%202%29%20and%20ProductID%20ne%203");
		(await Text(Client.For<Product>().Filter(x => !(x.ProductID == 1)))).Should().Be("Products?$filter=not%20%28ProductID%20eq%201%29");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1L))).Should().Be("Products(1)");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1 && x.ProductName == "abc"))).Should().Be("Products?$filter=ProductID%20eq%201%20and%20ProductName%20eq%20'abc'");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1 && x.ProductID == 1))).Should().Be("Products(1)");
		(await Text(Client.For<OrderDetail>().Filter(x => x.OrderID == 1 && x.Quantity == 1))).Should().Be("Order_Details?$filter=OrderID%20eq%201%20and%20Quantity%20eq%201");
		(await Text(Client.For<OrderDetail>().Filter(x => x.OrderID == 1 && x.ProductID == 2))).Should().Be("Order_Details(OrderID=1,ProductID=2)");
		(await Text(Client.For<OrderDetail>().Filter(x => x.OrderID == 1))).Should().Be("Order_Details?$filter=OrderID%20eq%201");
		(await Text(Client.For<Category>().Key(1).Filter(x => x.CategoryName == "Beverages"))).Should().Be("Categories(1)?$filter=CategoryName%20eq%20'Beverages'");
	}

	[Fact]
	public async Task KeyLikeFilterStaysAFilterWhenTheQueryIsACollectionQuery()
	{
		// $count, $orderby, $skip, $search, $count=true, $apply and $compute address the collection, so the filter must not collapse into a key
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).Count())).Should().Be("Products/$count?$filter=ProductID%20eq%201");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).OrderBy(x => x.ProductName))).Should().Be("Products?$filter=ProductID%20eq%201&$orderby=ProductName");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).Skip(1))).Should().Be("Products?$filter=ProductID%20eq%201&$skip=1");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).WithCount())).Should().Be("Products?$filter=ProductID%20eq%201&$count=true");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).Search("tea"))).Should().Be("Products?$filter=ProductID%20eq%201&$search=tea");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).Top(1))).Should().Be("Products(1)");
		(await Text(Client.For<Product>().Filter(x => x.ProductID == 1).Select(x => x.ProductName).Expand(x => x.Category))).Should().Be("Products(1)?$expand=Category&$select=ProductName");
	}

	[Fact]
	public async Task TypedFilterWithDateTimeOffset()
	{
		var created = new DateTimeOffset(2010, 12, 1, 12, 11, 10, TimeSpan.Zero);
		(await Text(Client.For<Order>().Filter(x => x.ShippedDateTimeOffset > created))).Should().Be("Orders?$filter=ShippedDateTimeOffset%20gt%202010-12-01T12%3A11%3A10Z");
		var createdDateTime = new DateTime(2010, 12, 1, 12, 11, 10, DateTimeKind.Utc);
		(await Text(Client.For<Order>().Filter(x => x.ShippedDateTimeOffset > createdDateTime))).Should().Be("Orders?$filter=ShippedDateTimeOffset%20gt%202010-12-01T12%3A11%3A10Z");
	}

	[Fact]
	public async Task TypedFilterWithContainsBecomesIn()
	{
		var ids = new List<int> { 1, 2, 3 };
		(await Text(Client.For<Product>().Filter(x => ids.Contains(x.ProductID)))).Should().Be("Products?$filter=%28ProductID%20in%20%281%2C2%2C3%29%29");
	}

	[Fact]
	public async Task TypedNavigation()
	{
		(await Text(Client.For<Employee>().Filter(x => x.EmployeeID == 1).NavigateTo("Superior"))).Should().Be("Employees(1)/Superior");
		(await Text(Client.For<Customer>().Filter(x => x.CustomerID == "ALFKI").NavigateTo<Order>())).Should().Be("Customers('ALFKI')/Orders");
		(await Text(Client.For<Employee>().Filter(x => x.EmployeeID == 2).NavigateTo("Subordinates"))).Should().Be("Employees(2)/Subordinates");
		(await Text(Client.For<Order>().Filter(x => x.OrderID == 10952).NavigateTo<OrderDetail>())).Should().Be("Orders(10952)/Order_Details");
		(await Text(Client.For<Employee>().Key(1).NavigateTo(x => x.Subordinates).Filter(x => x.FirstName == "a"))).Should().Be("Employees(1)/Subordinates?$filter=FirstName%20eq%20'a'");
	}

	[Fact]
	public async Task TypedDerivedTypes()
	{
		(await Text(Client.For<Transport>().Filter(x => x.TransportID == 1))).Should().Be("Transport(1)");
		(await Text(Client.For<Transport>().As<Ship>().Filter(x => x.TransportID == 1))).Should().Be("Transport(1)/NorthwindModel.Ships");
	}

	[Fact]
	public async Task TypedQueryOptions()
	{
		(await Text(Client.For<Product>().Filter(x => x.ProductName == "abc").QueryOptions<QueryOptions>(y => y.IntOption == 42 && y.StringOption == "xyz")))
			.Should().Be("Products?$filter=ProductName%20eq%20'abc'&IntOption=42&StringOption='xyz'");
		(await Text(Client.For<Product>().Filter(x => x.ProductName == "abc").QueryOptions(new Dictionary<string, object?> { ["IntOption"] = 42, ["StringOption"] = "xyz" })))
			.Should().Be("Products?$filter=ProductName%20eq%20'abc'&IntOption=42&StringOption='xyz'");
	}

	[Fact]
	public async Task TypedKeys()
	{
		var key = new Guid("D8F3F70F-C185-49AB-9A92-0C86C344AB1B");
		(await Text(Client.For<TypeWithGuidKey>().Filter(x => x.Key == key))).Should().Be("TypeWithGuidKey(d8f3f70f-c185-49ab-9a92-0c86c344ab1b)");
		(await Text(Client.For<TypeWithGuidKey>().Key(key))).Should().Be("TypeWithGuidKey(d8f3f70f-c185-49ab-9a92-0c86c344ab1b)");
		(await Text(Client.For("project1").Key("abc"))).Should().Be("project1('abc')");
		(await Text(Client.For<Product>().Key(new Product { ProductID = 3 }))).Should().Be("Products(3)");
	}

	[Fact]
	public async Task UnboundFunctionWithCollectionParameter() =>
		(await Client.Unbound().Function("PassThroughIntCollection").Set(new Dictionary<string, object?> { ["numbers"] = new[] { 1, 2, 3 } }).GetCommandTextAsync())
			.Should().Be("PassThroughIntCollection(numbers=@p1)?@p1=[1,2,3]");

	[Fact]
	public async Task UnboundFunctionWithComplexParameterAsDictionary() =>
		(await Client.Unbound().Function("PassThroughAddress").Set(new Dictionary<string, object?> { ["address"] = new Dictionary<string, object?> { ["City"] = "Oslo", ["Country"] = "Norway" } }).GetCommandTextAsync())
			.Should().Be("PassThroughAddress(address=@p1)?@p1=%7B%22City%22%3A%22Oslo%22%2C%22Country%22%3A%22Norway%22%7D", because: "complex parameters travel as a JSON parameter alias");

	[Fact]
	public async Task UnboundFunctionWithComplexParameterAsObject()
	{
		var text = await Client.Unbound().Function("PassThroughAddress").Set(new { address = new Address { City = "Oslo", Country = "Norway", Type = AddressType.Corporate } }).GetCommandTextAsync();
		text.Should().StartWith("PassThroughAddress(address=@p1)?@p1=%7B%22%40odata.type%22%3A%22%23NorthwindModel.Address%22");
		text.Should().Contain("%22City%22%3A%22Oslo%22").And.Contain("%22Type%22%3A%22Corporate%22");
	}

	// ---- dynamic filter as key -------------------------------------------------------------------------------

	[Fact]
	public async Task DynamicFilterAsKey()
	{
		var x = ODataDynamic.Expression;
		await Check(Client.For(x.Products).Filter(x.ProductID == 1), "Products(1)");
		await Check(Client.For(x.Products).Filter(x.ProductID != 1), "Products?$filter=ProductID%20ne%201");
		await Check(Client.For(x.Product).Filter(x.ProductID != 1).Filter(x.ProductID != 2), "Products?$filter=ProductID%20ne%201%20and%20ProductID%20ne%202");
		await Check(Client.For(x.Product).Filter(x.ProductID != 1 || x.ProductID != 2).Filter(x.ProductID != 3), "Products?$filter=%28ProductID%20ne%201%20or%20ProductID%20ne%202%29%20and%20ProductID%20ne%203");
		await Check(Client.For(x.Products).Filter(!(x.ProductID == 1)), "Products?$filter=not%20%28ProductID%20eq%201%29");
		await Check(Client.For(x.Products).Filter(x.ProductID == 1L), "Products(1)");
		await Check(Client.For(x.Products).Filter(x.ProductID == 1 && x.ProductName == "abc"), "Products?$filter=ProductID%20eq%201%20and%20ProductName%20eq%20'abc'");
		await Check(Client.For(x.Products).Filter(x.ProductID == 1 && x.ProductID == 1), "Products(1)");
		await Check(Client.For(x.OrderDetails).Filter(x.OrderID == 1 && x.ProductID == 2), "Order_Details(OrderID=1,ProductID=2)");
		await Check(Client.For(x.OrderDetails).Filter(x.OrderID == 1), "Order_Details?$filter=OrderID%20eq%201");
	}

	[Fact]
	public async Task DynamicNavigationAndDerivedTypes()
	{
		var x = ODataDynamic.Expression;
		await Check(Client.For(x.Employees).Filter(x.EmployeeID == 1).NavigateTo(x.Superior), "Employees(1)/Superior");
		await Check(Client.For(x.Customers).Filter(x.CustomerID == "ALFKI").NavigateTo(x.Orders), "Customers('ALFKI')/Orders");
		await Check(Client.For(x.Employees).Filter(x.EmployeeID == 2).NavigateTo(x.Subordinates), "Employees(2)/Subordinates");
		await Check(Client.For(x.Orders).Filter(x.OrderID == 10952).NavigateTo(x.OrderDetails), "Orders(10952)/Order_Details");
		await Check(Client.For(x.Transport).Filter(x.TransportID == 1), "Transport(1)");
		await Check(Client.For(x.Transport).As(x.Ship).Filter(x.TransportID == 1), "Transport(1)/NorthwindModel.Ships");
		await Check(Client.For(x.Product).Filter(x.ProductName == "abc").QueryOptions(x.IntOption == 42 && x.StringOption == "xyz"), "Products?$filter=ProductName%20eq%20'abc'&IntOption=42&StringOption='xyz'");
	}

	// ---- 4.01 options -----------------------------------------------------------------------------------------

	[Fact]
	public async Task FourOhOneQueryOptions()
	{
		(await Text(Client.For<Product>().Index(3).Compute(x => new { Doubled = x.UnitPrice * 2 }).Search("tea").WithCount()))
			.Should().Be("Products?$search=tea&$index=3&$compute=UnitPrice%20mul%202%20as%20Doubled&$count=true");
		(await Text(Client.For<Product>().Apply(b => b.GroupBy((x, a) => new { x.CategoryID, Total = a.Sum(x.UnitPrice) }))))
			.Should().Be("Products?$apply=groupby%28%28CategoryID%29%2Caggregate%28UnitPrice%20with%20sum%20as%20Total%29%29");
		(await Text(Client.For<Product>().Apply("filter(UnitPrice gt 1)"))).Should().Be("Products?$apply=filter%28UnitPrice%20gt%201%29");
		var x = ODataDynamic.Expression;
		var a = ODataDynamicDataAggregation.AggregationFunction;
		await Check(Client.For(x.Products).Apply(ODataDynamicDataAggregation.Builder.Aggregate(new { Total = a.Sum(x.UnitPrice) })), "Products?$apply=aggregate%28UnitPrice%20with%20sum%20as%20Total%29");
		(await Text(Client.For<Product>().Count())).Should().Be("Products/$count");
	}
}
