using System.Linq.Expressions;
using Simple.OData.Client.Expressions;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Expressions;

public class ColumnExpressionTests
{
	private readonly TypeCache _typeCache = TestTypeCache.Create();

	[Fact]
	public void SingleMember()
	{
		Expression<Func<Product, object>> expression = x => x.ProductName;
		expression.ExtractColumnNames(_typeCache).Should().Equal("ProductName");
	}

	[Fact]
	public void ValueTypeMemberWithConvert()
	{
		Expression<Func<Product, object>> expression = x => x.ProductID;
		expression.ExtractColumnNames(_typeCache).Should().Equal("ProductID");
	}

	[Fact]
	public void MappedMemberUsesMappedName()
	{
		Expression<Func<Product, object>> expression = x => x.MappedEnglishName;
		expression.ExtractColumnNames(_typeCache).Should().Equal("EnglishName");
	}

	[Fact]
	public void AnonymousTypeListsAllMembersInOrder()
	{
		Expression<Func<Product, object>> expression = x => new { x.ProductID, x.ProductName, x.Category.CategoryName };
		expression.ExtractColumnNames(_typeCache).Should().Equal("ProductID", "ProductName", "Category/CategoryName");
	}

	[Fact]
	public void NestedPath()
	{
		Expression<Func<Product, object>> expression = x => x.Category.CategoryName;
		expression.ExtractColumnNames(_typeCache).Should().Equal("Category/CategoryName");
	}

	[Fact]
	public void CollectionSelectProducesPath()
	{
		Expression<Func<Category, object>> expression = x => x.Products.Select(p => p.ProductName);
		expression.ExtractColumnNames(_typeCache).Should().Equal("Products/ProductName");
	}

	[Fact]
	public void UnsupportedBodyThrows()
	{
		Expression<Func<Product, object>> expression = x => x.ProductName.ToUpperInvariant();
		var act = () => expression.ExtractColumnNames(_typeCache).ToList();
		act.Should().Throw<NotSupportedException>();
	}
}
