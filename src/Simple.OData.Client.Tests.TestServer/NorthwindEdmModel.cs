using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer;

/// <summary>Builds the Northwind-style EDM model served at <c>/odata</c>.</summary>
public static class NorthwindEdmModel
{
	public const string Namespace = "NorthwindModel";

	public static IEdmModel Create()
	{
		var builder = new ODataConventionModelBuilder { Namespace = Namespace, ContainerName = "NorthwindEntities" };

		builder.EntitySet<Category>("Categories");
		builder.EntitySet<Product>("Products");
		builder.EntitySet<Supplier>("Suppliers");
		builder.EntitySet<Employee>("Employees");
		builder.EntitySet<Customer>("Customers");
		builder.EntitySet<Order>("Orders");
		builder.EntitySet<Order_Detail>("Order_Details");
		builder.EntitySet<Transport>("Transport");
		builder.EntityType<Ship>();
		builder.EntityType<Truck>();
		builder.EntitySet<TypeWithGuidKey>("TypeWithGuidKey");
		builder.EntitySet<Animal>("Animals");
		builder.EntitySet<Picture>("Pictures");
		builder.Singleton<Company>("Company");
		builder.ComplexType<Address>();
		builder.EnumType<AddressType>();

		// unbound functions
		builder.Function("PassThroughIntCollection").ReturnsCollection<int>().CollectionParameter<int>("numbers");
		builder.Function("PassThroughString").Returns<string>().Parameter<string>("value");
		builder.Function("PassThroughInt").Returns<int>().Parameter<int>("value");
		builder.Function("PassThroughLong").Returns<long>().Parameter<long>("value");
		builder.Function("PassThroughDateTime").Returns<DateTimeOffset>().Parameter<DateTimeOffset>("value");
		builder.Function("PassThroughGuid").Returns<Guid>().Parameter<Guid>("value");
		builder.Function("PassThroughAddress").Returns<Address>().Parameter<Address>("address");
		builder.Function("ProductCount").Returns<int>();
		builder.Function("GetProductsByRating").ReturnsCollectionFromEntitySet<Product>("Products").Parameter<int>("rating");

		// bound functions
		var products = builder.EntityType<Product>();
		products.Collection.Function("MostExpensive").ReturnsFromEntitySet<Product>("Products");
		products.Collection.Function("Slow").ReturnsCollectionFromEntitySet<Product>("Products");
		products.Collection.Function("Changes").ReturnsCollectionFromEntitySet<Product>("Products");
		products.Function("Discounted").Returns<decimal>().Parameter<decimal>("percent");

		// bound actions
		products.Action("Rate").Parameter<int>("rating");
		products.Action("CheckOut").ReturnsFromEntitySet<Product>("Products");
		products.Action("Return").ReturnsFromEntitySet<Product>("Products");
		products.Collection.Action("DiscontinueAll").Returns<int>();

		// unbound actions
		builder.Action("ResetData");
		builder.Action("Echo").Returns<int>().Parameter<int>("value");
		var createProduct = builder.Action("CreateProduct").ReturnsFromEntitySet<Product>("Products");
		createProduct.Parameter<string>("name");
		createProduct.Parameter<decimal>("price");
		builder.Action("ListProducts").ReturnsCollectionFromEntitySet<Product>("Products").Parameter<int>("categoryId");
		builder.Action("UpdateAddress").Returns<Address>().Parameter<Address>("address");
		builder.Action("MergeProducts").Returns<int>().CollectionEntityParameter<Product>("products");
		builder.Action("GetAddressType").Returns<AddressType>();
		builder.Action("FilterCustomersByAddressType").ReturnsCollectionFromEntitySet<Customer>("Customers").Parameter<AddressType>("type");

		var model = (EdmModel)builder.GetEdmModel();
		AddAlternateKey(model);
		return model;
	}

	private static void AddAlternateKey(EdmModel model)
	{
		var product = (IEdmEntityType)model.FindDeclaredType($"{Namespace}.Product");
		var productName = product.FindProperty("ProductName");
		model.AddAlternateKeyAnnotation(product, new Dictionary<string, IEdmProperty>(StringComparer.Ordinal) { ["ProductName"] = productName });
	}
}
