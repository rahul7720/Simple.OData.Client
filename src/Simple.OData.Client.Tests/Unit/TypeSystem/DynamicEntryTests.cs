using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public sealed class DynamicEntryTests
{
	private static readonly ITypeCache TypeCache = TestTypeCache.Create();

	private static Dictionary<string, object?> ProductData() => new()
	{
		["ProductID"] = 1,
		["ProductName"] = "Chai",
		["UnitPrice"] = 18m,
		["Category"] = new Dictionary<string, object?> { ["CategoryID"] = 2, ["CategoryName"] = "Beverages" },
		["Tags"] = new List<object?> { "a", "b" },
		["Details"] = new List<object?> { new Dictionary<string, object?> { ["Quantity"] = 5 } },
	};

	[Fact]
	public void MemberAccessReturnsValues()
	{
		dynamic entry = new DynamicODataEntry(ProductData(), TypeCache);
		((int)entry.ProductID).Should().Be(1);
		((string)entry.ProductName).Should().Be("Chai");
		((object?)entry.Missing).Should().BeNull();
	}

	[Fact]
	public void NestedDictionariesBecomeDynamicEntries()
	{
		dynamic entry = new DynamicODataEntry(ProductData(), TypeCache);
		((object)entry.Category).Should().BeOfType<DynamicODataEntry>();
		((string)entry.Category.CategoryName).Should().Be("Beverages");
		((object)entry.Tags).Should().BeAssignableTo<IEnumerable<object?>>();
		((int)entry.Details[0].Quantity).Should().Be(5);
	}

	[Fact]
	public void ConvertsToTypedObject()
	{
		dynamic entry = new DynamicODataEntry(ProductData(), TypeCache);
		Product product = entry;
		product.ProductName.Should().Be("Chai");
		product.Category.CategoryName.Should().Be("Beverages");
		((Product)entry).ProductID.Should().Be(1);
	}

	[Fact]
	public void UnderlyingDictionaryIsUnwrapped()
	{
		var entry = new DynamicODataEntry(ProductData(), TypeCache);
		entry.AsDictionary()["Category"].Should().BeOfType<Dictionary<string, object?>>();
	}

	[Fact]
	public void TypeCacheCreatesDynamicEntries()
	{
		TypeCache.ToObject(ProductData(), typeof(ODataEntry), dynamicObject: true).Should().BeOfType<DynamicODataEntry>();
		TypeCache.ToObject(ProductData(), typeof(ODataEntry), dynamicObject: false).Should().BeOfType<ODataEntry>();
	}
}
