using System.ComponentModel.DataAnnotations.Schema;

namespace Simple.OData.Client.Tests.Server;

/// <summary>Seed facts of the in-process server the end-to-end assertions rely on.</summary>
internal static class Seed
{
	public const int Products = 22;
	public const int Categories = 8;
	public const int BeveragesProducts = 2;
	public const int CondimentsProducts = 7;
	public const int Employees = 9;
	public const int Customers = 5;
	public const int Orders = 6;
	public const int OrderDetails = 12;
	public const int Transports = 3;
	public const int Ships = 2;
	public const int OrdersWithAnyDetailOver30 = 4;
	public const int OrdersWithAllDetailsAtLeast10 = 4;
	public const int WorkAddressCustomers = 3;
	public static readonly Guid FirstGuidKey = new("d8f3f70f-c185-49ab-9a92-0c86c344ab1b");
}

// Client-side shapes of the server entities that differ from the shared test entities in Simple.OData.Client.Tests.
// Being declared in this namespace they take precedence over the shared ones inside the Server tests.

public enum AddressType
{
	Home = 0,
	Work = 1,
	Shipping = 2,
}

public class Address
{
	public string? Street { get; set; }
	public string? City { get; set; }
	public string? Region { get; set; }
	public string? PostalCode { get; set; }
	public string? Country { get; set; }
	public AddressType Type { get; set; }
}

public class CustomerAddress
{
	public int Id { get; set; }
	public string? Street { get; set; }
	public string? City { get; set; }
}

public class Customer
{
	public string CustomerID { get; set; } = string.Empty;
	public string? CompanyName { get; set; }
	public string? ContactName { get; set; }
	public Address? Address { get; set; }
	public CustomerAddress[]? Addresses { get; set; }
	public Order[]? Orders { get; set; }
}

public class Company
{
	public string Name { get; set; } = string.Empty;
	public int EmployeeCount { get; set; }
	public Address? Headquarters { get; set; }
}

public class Supplier
{
	public int SupplierID { get; set; }
	public string? CompanyName { get; set; }
	public string? ContactName { get; set; }
	public Product[]? Products { get; set; }
}

public class Truck : Transport
{
	public string? TruckNumber { get; set; }
}

public class Picture
{
	public int Id { get; set; }
	public string? Name { get; set; }
}

[Table("Order_Details")]
public class Order_Details : OrderDetail
{
}

public class ProductWithRating : Product
{
	public int? Rating { get; set; }
	public bool CheckedOut { get; set; }
}
