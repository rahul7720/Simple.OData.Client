using System.Collections.ObjectModel;
using Microsoft.Spatial;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public class DictionaryMapperTests
{
	[Flags]
	public enum EnumType
	{
		Zero,
		One,
		Two,
		Three,
	}

	private sealed class ClassType
	{
#pragma warning disable CS0649
		public string? StringField;
#pragma warning restore CS0649

		public string? StringProperty { get; set; }
		public string? StringPropertyPrivateSetter { get; private set; }
		public int IntProperty { get; set; }
		public EnumType EnumProperty { get; set; }
		public string[]? StringArrayProperty { get; set; }
		public int[]? IntArrayProperty { get; set; }
		public Collection<int>? IntCollectionProperty { get; set; }
		public List<int>? IntListProperty { get; set; }
		public IList<SubclassType>? CompoundInterfaceListProperty { get; set; }
		public HashSet<string>? StringHashSetProperty { get; set; }
		public SubclassType? CompoundProperty { get; set; }
		public SubclassType[]? CompoundCollectionProperty { get; set; }
		public GeographyPoint? Point { get; set; }
		public DateOnly DateOnlyProperty { get; set; }
	}

	private sealed class SubclassType
	{
		public string? StringProperty { get; set; }
		public int IntProperty { get; set; }
	}

	private sealed class ClassNoDefaultConstructor(string arg)
	{
		public string Arg { get; } = arg;
	}

	private readonly TypeCache _typeCache = TestTypeCache.Create();

	[Fact]
	public void ToObjectPrimitiveProperties()
	{
		var dict = new Dictionary<string, object?> { ["StringProperty"] = "a", ["IntProperty"] = 1 };

		var value = _typeCache.ToObject<ClassType>(dict)!;

		value.StringProperty.Should().Be("a");
		value.IntProperty.Should().Be(1);
	}

	[Theory]
	[InlineData(EnumType.One, EnumType.One)]
	[InlineData("One", EnumType.One)]
	[InlineData(EnumType.One | EnumType.Two, EnumType.Three)]
	[InlineData("One,Two", EnumType.Three)]
	[InlineData(3, EnumType.Three)]
	[InlineData("3", EnumType.Three)]
	public void ToObjectEnumProperty(object source, EnumType expected)
	{
		var dict = new Dictionary<string, object?> { ["EnumProperty"] = source };

		_typeCache.ToObject<ClassType>(dict)!.EnumProperty.Should().Be(expected);
	}

	[Fact]
	public void ToObjectUnknownPropertyIsIgnored()
	{
		var dict = new Dictionary<string, object?> { ["StringProperty"] = "a", ["UnknownProperty"] = "u" };

		_typeCache.ToObject<ClassType>(dict)!.StringProperty.Should().Be("a");
	}

	[Fact]
	public void ToObjectPrivateSetter()
	{
		var dict = new Dictionary<string, object?> { ["StringPropertyPrivateSetter"] = "p" };

		_typeCache.ToObject<ClassType>(dict)!.StringPropertyPrivateSetter.Should().Be("p");
	}

	[Fact]
	public void ToObjectFieldIsNotMapped()
	{
		var dict = new Dictionary<string, object?> { ["StringField"] = "f" };

		_typeCache.ToObject<ClassType>(dict)!.StringField.Should().BeNull();
	}

	[Fact]
	public void ToObjectNullValueLeavesDefault()
	{
		var dict = new Dictionary<string, object?> { ["IntProperty"] = null, ["StringProperty"] = null };

		var value = _typeCache.ToObject<ClassType>(dict)!;
		value.IntProperty.Should().Be(0);
		value.StringProperty.Should().BeNull();
	}

	[Fact]
	public void ToObjectCollections()
	{
		var dict = new Dictionary<string, object?>
		{
			["StringArrayProperty"] = new[] { "x", "y", "z" },
			["IntArrayProperty"] = new List<object> { 1, 2, 3 },
			["IntCollectionProperty"] = new Collection<int> { 1, 2, 3 },
			["IntListProperty"] = new object[] { 1L, 2L, 3L },
			["StringHashSetProperty"] = new[] { "a", "b" },
			["CompoundInterfaceListProperty"] = new List<object>
			{
				new Dictionary<string, object?> { ["StringProperty"] = "x", ["IntProperty"] = 1 },
			},
		};

		var value = _typeCache.ToObject<ClassType>(dict)!;

		value.StringArrayProperty.Should().Equal("x", "y", "z");
		value.IntArrayProperty.Should().Equal(1, 2, 3);
		value.IntCollectionProperty.Should().Equal(1, 2, 3);
		value.IntListProperty.Should().Equal(1, 2, 3);
		value.StringHashSetProperty.Should().BeEquivalentTo(["a", "b"]);
		value.CompoundInterfaceListProperty.Should().ContainSingle().Which.StringProperty.Should().Be("x");
	}

	[Fact]
	public void ToObjectCompoundProperty()
	{
		var dict = new Dictionary<string, object?>
		{
			["CompoundProperty"] = new Dictionary<string, object?> { ["StringProperty"] = "z", ["IntProperty"] = 0 },
		};

		var value = _typeCache.ToObject<ClassType>(dict)!;
		value.CompoundProperty!.StringProperty.Should().Be("z");
		value.CompoundProperty.IntProperty.Should().Be(0);
	}

	[Fact]
	public void ToObjectCompoundCollectionProperty()
	{
		var dict = new Dictionary<string, object?>
		{
			["CompoundCollectionProperty"] = new[]
			{
				new Dictionary<string, object?> { ["StringProperty"] = "x", ["IntProperty"] = 1 },
				new Dictionary<string, object?> { ["StringProperty"] = "y", ["IntProperty"] = 2 },
			},
		};

		var value = _typeCache.ToObject<ClassType>(dict)!;
		value.CompoundCollectionProperty.Should().HaveCount(2);
		value.CompoundCollectionProperty![1].StringProperty.Should().Be("y");
		value.CompoundCollectionProperty[1].IntProperty.Should().Be(2);
	}

	[Fact]
	public void ToObjectODataEntry()
	{
		var dict = new Dictionary<string, object?> { ["StringProperty"] = "a", ["IntProperty"] = 1 };

		var value = _typeCache.ToObject<ODataEntry>(dict)!;
		value["StringProperty"].Should().Be("a");
		value["IntProperty"].Should().Be(1);
	}

	[Fact]
	public void ToObjectDictionaryTargetReturnsSource()
	{
		var dict = new Dictionary<string, object?> { ["StringProperty"] = "a" };

		_typeCache.ToObject<IDictionary<string, object?>>(dict).Should().BeSameAs(dict);
		_typeCache.ToObject<Dictionary<string, object?>>(dict).Should().BeSameAs(dict);
	}

	[Fact]
	public void ToObjectDateOnlyFromDateTimeOffset()
	{
		var dict = new Dictionary<string, object?> { ["DateOnlyProperty"] = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero) };

		_typeCache.ToObject<ClassType>(dict)!.DateOnlyProperty.Should().Be(new DateOnly(2024, 2, 29));
	}

	[Fact]
	public void ToObjectSpatialWithRegisteredDictionaryConverter()
	{
		var dict = new Dictionary<string, object?>
		{
			["Point"] = new Dictionary<string, object?> { ["Latitude"] = 1d, ["Longitude"] = 2d },
		};
		_typeCache.Converter.RegisterTypeConverter(typeof(GeographyPoint), d => GeographyPoint.Create((double)d["Latitude"]!, (double)d["Longitude"]!));

		var value = _typeCache.ToObject<ClassType>(dict)!;
		value.Point!.Latitude.Should().Be(1d);
		value.Point.Longitude.Should().Be(2d);
	}

	[Fact]
	public void ToObjectAnonymousType()
	{
		var template = new { ProductID = 0, ProductName = string.Empty };
		var dict = new Dictionary<string, object?> { ["ProductID"] = 7, ["ProductName"] = "Chai" };

		dynamic value = _typeCache.ToObject(dict, template.GetType())!;

		((int)value.ProductID).Should().Be(7);
		((string)value.ProductName).Should().Be("Chai");
	}

	[Fact]
	public void ToObjectBaseType_NoTypeAnnotation()
	{
		var dict = new Dictionary<string, object?> { ["TransportID"] = 1 };

		_typeCache.ToObject<Transport>(dict)!.TransportID.Should().Be(1);
	}

	[Fact]
	public void ToObjectBaseType_WithBaseTypeAnnotation()
	{
		var dict = new Dictionary<string, object?>
		{
			["__annotations"] = new ODataEntryAnnotations { TypeName = "Northwind.Transport" },
			["TransportID"] = 1,
		};

		_typeCache.ToObject<Transport>(dict).Should().BeOfType<Transport>().Which.TransportID.Should().Be(1);
	}

	[Fact]
	public void ToObjectBaseType_WithDerivedTypeAnnotationCreatesDerivedInstance()
	{
		var dict = new Dictionary<string, object?>
		{
			["__annotations"] = new ODataEntryAnnotations { TypeName = "Northwind.Ship" },
			["TransportID"] = 1,
			["ShipName"] = "Sloop John B",
		};

		var ship = _typeCache.ToObject<Transport>(dict).Should().BeOfType<Ship>().Subject;
		ship.TransportID.Should().Be(1);
		ship.ShipName.Should().Be("Sloop John B");
	}

	[Fact]
	public void ToObjectBaseType_WithUnknownDerivedTypeFallsBackToBase()
	{
		var dict = new Dictionary<string, object?>
		{
			["__annotations"] = new ODataEntryAnnotations { TypeName = "Northwind.Plane" },
			["TransportID"] = 1,
			["PlaneName"] = "Concorde",
		};

		_typeCache.ToObject<Transport>(dict).Should().BeOfType<Transport>().Which.TransportID.Should().Be(1);
	}

	[Fact]
	public void ToObjectAssignsAnnotationsProperty()
	{
		var annotations = new ODataEntryAnnotations { TypeName = "NS.Product", ETag = "W/\"1\"" };
		var dict = new Dictionary<string, object?> { ["__annotations"] = annotations, ["ProductID"] = 1 };

		var value = _typeCache.ToObject<ProductWithAnnotations>(dict)!;
		value.ProductID.Should().Be(1);
		value.Annotations.Should().BeSameAs(annotations);
	}

	[Fact]
	public void ToObjectOpenTypeCollectsUnknownPropertiesIntoDynamicContainer()
	{
		_typeCache.Register<Animal>("DynamicProperties");
		var dict = new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Rex", ["Legs"] = 4 };

		var animal = _typeCache.ToObject<Animal>(dict)!;
		animal.Id.Should().Be(1);
		animal.DynamicProperties.Should().ContainKey("Legs").WhoseValue.Should().Be(4);
		animal.DynamicProperties.Should().NotContainKey("Id");
	}

	[Fact]
	public void ToObjectClassWithoutDefaultCtorThrows()
	{
		var dict = new Dictionary<string, object?> { ["Arg"] = "test" };
		var act = () => _typeCache.ToObject<ClassNoDefaultConstructor>(dict);
		act.Should().Throw<MissingMethodException>();
	}

	[Fact]
	public void ToObjectStringOrValueTypeThrows()
	{
		var dict = new Dictionary<string, object?> { ["x"] = 1 };
		var act = () => _typeCache.ToObject(dict, typeof(int));
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void ToDictionaryUsesMappedNamesAndSkipsNotMapped()
	{
		var product = new Product { ProductID = 1, ProductName = "Chai", MappedEnglishName = "Tea", UnitPrice = 18m };

		var dict = _typeCache.ToDictionary(product);

		dict.Should().ContainKey("ProductID").WhoseValue.Should().Be(1);
		dict.Should().ContainKey("EnglishName").WhoseValue.Should().Be("Tea");
		dict.Should().NotContainKey("MappedEnglishName");
		dict.Should().NotContainKey("NotMappedProperty");
		dict.Should().ContainKey("Category").WhoseValue.Should().BeNull();
	}

	[Fact]
	public void ToDictionaryPassesThroughDictionariesAndEntries()
	{
		var dict = new Dictionary<string, object?> { ["a"] = 1 };
		_typeCache.ToDictionary(dict).Should().BeSameAs(dict);

		var entry = new ODataEntry(dict);
		_typeCache.ToDictionary(entry).Should().BeEquivalentTo(dict);
	}

	[Fact]
	public void ToDictionaryAnonymousType()
	{
		var dict = _typeCache.ToDictionary(new { ProductID = 1, ProductName = "Chai" });
		dict.Should().HaveCount(2).And.ContainKey("ProductName").WhoseValue.Should().Be("Chai");
	}

	private sealed class ProductWithAnnotations
	{
		public int ProductID { get; set; }
		public ODataEntryAnnotations? Annotations { get; set; }
	}
}
