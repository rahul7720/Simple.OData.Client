using Simple.OData.Client.Commands;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Commands;

public sealed class KeyResolutionTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4WithAlternateKeys.xml");

	private static string Format(FluentCommand command, Session? session = null) => command.Resolve(session ?? Session).Format();

	[Fact]
	public void PrimaryKey_SingleProperty_NoNames() =>
		Format(new FluentCommand().For("Categories").Key(1)).Should().Be("Categories(1)");

	[Fact]
	public void PrimaryKey_SingleProperty_Named() =>
		Format(new FluentCommand().For("Categories").Key(new { CategoryID = 1 })).Should().Be("Categories(1)");

	[Fact]
	public void PrimaryKey_SingleProperty_Dictionary() =>
		Format(new FluentCommand().For("Categories").Key(new Dictionary<string, object?> { ["CategoryID"] = 1 })).Should().Be("Categories(1)");

	[Fact]
	public void PrimaryKey_MultipleProperty_NoNames() =>
		Format(new FluentCommand().For("Order_Details").Key(1, 2)).Should().Be("Order_Details(OrderID=1,ProductID=2)");

	[Fact]
	public void PrimaryKey_MultipleProperty_Named_UsesDeclaredOrder() =>
		Format(new FluentCommand().For("Order_Details").Key(new { ProductID = 2, OrderID = 1 })).Should().Be("Order_Details(OrderID=1,ProductID=2)");

	[Fact]
	public void PrimaryKey_MultipleProperty_FromEntity() =>
		Format(new FluentCommand().For("Order_Details").Key(new OrderDetail { ProductID = 2, OrderID = 1, Quantity = 5 })).Should().Be("Order_Details(OrderID=1,ProductID=2)");

	[Fact]
	public void PrimaryKey_SingleProperty_FromEntity() =>
		Format(new FluentCommand().For("Categories").Key(new Category { CategoryID = 1 })).Should().Be("Categories(1)");

	[Fact]
	public void PrimaryKey_SingleProperty_FromFilter() =>
		Format(new FluentCommand().For("Categories").Filter(new ODataExpression<Category>(x => x.CategoryID == 1))).Should().Be("Categories(1)");

	[Fact]
	public void PrimaryKey_SingleProperty_NoMatchingFilter() =>
		Format(new FluentCommand().For("Categories").Filter(new ODataExpression<Category>(x => x.CategoryID == 1 && x.CategoryName == "Test")))
			.Should().Be("Categories?$filter=CategoryID%20eq%201%20and%20CategoryName%20eq%20'Test'");

	[Fact]
	public void PrimaryKey_NavigationKeyFilterStaysFilter()
	{
		var session = MetadataClientFactory.CreateSession("KeyDemo.xml");
		Format(new FluentCommand().For("Activities").Filter(new ODataExpression<Activity>(x => x.Ticket.Id == 1)), session)
			.Should().Be("Activities?$filter=Ticket%2FId%20eq%201");
		Format(new FluentCommand().For("Activities").Filter(new ODataExpression<Activity>(x => x.Option.Id == 1)), session)
			.Should().Be("Activities?$filter=Option%2FId%20eq%201");
	}

	[Fact]
	public void AlternateKey_SingleProperty() =>
		Format(new FluentCommand().For("Categories").Key(new { CategoryName = "Beverages" })).Should().Be("Categories(CategoryName='Beverages')");

	[Fact]
	public void AlternateKey_SingleProperty_AsFilter() =>
		Format(new FluentCommand().For("Categories").Filter(new ODataExpression<Category>(x => x.CategoryName == "Beverages")))
			.Should().Be("Categories(CategoryName='Beverages')");

	[Fact]
	public void AlternateKey_MultipleProperty() =>
		Format(new FluentCommand().For("Employees").Key(new { HomePhone = "123", Title = "Manager" }))
			.Should().Be("Employees(HomePhone='123',Title='Manager')");

	[Fact]
	public void AlternateKey_MultipleProperty_FromFilter() =>
		Format(new FluentCommand().For("Employees").Filter(new ODataExpression<Employee>(x => x.HomePhone == "123" && x.Title == "Manager")))
			.Should().Be("Employees(HomePhone='123',Title='Manager')");

	[Fact]
	public void AlternateKey_MultipleKeys()
	{
		Format(new FluentCommand().For("Orders").Key(new { CustomerID = "ALFKI" })).Should().Be("Orders(CustomerID='ALFKI')");
		Format(new FluentCommand().For("Orders").Key(new { ShipName = "TEST" })).Should().Be("Orders(ShipName='TEST')");
	}

	[Fact]
	public void NamedValuesContainingKeyUseOnlyKeyProperties() =>
		Format(new FluentCommand().For("Categories").Key(new { CategoryID = 1, Description = "x" })).Should().Be("Categories(1)");

	[Fact]
	public void GuidKeyIsNotQuoted()
	{
		var key = Guid.Parse("7d8f3a1e-0c6b-4d7a-9f5e-2b1c3d4e5f60");
		Format(new FluentCommand().For("TypeWithGuidKey").Key(key)).Should().Be("TypeWithGuidKey(7d8f3a1e-0c6b-4d7a-9f5e-2b1c3d4e5f60)");
	}

	[Fact]
	public void KeyValueCountMismatchThrows()
	{
		var act = () => Format(new FluentCommand().For("Order_Details").Key(1));
		act.Should().Throw<InvalidOperationException>().WithMessage("*Order_Details*2 key*");
	}

	[Fact]
	public void TopOtherThanOneWithKeyThrows()
	{
		var act = () => new FluentCommand().For("Categories").Key(1).Top(2);
		act.Should().Throw<InvalidOperationException>();
		new FluentCommand().For("Categories").Key(1).Top(1).Details.TopCount.Should().Be(-1);
	}

	[Fact]
	public void KeyAsSegment_SingleKey()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4WithAlternateKeys.xml", s => s.KeyAsSegment = true);
		Format(new FluentCommand().For("Products").Key(1), session).Should().Be("Products/1");
		Format(new FluentCommand().For("Customers").Key("ALFKI"), session).Should().Be("Customers/ALFKI");
		Format(new FluentCommand().For("Customers").Key("A/B C"), session).Should().Be("Customers/A%2FB%20C");
	}

	[Fact]
	public void KeyAsSegment_CompoundAndAlternateKeysStayParenthesized()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4WithAlternateKeys.xml", s => s.KeyAsSegment = true);
		Format(new FluentCommand().For("Order_Details").Key(1, 2), session).Should().Be("Order_Details(OrderID=1,ProductID=2)");
		Format(new FluentCommand().For("Categories").Key(new { CategoryName = "Beverages" }), session).Should().Be("Categories(CategoryName='Beverages')");
	}

	[Fact]
	public void KeyAsSegment_KeepsFollowingSegments()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4WithAlternateKeys.xml", s => s.KeyAsSegment = true);
		var parent = new FluentCommand().For("Categories").Key(1);
		Format(new FluentCommand(parent, null).Link("Products"), session).Should().Be("Categories/1/Products");
	}

	[Fact]
	public void ResolvedKeyValuesUseDeclaredNames()
	{
		var resolved = new FluentCommand().For("Order_Details").Key(new { productId = 2, orderId = 1 }).Resolve(Session);
		resolved.KeyValues.Should().Equal(new Dictionary<string, object?> { ["OrderID"] = 1, ["ProductID"] = 2 });
		resolved.FilterAsKey.Should().Equal(resolved.KeyValues);
	}
}
