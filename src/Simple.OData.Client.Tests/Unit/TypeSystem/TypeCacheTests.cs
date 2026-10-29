using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public class TypeCacheTests
{
	private readonly TypeCache _typeCache = TestTypeCache.Create();

	[DataContract(Name = "RenamedEntity")]
	private sealed class MappedEntity
	{
		public int Id { get; set; }

		[DataMember(Name = "DataMemberName")]
		public string? ViaDataMember { get; set; }

		[JsonPropertyName("JsonName")]
		public string? ViaJsonPropertyName { get; set; }

		[JsonIgnore]
		public string? Ignored { get; set; }
	}

	[Fact]
	public void GetDerivedTypes_BaseType()
	{
		_typeCache.GetDerivedTypes(typeof(Transport)).Should().ContainSingle().Which.Should().Be(typeof(Ship));
	}

	[Fact]
	public void GetAllProperties_BaseAndDerivedTypes()
	{
		_typeCache.GetAllProperties(typeof(Transport)).Should().ContainSingle();
		_typeCache.GetAllProperties(typeof(Ship)).Should().HaveCount(2);
	}

	[Fact]
	public void GetAllProperties_ExcludesExplicitInterfaceImplementationsAndIndexers()
	{
		_typeCache.GetAllProperties(typeof(Address)).Should().HaveCount(5);
		_typeCache.GetAllProperties(typeof(TypeWithIndexer)).Should().ContainSingle();
	}

	[Fact]
	public void GetDeclaredProperties_OnlyDeclaringType()
	{
		_typeCache.GetDeclaredProperties(typeof(Transport)).Should().ContainSingle();
		_typeCache.GetDeclaredProperties(typeof(Ship)).Should().ContainSingle();
	}

	[Fact]
	public void GetNamedProperty_WalksBaseTypes()
	{
		_typeCache.GetNamedProperty(typeof(Transport), "TransportID").Should().NotBeNull();
		_typeCache.GetNamedProperty(typeof(Ship), "TransportID").Should().NotBeNull();
		_typeCache.GetNamedProperty(typeof(Ship), "ShipName").Should().NotBeNull();
		_typeCache.GetNamedProperty(typeof(Ship), "Missing").Should().BeNull();
	}

	[Fact]
	public void GetDeclaredProperty_DoesNotWalkBaseTypes()
	{
		_typeCache.GetDeclaredProperty(typeof(Transport), "TransportID").Should().NotBeNull();
		_typeCache.GetDeclaredProperty(typeof(Ship), "TransportID").Should().BeNull();
		_typeCache.GetDeclaredProperty(typeof(Ship), "ShipName").Should().NotBeNull();
	}

	[Fact]
	public void GetMappedProperties_ExcludesNotMappedAndRenamesColumns()
	{
		var names = _typeCache.GetMappedProperties(typeof(Product)).Select(x => _typeCache.GetMappedName(typeof(Product), x)).ToList();

		names.Should().Contain("ProductID").And.Contain("EnglishName").And.Contain("Category");
		names.Should().NotContain("NotMappedProperty").And.NotContain("MappedEnglishName");
	}

	[Fact]
	public void GetMappedName_HonoursDataMemberJsonPropertyNameAndDataContract()
	{
		_typeCache.GetMappedName(typeof(MappedEntity)).Should().Be("RenamedEntity");
		_typeCache.GetMappedName(typeof(MappedEntity), "ViaDataMember").Should().Be("DataMemberName");
		_typeCache.GetMappedName(typeof(MappedEntity), "ViaJsonPropertyName").Should().Be("JsonName");
		_typeCache.GetMappedName(typeof(MappedEntity), "Id").Should().Be("Id");
		_typeCache.GetMappedProperties(typeof(MappedEntity)).Select(x => x.Name).Should().NotContain("Ignored");
	}

	[Fact]
	public void GetMappedProperty_ResolvesByMappedNameThroughResolver()
	{
		_typeCache.GetMappedProperty(typeof(Product), "EnglishName")!.Name.Should().Be("MappedEnglishName");
		_typeCache.GetMappedProperty(typeof(Product), "productid")!.Name.Should().Be("ProductID");
		_typeCache.GetMappedProperty(typeof(Product), "Unknown").Should().BeNull();
	}

	[Fact]
	public void Register_SetsDynamicContainerNameForTypeAndDerivedTypes()
	{
		_typeCache.DynamicContainerName(typeof(Animal)).Should().BeNull();

		_typeCache.Register<Animal>("DynamicProperties");

		_typeCache.DynamicContainerName(typeof(Animal)).Should().Be("DynamicProperties");
		_typeCache.DynamicContainerName(typeof(Mammal)).Should().Be("DynamicProperties");
	}

	[Fact]
	public void TypeClassificationHelpers()
	{
		_typeCache.IsAnonymousType(new { Id = 1 }.GetType()).Should().BeTrue();
		_typeCache.IsAnonymousType(typeof(Product)).Should().BeFalse();
		_typeCache.IsEnumType(typeof(AddressType)).Should().BeTrue();
		_typeCache.IsValue(typeof(int)).Should().BeTrue();
		_typeCache.IsValue(typeof(string)).Should().BeFalse();
		_typeCache.IsTypeAssignableFrom(typeof(Transport), typeof(Ship)).Should().BeTrue();
		_typeCache.IsTypeAssignableFrom(typeof(Ship), typeof(Transport)).Should().BeFalse();
	}

	[Fact]
	public void GetAnnotationsProperty_FindsODataEntryAnnotationsTypedProperty()
	{
		_typeCache.GetAnnotationsProperty(typeof(ProductWithAnnotations))!.Name.Should().Be("Annotations");
		_typeCache.GetAnnotationsProperty(typeof(Product)).Should().BeNull();
	}

	private sealed class ProductWithAnnotations
	{
		public int ProductID { get; set; }
		public ODataEntryAnnotations? Annotations { get; set; }
	}
}
