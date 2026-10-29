using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Spatial;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Expressions;

public class TypedExpressionTests
{
	private readonly Session _session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private string Format(Expression<Func<TestEntity, bool>> filter) => ODataExpression.FromLinqExpression(filter).AsString(_session);

	[Fact]
	public void And() => Format(x => x.ProductID == 1 && x.ProductName == "Chai").Should().Be("ProductID eq 1 and ProductName eq 'Chai'");

	[Fact]
	public void Or() => Format(x => x.ProductName == "Chai" || x.ProductID == 1).Should().Be("ProductName eq 'Chai' or ProductID eq 1");

	[Fact]
	public void Not() => Format(x => !(x.ProductName == "Chai")).Should().Be("not (ProductName eq 'Chai')");

	[Fact]
	public void Precedence() => Format(x => (x.ProductID == 1 || x.ProductID == 2) && x.ProductName == "Chai").Should().Be("(ProductID eq 1 or ProductID eq 2) and ProductName eq 'Chai'");

	[Fact]
	public void EqualString() => Format(x => x.ProductName == "Chai").Should().Be("ProductName eq 'Chai'");

	private sealed class NameSource
	{
		public string GetName() => "Chai";

		public string WithSuffix(string prefix) => prefix + "ai";
	}

	[Fact]
	public void ClosureInstanceMethodCallIsEvaluated()
	{
		var source = new NameSource();
		Format(x => x.ProductName == source.GetName()).Should().Be("ProductName eq 'Chai'");
	}

	[Fact]
	public void ClosureMethodCallWithArgumentsIsEvaluated()
	{
		var source = new NameSource();
		var prefix = "Ch";
		Format(x => x.ProductName == source.WithSuffix(prefix).ToUpperInvariant()).Should().Be("ProductName eq 'CHAI'");
	}

	[Fact]
	public void EqualFieldToString() => Format(x => x.ProductName!.ToString() == "Chai").Should().Be("ProductName eq 'Chai'");

	[Fact]
	public void EqualValueToString()
	{
		var name = "Chai";
		Format(x => x.ProductName!.ToString() == name.ToString()).Should().Be("ProductName eq 'Chai'");
	}

	[Theory]
	[InlineData(1, "ProductID eq 1")]
	public void EqualNumeric(int value, string expected) => Format(x => x.ProductID == value).Should().Be(expected);

	[Fact]
	public void ComparisonOperators()
	{
		Format(x => x.ProductID != 1).Should().Be("ProductID ne 1");
		Format(x => x.ProductID > 1).Should().Be("ProductID gt 1");
		Format(x => x.ProductID >= 1.5).Should().Be("ProductID ge 1.5");
		Format(x => x.ProductID < 1).Should().Be("ProductID lt 1");
		Format(x => x.ProductID <= 1).Should().Be("ProductID le 1");
	}

	[Fact]
	public void ArithmeticOperators()
	{
		Format(x => x.ProductID + 1 == 2).Should().Be("ProductID add 1 eq 2");
		Format(x => x.ProductID - 1 == 2).Should().Be("ProductID sub 1 eq 2");
		Format(x => x.ProductID * 1 == 2).Should().Be("ProductID mul 1 eq 2");
		Format(x => x.ProductID / 1 == 2).Should().Be("ProductID div 1 eq 2");
		Format(x => x.ProductID % 1 == 2).Should().Be("ProductID mod 1 eq 2");
	}

	[Fact]
	public void NumericLiteralsHaveNoSuffixes()
	{
		Format(x => x.ProductID == 1L).Should().Be("ProductID eq 1");
		Format(x => x.Price == 1M).Should().Be("Price eq 1");
		Format(x => x.Price == 1.23M).Should().Be("Price eq 1.23");
	}

	[Fact]
	public void EqualGuid() => Format(x => x.LinkID == Guid.Empty).Should().Be("LinkID eq 00000000-0000-0000-0000-000000000000");

	[Fact]
	public void EqualDateTimeIsWrittenAsDateTimeOffset() =>
		Format(x => x.CreationTime == new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should().Be("CreationTime eq 2013-01-01T00:00:00Z");

	[Fact]
	public void EqualDateTimeOffset() =>
		Format(x => x.Updated == new DateTimeOffset(new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc))).Should().Be("Updated eq 2013-01-01T00:00:00Z");

	[Fact]
	public void EqualDateOnly() => Format(x => x.ShipDate == new DateOnly(2024, 2, 29)).Should().Be("ShipDate eq 2024-02-29");

	[Fact]
	public void EqualTimeSpan() => Format(x => x.Period == new TimeSpan(1, 2, 3)).Should().Be("Period eq duration'PT1H2M3S'");

	[Fact]
	public void StringFunctions()
	{
		Format(x => x.ProductName!.Length == 4).Should().Be("length(ProductName) eq 4");
		Format(x => x.ProductName!.ToLower() == "chai").Should().Be("tolower(ProductName) eq 'chai'");
		Format(x => x.ProductName!.ToUpper() == "CHAI").Should().Be("toupper(ProductName) eq 'CHAI'");
		Format(x => x.ProductName!.StartsWith("Ch") == true).Should().Be("startswith(ProductName,'Ch') eq true");
		Format(x => x.ProductName!.EndsWith("Ch") == true).Should().Be("endswith(ProductName,'Ch') eq true");
		Format(x => x.ProductName!.Contains("ai") == true).Should().Be("contains(ProductName,'ai') eq true");
		Format(x => x.ProductName!.Contains("ai") == false).Should().Be("contains(ProductName,'ai') eq false");
		Format(x => x.ProductName!.Contains("ai")).Should().Be("contains(ProductName,'ai')");
		Format(x => "Chai".Contains(x.ProductName!)).Should().Be("contains('Chai',ProductName)");
		Format(x => !x.ProductName!.Contains("ai")).Should().Be("not contains(ProductName,'ai')");
		Format(x => x.ProductName!.ToLower().Contains("Chai")).Should().Be("contains(tolower(ProductName),'Chai')");
		Format(x => x.ProductName!.IndexOf("ai") == 1).Should().Be("indexof(ProductName,'ai') eq 1");
		Format(x => x.ProductName!.Substring(1) == "hai").Should().Be("substring(ProductName,1) eq 'hai'");
		Format(x => x.ProductName!.Substring(1, 2) == "ha").Should().Be("substring(ProductName,1,2) eq 'ha'");
		Format(x => x.ProductName!.Replace("a", "o") == "Choi").Should().Be("replace(ProductName,'a','o') eq 'Choi'");
		Format(x => x.ProductName!.Trim() == "Chai").Should().Be("trim(ProductName) eq 'Chai'");
		Format(x => string.Concat(x.ProductName, "Chai") == "ChaiChai").Should().Be("concat(ProductName,'Chai') eq 'ChaiChai'");
	}

	[Fact]
	public void DateFunctions()
	{
		Format(x => x.CreationTime.Day == 1).Should().Be("day(CreationTime) eq 1");
		Format(x => x.CreationTime.Month == 2).Should().Be("month(CreationTime) eq 2");
		Format(x => x.CreationTime.Year == 3).Should().Be("year(CreationTime) eq 3");
		Format(x => x.CreationTime.Hour == 4).Should().Be("hour(CreationTime) eq 4");
		Format(x => x.CreationTime.Minute == 5).Should().Be("minute(CreationTime) eq 5");
		Format(x => x.CreationTime.Second == 6).Should().Be("second(CreationTime) eq 6");
		Format(x => x.Updated.Millisecond == 7).Should().Be("fractionalseconds(Updated) eq 7");
		Format(x => x.CreationTime.Date == new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should().Be("date(CreationTime) eq 2013-01-01T00:00:00Z");
		Format(x => x.Updated.TimeOfDay == new TimeSpan(1, 2, 3)).Should().Be("time(Updated) eq duration'PT1H2M3S'");
	}

	[Fact]
	public void MathFunctions()
	{
		Format(x => decimal.Round(x.Price) == 1).Should().Be("round(Price) eq 1");
		Format(x => decimal.Floor(x.Price) == 1).Should().Be("floor(Price) eq 1");
		Format(x => decimal.Ceiling(x.Price) == 2).Should().Be("ceiling(Price) eq 2");
	}

	[Fact]
	public void NestedProperties()
	{
		Format(x => x.Nested!.ProductID == 1).Should().Be("Nested/ProductID eq 1");
		Format(x => x.Nested!.ProductName!.Length == 4).Should().Be("length(Nested/ProductName) eq 4");
	}

	[Fact]
	public void ConvertEqual()
	{
		var id = "1";
		Format(x => x.Nested!.ProductID == Convert.ToInt32(id)).Should().Be("Nested/ProductID eq 1");
	}

	[Fact]
	public void MappedPropertyNames()
	{
		Format(x => x.MappedNameUsingColumnAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingDataAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingDataMemberAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingOtherAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingDataMemberAndOtherAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingJsonPropertyAttribute == "Milk").Should().Be("Name eq 'Milk'");
		Format(x => x.MappedNameUsingJsonPropertyNameAttribute == "Milk").Should().Be("Name eq 'Milk'");
	}

	[Fact]
	public void MappedPropertyOfOtherEntityIsEvaluated()
	{
		var otherEntity = new TestEntity { MappedNameUsingDataMemberAttribute = "Other Name" };
		Format(x => x.MappedNameUsingDataMemberAttribute == otherEntity.MappedNameUsingDataMemberAttribute).Should().Be("Name eq 'Other Name'");
	}

	[Fact]
	public void Enums()
	{
		var local = AddressType.Corporate;
		Format(x => x.Address!.Type == AddressType.Corporate).Should().Be("Address/Type eq NorthwindModel.AddressType'Corporate'");
		Format(x => x.Address!.Type == local).Should().Be("Address/Type eq NorthwindModel.AddressType'Corporate'");
		Format(x => x.Address!.Type.HasFlag(AddressType.Corporate)).Should().Be("Address/Type has NorthwindModel.AddressType'Corporate'");
		Format(x => x.Address!.Type.ToString() == AddressType.Corporate.ToString()).Should().Be("Address/Type eq NorthwindModel.AddressType'Corporate'");
	}

	[Fact]
	public void EnumPrefixFree()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s => s.EnumPrefixFree = true);
		Expression<Func<TestEntity, bool>> filter = x => x.Address!.Type == AddressType.Corporate;
		ODataExpression.FromLinqExpression(filter).AsString(session).Should().Be("Address/Type eq 'Corporate'");
	}

	[Fact]
	public void DateTimeRange()
	{
		var before = new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var after = new DateTime(2014, 2, 2, 0, 0, 0, DateTimeKind.Utc);
		Format(x => x.CreationTime >= before && x.CreationTime < after).Should().Be("CreationTime ge 2013-01-01T00:00:00Z and CreationTime lt 2014-02-02T00:00:00Z");
	}

	[Fact]
	public void InOperator()
	{
		var ids = new List<int> { 1, 2, 3 };
		var names = new List<string> { "Chai", "Milk", "Water" };
		var categories = new List<string> { "chai", "milk", "water" };
		Format(x => ids.Contains(x.ProductID)).Should().Be("(ProductID in (1,2,3))");
		Format(x => names.Contains(x.ProductName!)).Should().Be("(ProductName in ('Chai','Milk','Water'))");
		Format(x => categories.Contains(x.Nested!.ProductName!)).Should().Be("(Nested/ProductName in ('chai','milk','water'))");
		Format(x => categories.Contains(x.ProductName!.ToLower())).Should().Be("(tolower(ProductName) in ('chai','milk','water'))");
		Format(x => !ids.Contains(x.ProductID)).Should().Be("not (ProductID in (1,2,3))");
		Format(x => ids.Contains(x.ProductID) && !names.Contains(x.ProductName!)).Should().Be("(ProductID in (1,2,3)) and not (ProductName in ('Chai','Milk','Water'))");
	}

	[Fact]
	public void ExpressionBuilder()
	{
		Expression<Predicate<TestEntity>> condition1 = x => x.ProductName == "Chai";
		Expression<Func<TestEntity, bool>> condition2 = x => x.ProductID == 1;
		var filter = new ODataExpression(condition1) || new ODataExpression(condition2);
		filter.AsString(_session).Should().Be("ProductName eq 'Chai' or ProductID eq 1");

		var generic = new ODataExpression<TestEntity>(x => x.ProductName == "Chai") || new ODataExpression<TestEntity>(x => x.ProductID == 1);
		generic.AsString(_session).Should().Be("ProductName eq 'Chai' or ProductID eq 1");
	}

	[Fact]
	public void ExpressionBuilderGrouping()
	{
		var filter1 = new ODataExpression<TestEntity>(x => x.ProductName == "Chai") || new ODataExpression<TestEntity>(x => x.ProductID == 1);
		var filter2 = new ODataExpression<TestEntity>(x => x.ProductName == "Kaffe") || new ODataExpression<TestEntity>(x => x.ProductID == 2);
		(filter1 && filter2).AsString(_session).Should().Be("(ProductName eq 'Chai' or ProductID eq 1) and (ProductName eq 'Kaffe' or ProductID eq 2)");
	}

	[Fact]
	public void StringLiteralQuotesAreDoubledAndUnicodeIsKept()
	{
		Format(x => x.ProductName == "O'Brien").Should().Be("ProductName eq 'O''Brien'");
		Format(x => x.ProductName == "Übergröße").Should().Be("ProductName eq 'Übergröße'");
	}

	[Fact]
	public void NullComparison() => Format(x => x.ProductName == null).Should().Be("ProductName eq null");

	[Fact]
	public void FourOhOneFunctions()
	{
		Format(x => Regex.IsMatch(x.ProductName!, "^Ch")).Should().Be("matchesPattern(ProductName,'^Ch')");
		Format(x => ODataFunctions.MatchesPattern(x.ProductName!, "^Ch")).Should().Be("matchesPattern(ProductName,'^Ch')");
		Format(x => ODataFunctions.HasSubset(x.Tags!, new[] { "a" })).Should().Be("hassubset(Tags,['a'])");
		Format(x => ODataFunctions.HasSubsequence(x.Tags!, new[] { "a", "b" })).Should().Be("hassubsequence(Tags,['a','b'])");
		Format(x => ODataFunctions.GeoDistance(x.Location!, GeographyPoint.Create(1, 2)) < 10).Should().Be("geo.distance(Location,geography'SRID=4326;POINT (2 1)') lt 10.0");
		Format(x => x.Updated < ODataFunctions.Now()).Should().Be("Updated lt now()");
		Format(x => x.Updated > ODataFunctions.MinDateTime()).Should().Be("Updated gt mindatetime()");
		Format(x => ODataFunctions.TotalOffsetMinutes(x.Updated) == 60).Should().Be("totaloffsetminutes(Updated) eq 60");
		Format(x => ODataFunctions.TotalSeconds(x.Period) > 1).Should().Be("totalseconds(Period) gt 1");
	}

	[Fact]
	public void ODataFunctionsThrowWhenInvokedLocally()
	{
		var act = () => ODataFunctions.MatchesPattern("a", "b");
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void LambdaVariablesAreNumberedPerExpression()
	{
		var employees = _session.Metadata.GetEntityCollection("Employees");
		Expression<Func<Employee, bool>> first = x => x.Subordinates.Any(y => y.EmployeeID == 1);
		Expression<Func<Employee, bool>> second = x => x.Subordinates.All(y => y.FirstName == "abc");

		ODataExpression.FromLinqExpression(first).AsString(_session, employees).Should().Be("Subordinates/any(x1:x1/EmployeeID eq 1)");
		ODataExpression.FromLinqExpression(second).AsString(_session, employees).Should().Be("Subordinates/all(x1:x1/FirstName eq 'abc')");
	}

	[Fact]
	public void NestedLambdasGetIncreasingNumbers()
	{
		var employees = _session.Metadata.GetEntityCollection("Employees");
		Expression<Func<Employee, bool>> filter = x => x.Subordinates.Any(y => y.Subordinates.Any(z => z.EmployeeID == 1));

		ODataExpression.FromLinqExpression(filter).AsString(_session, employees).Should().Be("Subordinates/any(x1:x1/Subordinates/any(x2:x2/EmployeeID eq 1))");
	}

	[Fact]
	public void IsOfAndCast()
	{
		var transports = _session.Metadata.GetEntityCollection("Transport");
		Expression<Func<Transport, bool>> isOf = x => x is Ship;
		Expression<Func<Transport, bool>> cast = x => (x as Ship)!.ShipName == "Titanic";

		ODataExpression.FromLinqExpression(isOf).AsString(_session, transports).Should().Be("isof('NorthwindModel.Ships')");
		ODataExpression.FromLinqExpression(cast).AsString(_session, transports).Should().Be("cast('NorthwindModel.Ships')/ShipName eq 'Titanic'");
	}

	[Fact]
	public void UnsupportedExpressionThrowsNotSupported()
	{
		Expression<Func<TestEntity, bool>> filter = x => x.ProductName!.Equals("a", StringComparison.Ordinal);
		var act = () => ODataExpression.FromLinqExpression(filter).AsString(_session);
		act.Should().Throw<NotSupportedException>();
	}
}
