using System.Linq.Expressions;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Commands;

public sealed class CommandUriBuilderTests
{
	private static readonly Session Session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private static string Format(FluentCommand command) => command.Resolve(Session).Format();

	private static ODataExpression Expr<TResult>(Expression<Func<Product, TResult>> expression) => ODataExpression.FromLinqExpression(expression);

	[Fact]
	public void CollectionNameIsResolvedThroughMetadata()
	{
		Format(new FluentCommand().For("Products")).Should().Be("Products");
		Format(new FluentCommand().For("products")).Should().Be("Products");
		Format(new FluentCommand().For("Product")).Should().Be("Products");
	}

	[Fact]
	public void UnknownCollectionThrows()
	{
		var act = () => Format(new FluentCommand().For("NoSuchSet"));
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void FilterTextIsEscaped() =>
		Format(new FluentCommand().For("Products").Filter("ProductName eq 'Chai'")).Should().Be("Products?$filter=ProductName%20eq%20%27Chai%27");

	[Fact]
	public void FilterStringsAreCombined() =>
		Format(new FluentCommand().For("Products").Filter("UnitPrice gt 10").Filter("UnitPrice lt 20"))
			.Should().Be("Products?$filter=%28UnitPrice%20gt%2010%29%20and%20%28UnitPrice%20lt%2020%29");

	[Fact]
	public void FilterExpressionEscapesUnicodeAndQuotes()
	{
		Format(new FluentCommand().For("Products").Filter(new ODataExpression<Product>(x => x.ProductName == "Ü")))
			.Should().Be("Products?$filter=ProductName%20eq%20%27%C3%9C%27");
		Format(new FluentCommand().For("Products").Filter(new ODataExpression<Product>(x => x.ProductName == "O'Brien")))
			.Should().Be("Products?$filter=ProductName%20eq%20%27O%27%27Brien%27");
	}

	[Fact]
	public void FilterFunctionCallIsEscaped() =>
		Format(new FluentCommand().For("Products").Filter(new ODataExpression<Product>(x => x.ProductName.Contains("ai"))))
			.Should().Be("Products?$filter=contains%28ProductName%2C%27ai%27%29");

	[Fact]
	public void SearchIsEscaped() =>
		Format(new FluentCommand().For("Products").Search("blue OR green")).Should().Be("Products?$search=blue%20OR%20green");

	[Fact]
	public void SkipAndTop() =>
		Format(new FluentCommand().For("Products").Skip(10).Top(5)).Should().Be("Products?$skip=10&$top=5");

	[Fact]
	public void Index() =>
		Format(new FluentCommand().For("Products").Index(3)).Should().Be("Products?$index=3");

	[Fact]
	public void ComputeText() =>
		Format(new FluentCommand().For("Products").Compute("UnitPrice mul 2 as Doubled")).Should().Be("Products?$compute=UnitPrice%20mul%202%20as%20Doubled");

	[Fact]
	public void ComputeExpressions() =>
		Format(new FluentCommand().For("Products").Compute([("Doubled", Expr(x => x.UnitPrice * 2)), ("Name", Expr(x => x.ProductName.ToUpperInvariant()))]))
			.Should().Be("Products?$compute=UnitPrice%20mul%202%20as%20Doubled%2Ctoupper%28ProductName%29%20as%20Name");

	[Fact]
	public void OrderByAndThenByDescending() =>
		Format(new FluentCommand().For("Products").OrderBy("ProductName").ThenByDescending("UnitPrice")).Should().Be("Products?$orderby=ProductName,UnitPrice%20desc");

	[Fact]
	public void OrderByResolvesNames() =>
		Format(new FluentCommand().For("Products").OrderByDescending("productname")).Should().Be("Products?$orderby=ProductName%20desc");

	[Fact]
	public void SelectSplitsCommaSeparatedColumns()
	{
		Format(new FluentCommand().For("Products").Select("ProductName", "UnitPrice")).Should().Be("Products?$select=ProductName,UnitPrice");
		Format(new FluentCommand().For("Products").Select("ProductName, UnitPrice")).Should().Be("Products?$select=ProductName,UnitPrice");
	}

	[Fact]
	public void ExpandSingleNavigation() =>
		Format(new FluentCommand().For("Products").Expand("Category")).Should().Be("Products?$expand=Category");

	[Fact]
	public void ExpandWithNestedSelectAndOrderBy()
	{
		Format(new FluentCommand().For("Categories").Expand("Products").Select("CategoryName", "Products/ProductName"))
			.Should().Be("Categories?$expand=Products($select=ProductName)&$select=CategoryName");
		Format(new FluentCommand().For("Categories").Expand("Products").OrderBy("Products/ProductName").ThenByDescending("Products/UnitPrice"))
			.Should().Be("Categories?$expand=Products($orderby=ProductName,UnitPrice%20desc)");
	}

	[Fact]
	public void ExpandAssociationWithOptions()
	{
		var products = new ODataExpandAssociation("Products")
		{
			FilterExpression = new ODataExpression<Product>(x => x.UnitPrice > 10),
			Top = 5,
			Count = true,
		};
		Format(new FluentCommand().For("Categories").Expand([products]))
			.Should().Be("Categories?$expand=Products($filter=UnitPrice%20gt%2010;$top=5;$count=true)");
	}

	[Fact]
	public void ExpandAssociationWithSkipAndSearch()
	{
		var products = new ODataExpandAssociation("Products") { Skip = 2, Search = "blue" };
		Format(new FluentCommand().For("Categories").Expand([products]))
			.Should().Be("Categories?$expand=Products($skip=2;$search=blue)");
	}

	[Fact]
	public void ExpandRawOptionsPassThrough() =>
		Format(new FluentCommand().For("Categories").Expand("Products($filter=UnitPrice gt 10;$top=5)"))
			.Should().Be("Categories?$expand=Products($filter=UnitPrice gt 10;$top=5)");

	[Fact]
	public void ExpandNestedChain()
	{
		Format(new FluentCommand().For("Employees").Expand("Subordinates/Subordinates"))
			.Should().Be("Employees?$expand=Subordinates($expand=Subordinates)");
		Format(new FluentCommand().For("Employees").Expand("Subordinates/Subordinates").Select("LastName", "Subordinates/LastName"))
			.Should().Be("Employees?$expand=Subordinates($select=LastName;$expand=Subordinates)&$select=LastName");
	}

	[Fact]
	public void ExpandStarVariants()
	{
		Format(new FluentCommand().For("Categories").Expand(ODataExpandOptions.ByValue())).Should().Be("Categories?$expand=*");
		Format(new FluentCommand().For("Categories").Expand(ODataExpandOptions.ByReference())).Should().Be("Categories?$expand=*/$ref");
		Format(new FluentCommand().For("Categories").Expand(ODataExpandOptions.ByValue(ODataExpandLevels.Max))).Should().Be("Categories?$expand=*($levels=max)");
		Format(new FluentCommand().For("Employees").Expand(ODataExpandOptions.ByValue(2), [new ODataExpandAssociation("Subordinates")]))
			.Should().Be("Employees?$expand=Subordinates($levels=2)");
	}

	[Fact]
	public void ExpandStarRemovesNavigationPropertiesFromSelect() =>
		Format(new FluentCommand().For("Categories").Expand(ODataExpandOptions.ByValue()).Select("CategoryName", "Products"))
			.Should().Be("Categories?$expand=*&$select=CategoryName");

	[Fact]
	public void CountSegmentAndCountOption()
	{
		Format(new FluentCommand().For("Products").WithCount()).Should().Be("Products?$count=true");
		Format(new FluentCommand().For("Products").Count()).Should().Be("Products/$count");
		Format(new FluentCommand().For("Products").Count().Filter("UnitPrice gt 10")).Should().Be("Products/$count?$filter=UnitPrice%20gt%2010");
	}

	[Fact]
	public void EachSegment() =>
		Format(new FluentCommand().For("Products").Each().Filter("UnitPrice gt 10")).Should().Be("Products/$each?$filter=UnitPrice%20gt%2010");

	[Fact]
	public void UnboundFunctionWithCollectionParameterUsesAlias() =>
		Format(new FluentCommand().Function("PassThroughIntCollection").Set(new { numbers = new[] { 1, 2, 3 } }))
			.Should().Be("PassThroughIntCollection(numbers=@p1)?@p1=[1,2,3]");

	[Fact]
	public void FunctionAndActionCannotBeCombined()
	{
		var act = () => Format(new FluentCommand().Function("PassThroughIntCollection").Action("Something"));
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void DerivedCollection()
	{
		Format(new FluentCommand().For("Transport").As("Ships")).Should().Be("Transport/NorthwindModel.Ships");
		Format(new FluentCommand().For("Transport/Ships")).Should().Be("Transport/NorthwindModel.Ships");
		new FluentCommand().For("Transport").As("Ships").Resolve(Session).QualifiedEntityCollectionName.Should().Be("Transport/NorthwindModel.Ships");
	}

	[Fact]
	public void NavigationLinkFromParent()
	{
		var parent = new FluentCommand().For("Categories").Key(1);
		Format(new FluentCommand(parent, null).Link("Products")).Should().Be("Categories(1)/Products");
		Format(new FluentCommand(parent, null).Link("Products").Filter("UnitPrice gt 10")).Should().Be("Categories(1)/Products?$filter=UnitPrice%20gt%2010");
		new FluentCommand(parent, null).Link("Products").Resolve(Session).EntityCollection!.Name.Should().Be("Products");
	}

	[Fact]
	public void MediaSegments()
	{
		Format(new FluentCommand().For("Products").Key(1).Media()).Should().Be("Products(1)/$value");
		Format(new FluentCommand().For("Products").Key(1).Media("Photo")).Should().Be("Products(1)/Photo");
	}

	[Fact]
	public void CustomQueryOptions()
	{
		Format(new FluentCommand().For("Products").QueryOptions("foo=bar")).Should().Be("Products?foo=bar");
		Format(new FluentCommand().For("Products").QueryOptions(new Dictionary<string, object?> { ["foo"] = 1, ["bar"] = "x" })).Should().Be("Products?foo=1&bar=%27x%27");
	}

	[Fact]
	public void ApplyTextIsEscaped() =>
		Format(new FluentCommand().For("Products").Apply("groupby((CategoryID))")).Should().Be("Products?$apply=groupby%28%28CategoryID%29%29");

	[Fact]
	public void ApplyBuilderIsInvoked()
	{
		var builder = new FakeApplyBuilder();
		Format(new FluentCommand().For("Products").Apply(builder)).Should().Be("Products?$apply=aggregate%28UnitPrice%20with%20sum%20as%20Total%29");
		builder.Command!.EntityCollection!.Name.Should().Be("Products");
	}

	[Fact]
	public void HeadersAndPreferences()
	{
		var command = new FluentCommand().For("Products").WithIfMatch("W/\"1\"").WithIfNoneMatch("*").WithMaxPageSize(10).TrackChanges().WithHeader("X-Test", "1");
		command.Details.Headers.Should().Equal(new Dictionary<string, string> { ["If-Match"] = "W/\"1\"", ["If-None-Match"] = "*", ["X-Test"] = "1" });
		command.Details.MaxPageSize.Should().Be(10);
		command.Details.TrackChanges.Should().BeTrue();
	}

	[Fact]
	public void CommandDataFlattensDynamicContainer()
	{
		var resolved = new FluentCommand().For("Products").WithProperties("DynamicProperties")
			.Set(new Dictionary<string, object?> { ["ProductName"] = "Chai", ["DynamicProperties"] = new Dictionary<string, object?> { ["Extra"] = 1 } })
			.Resolve(Session);
		resolved.CommandData.Should().Equal(new Dictionary<string, object?> { ["ProductName"] = "Chai", ["Extra"] = 1 });
		resolved.DynamicPropertiesContainerName.Should().Be("DynamicProperties");
	}

	[Fact]
	public void SetObjectIsConvertedToDictionary()
	{
		var resolved = new FluentCommand().For("Products").Set(new { ProductName = "Chai", UnitPrice = 1.5m }).Resolve(Session);
		resolved.CommandData.Should().Equal(new Dictionary<string, object?> { ["ProductName"] = "Chai", ["UnitPrice"] = 1.5m });
	}

	[Fact]
	public void FilterOnKeyResetsTopAndDerivedTypeConstraint() =>
		Format(new FluentCommand().For("Products").Filter(new ODataExpression<Product>(x => x.ProductID == 1)).Top(1)).Should().Be("Products(1)");

	private sealed class FakeApplyBuilder : IApplyBuilder
	{
		public ResolvedCommand? Command { get; private set; }

		public string Build(ResolvedCommand command, Session session)
		{
			Command = command;
			return "aggregate(UnitPrice with sum as Total)";
		}
	}
}
