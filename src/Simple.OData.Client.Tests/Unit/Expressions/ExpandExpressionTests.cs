using System.Linq.Expressions;
using Simple.OData.Client.Expressions;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Expressions;

public class ExpandExpressionTests
{
	private readonly TypeCache _typeCache = TestTypeCache.Create();
	private readonly Session _session = MetadataClientFactory.CreateSession("Northwind4.xml");

	private IReadOnlyList<ODataExpandAssociation> Extract<T>(Expression<Func<T, object>> expression) => expression.ExtractExpandAssociations(_typeCache).ToList();

	[Fact]
	public void SingleNavigation()
	{
		var associations = Extract<Product>(x => x.Category);
		associations.Should().ContainSingle().Which.Name.Should().Be("Category");
	}

	[Fact]
	public void AnonymousTypeListsSeveral()
	{
		var associations = Extract<Employee>(x => new { x.Superior, x.Subordinates });
		associations.Select(x => x.Name).Should().Equal("Superior", "Subordinates");
	}

	[Fact]
	public void NestedNavigationViaMemberAccess()
	{
		var associations = Extract<Product>(x => x.Category.Products);
		var category = associations.Should().ContainSingle().Subject;
		category.Name.Should().Be("Category");
		category.ExpandAssociations.Should().ContainSingle().Which.Name.Should().Be("Products");
	}

	[Fact]
	public void CollectionSelectProducesNestedExpand()
	{
		var associations = Extract<Employee>(x => x.Subordinates.Select(s => s.Superior));
		var subordinates = associations.Should().ContainSingle().Subject;
		subordinates.Name.Should().Be("Subordinates");
		subordinates.ExpandAssociations.Should().ContainSingle().Which.Name.Should().Be("Superior");
	}

	[Fact]
	public void WhereBecomesFilter()
	{
		var associations = Extract<Category>(x => x.Products.Where(p => p.UnitPrice > 10));
		var products = associations.Should().ContainSingle().Subject;
		products.FilterExpression.Should().NotBeNull();
		products.FilterExpression!.AsString(_session, _session.Metadata.GetEntityCollection("Products")).Should().Be("UnitPrice gt 10");
	}

	[Fact]
	public void OrderByBecomesOrderByColumns()
	{
		var associations = Extract<Category>(x => x.Products.OrderBy(p => p.ProductName).ThenByDescending(p => p.UnitPrice));
		var products = associations.Should().ContainSingle().Subject;
		products.OrderByColumns.Should().Equal(new ODataOrderByColumn("ProductName", false), new ODataOrderByColumn("UnitPrice", true));
	}

	[Fact]
	public void TakeAndSkipBecomeTopAndSkip()
	{
		var associations = Extract<Category>(x => x.Products.Skip(2).Take(5));
		var products = associations.Should().ContainSingle().Subject;
		products.Top.Should().Be(5);
		products.Skip.Should().Be(2);
	}

	[Fact]
	public void FromPathBuildsNestedChain()
	{
		var association = ODataExpandAssociation.From("Category/Products/Supplier");
		association.Name.Should().Be("Category");
		association.ExpandAssociations.Single().Name.Should().Be("Products");
		association.ExpandAssociations.Single().ExpandAssociations.Single().Name.Should().Be("Supplier");
	}

	[Fact]
	public void UnsupportedExpressionThrows()
	{
		Expression<Func<Product, object>> expression = x => x.ProductName.Length;
		var act = () => expression.ExtractExpandAssociations(_typeCache).ToList();
		act.Should().Throw<NotSupportedException>();
	}
}
