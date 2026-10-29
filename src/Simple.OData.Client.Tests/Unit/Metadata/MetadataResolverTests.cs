using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Metadata;

public class MetadataResolverTests
{
	private const string ConcurrencyCsdl = """
		<edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx"><edmx:DataServices>
		<Schema Namespace="NS" xmlns="http://docs.oasis-open.org/odata/ns/edm">
		<EntityType Name="Item"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/><Property Name="RowVersion" Type="Edm.Binary"/></EntityType>
		<EntityType Name="Other"><Key><PropertyRef Name="Id"/></Key><Property Name="Id" Type="Edm.Int32" Nullable="false"/></EntityType>
		<EntityContainer Name="C">
		<EntitySet Name="Items" EntityType="NS.Item"><Annotation Term="Org.OData.Core.V1.OptimisticConcurrency"><Collection><PropertyPath>RowVersion</PropertyPath></Collection></Annotation></EntitySet>
		<EntitySet Name="Others" EntityType="NS.Other"/>
		</EntityContainer></Schema></edmx:DataServices></edmx:Edmx>
		""";

	private readonly IMetadata _northwind = TestMetadata.Resolver("Northwind4.xml");
	private readonly IMetadata _tripPin = TestMetadata.Resolver("TripPin.xml");

	[Fact]
	public void GetEntityCollectionExactName_ResolvesLeniently()
	{
		_northwind.GetEntityCollectionExactName("Products").Should().Be("Products");
		_northwind.GetEntityCollectionExactName("products").Should().Be("Products");
		_northwind.GetEntityCollectionExactName("Product").Should().Be("Products");
		_northwind.GetEntityCollectionExactName("Order_Details").Should().Be("Order_Details");
		_northwind.GetEntityCollectionExactName("OrderDetails").Should().Be("Order_Details");
	}

	[Fact]
	public void GetEntityCollectionExactName_StrictRejectsCaseDifference()
	{
		var strict = TestMetadata.Resolver("Northwind4.xml", ODataNameMatchResolver.Strict);
		strict.GetEntityCollectionExactName("Products").Should().Be("Products");
		var act = () => strict.GetEntityCollectionExactName("products");
		act.Should().Throw<UnresolvableObjectException>().Which.ObjectName.Should().Be("products");
	}

	[Fact]
	public void Singletons()
	{
		_tripPin.GetEntityCollectionExactName("Me").Should().Be("Me");
		_tripPin.IsSingleton("Me").Should().BeTrue();
		_tripPin.IsSingleton("People").Should().BeFalse();
		_tripPin.GetEntityTypeExactName("Me").Should().Be("Person");
	}

	[Fact]
	public void GetEntityCollection_WithDerivedTypePath()
	{
		var collection = _northwind.GetEntityCollection("Transports/NorthwindModel.Ships");
		collection.Name.Should().Be("Ships");
		collection.BaseEntityCollection!.Name.Should().Be("Transport");
	}

	[Fact]
	public void GetDerivedEntityCollection()
	{
		var transports = _northwind.GetEntityCollection("Transports");
		_northwind.GetDerivedEntityCollection(transports, "Ships").Name.Should().Be("Ships");
		_northwind.GetDerivedEntityCollection(transports, "ships").Name.Should().Be("Ships");
		var act = () => _northwind.GetDerivedEntityCollection(transports, "Planes");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void GetDeclaredKeyPropertyNames()
	{
		_northwind.GetDeclaredKeyPropertyNames("Order_Details").Should().Equal("OrderID", "ProductID");
		_northwind.GetDeclaredKeyPropertyNames("Products").Should().Equal("ProductID");
		_northwind.GetDeclaredKeyPropertyNames("Ships").Should().Equal("TransportID");
	}

	[Fact]
	public void ExactMatchResolverHonoursItsStringComparison()
	{
		var ignoreCase = TestMetadata.Resolver("Northwind4.xml", new ExactMatchResolver(alphanumericComparison: false, StringComparison.OrdinalIgnoreCase));
		ignoreCase.GetEntityCollectionExactName("products").Should().Be("Products");
		ignoreCase.GetStructuralPropertyExactName("Products", "productname").Should().Be("ProductName");
		ignoreCase.GetNavigationPropertyExactName("Products", "category").Should().Be("Category");

		var ordinal = TestMetadata.Resolver("Northwind4.xml", new ExactMatchResolver());
		ordinal.Invoking(x => x.GetEntityCollectionExactName("products")).Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void GetAlternateKeyPropertyNamesFromJsonCsdlWithStringPropertyRefs()
	{
		// ASP.NET Core OData writes the PropertyRef Name as a plain string instead of a $PropertyPath
		var metadata = TestMetadata.Resolver("AlternateKeysJson.json");
		metadata.GetAlternateKeyPropertyNames("Products").Should().BeEquivalentTo(new[] { new[] { "ProductName" } });
	}

	[Fact]
	public void GetAlternateKeyPropertyNames()
	{
		var metadata = TestMetadata.Resolver("Northwind4WithAlternateKeys.xml");
		metadata.GetAlternateKeyPropertyNames("Categories").Should().BeEquivalentTo(new[] { new[] { "CategoryName" } });
		metadata.GetAlternateKeyPropertyNames("Employees").Should().BeEquivalentTo(new[] { new[] { "HomePhone", "Title" } });
		metadata.GetAlternateKeyPropertyNames("Orders").Should().BeEquivalentTo(new[] { new[] { "CustomerID" }, new[] { "ShipName" } });
		_northwind.GetAlternateKeyPropertyNames("Products").Should().BeEmpty();
	}

	[Fact]
	public void NavigationProperties()
	{
		_northwind.HasNavigationProperty("Products", "Category").Should().BeTrue();
		_northwind.HasNavigationProperty("Products", "Nope").Should().BeFalse();
		_northwind.GetNavigationPropertyExactName("Products", "category").Should().Be("Category");
		_northwind.GetNavigationPropertyPartnerTypeName("Products", "Category").Should().Be("Category");
		_northwind.GetNavigationPropertyPartnerTypeName("Categories", "Products").Should().Be("Product");
		_northwind.IsNavigationPropertyCollection("Categories", "Products").Should().BeTrue();
		_northwind.IsNavigationPropertyCollection("Products", "Category").Should().BeFalse();
		_northwind.GetNavigationPropertyNames("Products").Should().Contain(["Category", "Order_Details", "Supplier"]);
		_northwind.GetNavigationPropertyTargetSetName("Products", "Category").Should().Be("Categories");
		var act = () => _northwind.GetNavigationPropertyExactName("Products", "Nope");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void ContainedNavigationProperties()
	{
		_tripPin.IsNavigationPropertyContained("People", "Trips").Should().BeTrue();
		_tripPin.IsNavigationPropertyContained("People", "Friends").Should().BeFalse();
		_northwind.IsNavigationPropertyContained("Products", "Category").Should().BeFalse();
	}

	[Fact]
	public void StructuralProperties()
	{
		_northwind.HasStructuralProperty("Products", "ProductName").Should().BeTrue();
		_northwind.HasStructuralProperty("Products", "Category").Should().BeFalse();
		_northwind.GetStructuralPropertyExactName("Products", "productname").Should().Be("ProductName");
		_northwind.GetStructuralPropertyNames("Categories").Should().Equal("CategoryID", "CategoryName", "Description", "Picture");
		var act = () => _northwind.GetStructuralPropertyExactName("Products", "Nope");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void GetStructuralPropertyPath_WalksComplexTypesIncludingCollections()
	{
		_tripPin.GetStructuralPropertyPath("People", "AddressInfo", "City", "Name").Should().Be("AddressInfo/City/Name");
		_tripPin.GetStructuralPropertyPath("People", "addressinfo", "city", "countryregion").Should().Be("AddressInfo/City/CountryRegion");
		_northwind.GetStructuralPropertyPath("Products", "ProductName").Should().Be("ProductName");
	}

	[Fact]
	public void OpenTypesStreamsAndIds()
	{
		TestMetadata.Resolver("OpenTypes.xml").IsOpenType("TheRecords").Should().BeTrue();
		_northwind.IsOpenType("Products").Should().BeFalse();
		_tripPin.HasStream("Photos").Should().BeTrue();
		_tripPin.HasStream("People").Should().BeFalse();
		_northwind.IsTypeWithId("Product").Should().BeTrue();
		_northwind.IsTypeWithId("NorthwindModel.Product").Should().BeTrue();
		_northwind.IsTypeWithId("Address").Should().BeFalse();
	}

	[Fact]
	public void GetQualifiedTypeName()
	{
		_northwind.GetQualifiedTypeName("Product").Should().Be("NorthwindModel.Product");
		_northwind.GetQualifiedTypeName("Products").Should().Be("NorthwindModel.Product");
		_northwind.GetQualifiedTypeName("Address").Should().Be("NorthwindModel.Address");
		_northwind.GetQualifiedTypeName("AddressType").Should().Be("NorthwindModel.AddressType");
		var act = () => _northwind.GetQualifiedTypeName("Nope");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void GetEntityTypeExactName()
	{
		_northwind.GetEntityTypeExactName("product").Should().Be("Product");
		_northwind.GetEntityTypeExactName("Products").Should().Be("Product");
	}

	[Fact]
	public void Functions()
	{
		_northwind.GetFunctionFullName("PassThroughIntCollection").Should().Be("PassThroughIntCollection");
		_northwind.GetFunctionReturnCollection("PassThroughIntCollection").Should().BeNull();

		_tripPin.GetFunctionFullName("GetFavoriteAirline").Should().Be("Microsoft.OData.SampleService.Models.TripPin.GetFavoriteAirline");
		_tripPin.GetFunctionReturnCollection("GetFavoriteAirline")!.Name.Should().Be("Airline");
		_tripPin.GetFunctionFullName("GetNearestAirport").Should().Be("GetNearestAirport");

		TestMetadata.Resolver("TripPin.xml", unqualifiedNameCall: true).GetFunctionFullName("GetFavoriteAirline").Should().Be("GetFavoriteAirline");
		var act = () => _northwind.GetFunctionFullName("Nope");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void Actions()
	{
		_tripPin.GetActionFullName("ShareTrip").Should().Be("Microsoft.OData.SampleService.Models.TripPin.ShareTrip");
		_tripPin.GetActionFullName("ResetDataSource").Should().Be("ResetDataSource");
		_tripPin.GetActionReturnCollection("ResetDataSource").Should().BeNull();
		var act = () => _tripPin.GetActionFullName("Nope");
		act.Should().Throw<UnresolvableObjectException>();
	}

	[Fact]
	public void OptimisticConcurrencyAnnotationIsDetected()
	{
		var metadata = TestMetadata.ResolverFromCsdl(ConcurrencyCsdl);
		metadata.EntityCollectionRequiresOptimisticConcurrencyCheck("Items").Should().BeTrue();
		metadata.EntityCollectionRequiresOptimisticConcurrencyCheck("Others").Should().BeFalse();
	}

	[Fact]
	public void NavigateToCollection()
	{
		_northwind.NavigateToCollection("Products/Category").Name.Should().Be("Categories");
		_northwind.NavigateToCollection("Categories(1)/Products").Name.Should().Be("Products");
		_northwind.NavigateToCollection("Employees/Subordinates/Superior").Name.Should().Be("Employees");
		_northwind.NavigateToCollection(_northwind.GetEntityCollection("Orders"), "Order_Details/Product").Name.Should().Be("Products");
		_northwind.NavigateToCollection("Transports/NorthwindModel.Ships").Name.Should().Be("Ships");
	}

	[Fact]
	public void GetLinkedCollectionName()
	{
		_northwind.GetLinkedCollectionName("Category", "Category", out var isSingleton).Should().Be("Categories");
		isSingleton.Should().BeFalse();
		_tripPin.GetLinkedCollectionName("Me", "Person", out isSingleton).Should().Be("Me");
		isSingleton.Should().BeTrue();
	}

	[Fact]
	public void ParseEntryDetails_SplitsPropertiesAndLinks()
	{
		var data = new Dictionary<string, object?>
		{
			["ProductName"] = "Chai",
			["Category"] = new Dictionary<string, object?> { ["CategoryID"] = 1 },
			["Order_Details"] = new List<object> { new Dictionary<string, object?> { ["OrderID"] = 1 }, new Dictionary<string, object?> { ["OrderID"] = 2 } },
		};

		var details = _northwind.ParseEntryDetails("Products", data, contentId: "7");

		details.Properties.Should().ContainSingle().Which.Key.Should().Be("ProductName");
		details.Links["Category"].Should().ContainSingle().Which.ContentId.Should().Be("7");
		details.Links["Order_Details"].Should().HaveCount(2);
		details.HasOpenTypeProperties.Should().BeFalse();
	}

	[Fact]
	public void ParseEntryDetails_UnmappedPropertyThrowsUnlessIgnored()
	{
		var data = new Dictionary<string, object?> { ["Nope"] = 1 };
		var act = () => _northwind.ParseEntryDetails("Products", data);
		act.Should().Throw<UnresolvableObjectException>().Which.ObjectName.Should().Be("Nope");

		var lenient = TestMetadata.Resolver("Northwind4.xml", ignoreUnmappedProperties: true);
		lenient.ParseEntryDetails("Products", data).Properties.Should().BeEmpty();
	}

	[Fact]
	public void ParseEntryDetails_OpenTypeKeepsUnknownProperties()
	{
		var metadata = TestMetadata.Resolver("OpenTypes.xml");
		var details = metadata.ParseEntryDetails("TheRecords", new Dictionary<string, object?> { ["Id"] = 1, ["Extra"] = "x" });
		details.HasOpenTypeProperties.Should().BeTrue();
		details.Properties.Should().ContainKey("Extra");
	}
}
