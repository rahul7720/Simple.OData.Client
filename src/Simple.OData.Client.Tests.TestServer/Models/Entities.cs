using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.OData.ModelBuilder;

namespace Simple.OData.Client.Tests.TestServer.Models;

public class Category
{
	public int CategoryID { get; set; }

	public string CategoryName { get; set; } = string.Empty;

	public string? Description { get; set; }

	public byte[]? Picture { get; set; }

	public ICollection<Product> Products { get; set; } = [];
}

public class Product
{
	public int ProductID { get; set; }

	public string ProductName { get; set; } = string.Empty;

	public string? EnglishName { get; set; }

	public string? QuantityPerUnit { get; set; }

	public decimal UnitPrice { get; set; }

	public short? UnitsInStock { get; set; }

	public short? UnitsOnOrder { get; set; }

	public short? ReorderLevel { get; set; }

	public bool Discontinued { get; set; }

	public int? CategoryID { get; set; }

	public int? SupplierID { get; set; }

	public int? Rating { get; set; }

	public bool CheckedOut { get; set; }

	/// <summary>Concurrency token; the service bumps it on every change and emits it as the ETag.</summary>
	[ConcurrencyCheck]
	public long RowVersion { get; set; }

	public Category? Category { get; set; }

	public Supplier? Supplier { get; set; }

	public ICollection<Order_Detail> Order_Details { get; set; } = [];
}

public class Supplier
{
	public int SupplierID { get; set; }

	public string CompanyName { get; set; } = string.Empty;

	public string? ContactName { get; set; }

	public ICollection<Product> Products { get; set; } = [];
}

public class Employee
{
	public int EmployeeID { get; set; }

	public string FirstName { get; set; } = string.Empty;

	public string LastName { get; set; } = string.Empty;

	public string? Title { get; set; }

	public DateTimeOffset? BirthDate { get; set; }

	public DateTimeOffset? HireDate { get; set; }

	public string? Address { get; set; }

	public string? HomePhone { get; set; }

	public string? Extension { get; set; }

	public byte[]? Photo { get; set; }

	public string? Notes { get; set; }

	public int? ReportsTo { get; set; }

	public Employee? Superior { get; set; }

	public ICollection<Employee> Subordinates { get; set; } = [];
}

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

	public string Street { get; set; } = string.Empty;

	public string City { get; set; } = string.Empty;
}

public class Customer
{
	public string CustomerID { get; set; } = string.Empty;

	public string CompanyName { get; set; } = string.Empty;

	public string? ContactName { get; set; }

	public Address? Address { get; set; }

	[Contained]
	public ICollection<CustomerAddress> Addresses { get; set; } = [];

	public ICollection<Order> Orders { get; set; } = [];
}

public class Order
{
	public int OrderID { get; set; }

	public string? CustomerID { get; set; }

	public int? EmployeeID { get; set; }

	public DateTimeOffset? OrderDate { get; set; }

	public DateTimeOffset? RequiredDate { get; set; }

	public DateTimeOffset? ShippedDate { get; set; }

	public DateTimeOffset? ShippedDateTimeOffset { get; set; }

	public decimal? Freight { get; set; }

	public string? ShipName { get; set; }

	public Customer? Customer { get; set; }

	public Employee? Employee { get; set; }

	public ICollection<Order_Detail> Order_Details { get; set; } = [];
}

public class Order_Detail
{
	[Key]
	public int OrderID { get; set; }

	[Key]
	public int ProductID { get; set; }

	public decimal UnitPrice { get; set; }

	public short Quantity { get; set; }

	public float Discount { get; set; }

	public Order? Order { get; set; }

	public Product? Product { get; set; }
}

public abstract class Transport
{
	public int TransportID { get; set; }
}

public class Ship : Transport
{
	public string ShipName { get; set; } = string.Empty;
}

public class Truck : Transport
{
	public string TruckNumber { get; set; } = string.Empty;
}

public class TypeWithGuidKey
{
	[Key]
	public Guid Key { get; set; }

	public string Name { get; set; } = string.Empty;
}

public class Animal
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public IDictionary<string, object> DynamicProperties { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

[MediaType]
public class Picture
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	/// <summary>The media resource bytes (served at <c>Pictures(id)/$value</c>).</summary>
	[NotMapped]
	public byte[] Media { get; set; } = [];

	/// <summary>The stream property bytes (served at <c>Pictures(id)/Content</c>).</summary>
	[NotMapped]
	public byte[] ContentBytes { get; set; } = [];

	/// <summary>The stream property as the EDM model sees it.</summary>
	public Stream? Content
	{
		get => new MemoryStream(ContentBytes, writable: false);
		set
		{
			if (value is null)
			{
				ContentBytes = [];
				return;
			}

			using var buffer = new MemoryStream();
			value.CopyTo(buffer);
			ContentBytes = buffer.ToArray();
		}
	}
}

public class Company
{
	[Key]
	public string Name { get; set; } = string.Empty;

	public int EmployeeCount { get; set; }

	public Address? Headquarters { get; set; }
}
