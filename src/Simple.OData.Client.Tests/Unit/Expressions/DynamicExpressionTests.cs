using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Expressions;

public sealed class DynamicExpressionTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private static void Check(dynamic filter, string expected)
	{
		string actual = ((ODataExpression)filter).AsString(Session);
		actual.Should().Be(expected);
	}

	[Fact]
	public void LogicalOperators()
	{
		var x = ODataDynamic.Expression;
		Check(x.CategoryID == 1 && x.ProductName == "Chai", "CategoryID eq 1 and ProductName eq 'Chai'");
		Check(x.ProductName == "Chai" || x.CategoryID == 1, "ProductName eq 'Chai' or CategoryID eq 1");
		Check(!(x.ProductName == "Chai"), "not (ProductName eq 'Chai')");
		Check((x.CategoryID == 1 || x.CategoryID == 2) && x.ProductName == "Chai", "(CategoryID eq 1 or CategoryID eq 2) and ProductName eq 'Chai'");
	}

	[Fact]
	public void ComparisonOperators()
	{
		var x = ODataDynamic.Expression;
		Check(x.ProductName == "Chai", "ProductName eq 'Chai'");
		Check(x.CategoryID == 1, "CategoryID eq 1");
		Check(x.CategoryID != 1, "CategoryID ne 1");
		Check(x.CategoryID > 1, "CategoryID gt 1");
		Check(x.CategoryID >= 1.5, "CategoryID ge 1.5");
		Check(x.CategoryID < 1, "CategoryID lt 1");
		Check(x.CategoryID <= 1, "CategoryID le 1");
	}

	[Fact]
	public void ArithmeticOperators()
	{
		var x = ODataDynamic.Expression;
		Check(x.CategoryID + 1 == 2, "CategoryID add 1 eq 2");
		Check(x.CategoryID - 1 == 2, "CategoryID sub 1 eq 2");
		Check(x.CategoryID * 1 == 2, "CategoryID mul 1 eq 2");
		Check(x.CategoryID / 1 == 2, "CategoryID div 1 eq 2");
		Check(x.CategoryID % 1 == 2, "CategoryID mod 1 eq 2");
	}

	[Fact]
	public void Literals()
	{
		var x = ODataDynamic.Expression;
		Check(x.CategoryID == 1L, "CategoryID eq 1");
		Check(x.Total == 1M, "Total eq 1");
		Check(x.Total == 1.23M, "Total eq 1.23");
		Check(x.LinkID == Guid.Empty, "LinkID eq 00000000-0000-0000-0000-000000000000");
		Check(x.Updated == new DateTimeOffset(new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "Updated eq 2013-01-01T00:00:00Z");
		Check(x.Updated == new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Updated eq 2013-01-01T00:00:00Z");
		Check(x.Period == new TimeSpan(1, 2, 3), "Period eq duration'PT1H2M3S'");
		Check(x.ShipDate == new DateOnly(2013, 1, 1), "ShipDate eq 2013-01-01");
	}

	[Fact]
	public void StringFunctions()
	{
		var x = ODataDynamic.Expression;
		Check(x.ProductName.Length() == 4, "length(ProductName) eq 4");
		Check(x.ProductName.Length == 4, "length(ProductName) eq 4");
		Check(x.ProductName.ToLower() == "chai", "tolower(ProductName) eq 'chai'");
		Check(x.ProductName.ToUpper() == "CHAI", "toupper(ProductName) eq 'CHAI'");
		Check(x.ProductName.StartsWith("Ch") == true, "startswith(ProductName,'Ch') eq true");
		Check(x.ProductName.EndsWith("Ch") == true, "endswith(ProductName,'Ch') eq true");
		Check(x.ProductName.Contains("ai") == true, "contains(ProductName,'ai') eq true");
		Check(x.ProductName.Contains("ai") == false, "contains(ProductName,'ai') eq false");
		Check(x.ProductName.Contains("ai"), "contains(ProductName,'ai')");
		Check(!x.ProductName.Contains("ai"), "not contains(ProductName,'ai')");
		Check(x.ProductName.ToLower().Contains("Chai"), "contains(tolower(ProductName),'Chai')");
		Check(x.ProductName.IndexOf("ai") == 1, "indexof(ProductName,'ai') eq 1");
		Check(x.ProductName.Substring(1) == "hai", "substring(ProductName,1) eq 'hai'");
		Check(x.ProductName.Substring(1, 2) == "ha", "substring(ProductName,1,2) eq 'ha'");
		Check(x.ProductName.Replace("a", "o") == "Choi", "replace(ProductName,'a','o') eq 'Choi'");
		Check(x.ProductName.Trim() == "Chai", "trim(ProductName) eq 'Chai'");
		Check(x.ProductName.Concat("Chai") == "ChaiChai", "concat(ProductName,'Chai') eq 'ChaiChai'");
		Check(x.ProductName.MatchesPattern("^Ch"), "matchesPattern(ProductName,'^Ch')");
	}

	[Fact]
	public void DateAndMathFunctions()
	{
		var x = ODataDynamic.Expression;
		Check(x.CreationTime.Day == 1, "day(CreationTime) eq 1");
		Check(x.CreationTime.Month == 2, "month(CreationTime) eq 2");
		Check(x.CreationTime.Year == 3, "year(CreationTime) eq 3");
		Check(x.CreationTime.Hour == 4, "hour(CreationTime) eq 4");
		Check(x.CreationTime.Minute == 5, "minute(CreationTime) eq 5");
		Check(x.CreationTime.Second == 6, "second(CreationTime) eq 6");
		Check(x.CreationTime.Date() == new DateOnly(2013, 1, 1), "date(CreationTime) eq 2013-01-01");
		Check(x.Price.Round() == 1, "round(Price) eq 1");
		Check(x.Price.Floor() == 1, "floor(Price) eq 1");
		Check(x.Price.Ceiling() == 2, "ceiling(Price) eq 2");
	}

	[Fact]
	public void NestedProperty()
	{
		var x = ODataDynamic.Expression;
		Check(x.Nested.ProductID == 1, "Nested/ProductID eq 1");
	}

	[Fact]
	public void EnumComparison()
	{
		var x = ODataDynamic.Expression;
		Check(x.Address.Type == AddressType.Corporate, "Address/Type eq NorthwindModel.AddressType'Corporate'");
		var addressType = AddressType.Corporate;
		Check(x.Address.Type == addressType, "Address/Type eq NorthwindModel.AddressType'Corporate'");
		const AddressType constant = AddressType.Corporate;
		Check(x.Address.Type == constant, "Address/Type eq NorthwindModel.AddressType'Corporate'");
		Check(x.Address.Type != AddressType.Private, "Address/Type ne NorthwindModel.AddressType'Private'");
	}

	[Fact]
	public void AnyAll()
	{
		var x = ODataDynamic.Expression;
		Check(x.Subordinates.Any(x.EmployeeID == 1), "Subordinates/any(x1:x1/EmployeeID eq 1)");
		Check(x.Subordinates.All(x.EmployeeID == 1), "Subordinates/all(x1:x1/EmployeeID eq 1)");
		Check(x.Subordinates.Any(), "Subordinates/any()");
	}

	[Fact]
	public void IsOfAndCast()
	{
		var x = ODataDynamic.Expression;
		Check(x.Is(typeof(Ship)), "isof('NorthwindModel.Ships')");
		Check(x.IsOf(typeof(Ship)), "isof('NorthwindModel.Ships')");
		Check(x.Superior.Is(typeof(Employee)), "isof(Superior,'NorthwindModel.Employee')");
		Check(x.As(typeof(Ship)), "cast('NorthwindModel.Ships')");
		Check(x.Cast(typeof(Ship)).ShipName == "Titanic", "cast('NorthwindModel.Ships')/ShipName eq 'Titanic'");
	}

	[Fact]
	public void SetMemberProducesAssignment()
	{
		var x = ODataDynamic.Expression;
		ODataExpression assignment = x.ProductName = "Chai";
		assignment.Reference.Should().Be("ProductName");
		assignment.Value.Should().Be("Chai");
	}

	[Fact]
	public void FactoryMethods()
	{
		ODataDynamic.ExpressionFromReference("ProductName").AsString(Session).Should().Be("ProductName");
		ODataDynamic.ExpressionFromValue(1).AsString(Session).Should().Be("1");
		ODataDynamic.ExpressionFromFunction("contains", "ProductName", ["ai"]).AsString(Session).Should().Be("contains(ProductName,'ai')");
	}

	[Fact]
	public void UnknownMethodThrows()
	{
		var x = ODataDynamic.Expression;
		var act = () => Check(x.ProductName.Frobnicate("a"), string.Empty);
		act.Should().Throw<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>();
	}
}
