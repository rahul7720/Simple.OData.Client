using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Data;

/// <summary>The in-memory data of the service; <see cref="Reset"/> restores the seed.</summary>
public sealed class NorthwindData
{
	private readonly Lock _sync = new();

	public NorthwindData() => Reset();

	public List<Category> Categories { get; private set; } = [];

	public List<Product> Products { get; private set; } = [];

	public List<Supplier> Suppliers { get; private set; } = [];

	public List<Employee> Employees { get; private set; } = [];

	public List<Customer> Customers { get; private set; } = [];

	public List<Order> Orders { get; private set; } = [];

	public List<Order_Detail> OrderDetails { get; private set; } = [];

	public List<Transport> Transports { get; private set; } = [];

	public List<TypeWithGuidKey> GuidKeyed { get; private set; } = [];

	public List<Animal> Animals { get; private set; } = [];

	public List<Picture> Pictures { get; private set; } = [];

	public Company Company { get; private set; } = new();

	public long NextRowVersion { get; private set; }

	/// <summary>Runs <paramref name="action"/> under the data lock.</summary>
	public void Mutate(Action action)
	{
		lock (_sync)
		{
			action();
		}
	}

	public long BumpRowVersion() => ++NextRowVersion;

	public int NextProductId() => Products.Count == 0 ? 1 : Products.Max(x => x.ProductID) + 1;

	public int NextCategoryId() => Categories.Count == 0 ? 1 : Categories.Max(x => x.CategoryID) + 1;

	public int NextEmployeeId() => Employees.Count == 0 ? 1 : Employees.Max(x => x.EmployeeID) + 1;

	public int NextOrderId() => Orders.Count == 0 ? 1 : Orders.Max(x => x.OrderID) + 1;

	public int NextTransportId() => Transports.Count == 0 ? 1 : Transports.Max(x => x.TransportID) + 1;

	public int NextAnimalId() => Animals.Count == 0 ? 1 : Animals.Max(x => x.Id) + 1;

	public int NextPictureId() => Pictures.Count == 0 ? 1 : Pictures.Max(x => x.Id) + 1;

	public void Reset()
	{
		lock (_sync)
		{
			NextRowVersion = 1000;
			Seed();
		}
	}

	/// <summary>Re-links navigation properties from the foreign key values.</summary>
	public void Relink()
	{
		foreach (var category in Categories)
		{
			category.Products = Products.Where(x => x.CategoryID == category.CategoryID).ToList();
		}

		foreach (var supplier in Suppliers)
		{
			supplier.Products = Products.Where(x => x.SupplierID == supplier.SupplierID).ToList();
		}

		foreach (var product in Products)
		{
			product.Category = Categories.FirstOrDefault(x => x.CategoryID == product.CategoryID);
			product.Supplier = Suppliers.FirstOrDefault(x => x.SupplierID == product.SupplierID);
			product.Order_Details = OrderDetails.Where(x => x.ProductID == product.ProductID).ToList();
		}

		foreach (var employee in Employees)
		{
			employee.Superior = Employees.FirstOrDefault(x => x.EmployeeID == employee.ReportsTo);
			employee.Subordinates = Employees.Where(x => x.ReportsTo == employee.EmployeeID).ToList();
		}

		foreach (var customer in Customers)
		{
			customer.Orders = Orders.Where(x => x.CustomerID == customer.CustomerID).ToList();
		}

		foreach (var order in Orders)
		{
			order.Customer = Customers.FirstOrDefault(x => x.CustomerID == order.CustomerID);
			order.Employee = Employees.FirstOrDefault(x => x.EmployeeID == order.EmployeeID);
			order.Order_Details = OrderDetails.Where(x => x.OrderID == order.OrderID).ToList();
		}

		foreach (var detail in OrderDetails)
		{
			detail.Order = Orders.FirstOrDefault(x => x.OrderID == detail.OrderID);
			detail.Product = Products.FirstOrDefault(x => x.ProductID == detail.ProductID);
		}
	}

	private void Seed()
	{
		Categories =
		[
			new() { CategoryID = 1, CategoryName = "Beverages", Description = "Soft drinks, coffees, teas, beers, and ales" },
			new() { CategoryID = 2, CategoryName = "Condiments", Description = "Sweet and savory sauces, relishes, spreads, and seasonings" },
			new() { CategoryID = 3, CategoryName = "Confections", Description = "Desserts, candies, and sweet breads" },
			new() { CategoryID = 4, CategoryName = "Dairy Products", Description = "Cheeses" },
			new() { CategoryID = 5, CategoryName = "Grains/Cereals", Description = "Breads, crackers, pasta, and cereal" },
			new() { CategoryID = 6, CategoryName = "Meat/Poultry", Description = "Prepared meats" },
			new() { CategoryID = 7, CategoryName = "Produce", Description = "Dried fruit and bean curd" },
			new() { CategoryID = 8, CategoryName = "Seafood", Description = "Seaweed and fish" },
		];

		Suppliers =
		[
			new() { SupplierID = 1, CompanyName = "Exotic Liquids", ContactName = "Charlotte Cooper" },
			new() { SupplierID = 2, CompanyName = "New Orleans Cajun Delights", ContactName = "Shelley Burke" },
			new() { SupplierID = 3, CompanyName = "Grandma Kelly's Homestead", ContactName = "Regina Murphy" },
		];

		Products =
		[
			P(1, "Chai", 18m, 1, 1, 39, "10 boxes x 20 bags"),
			P(2, "Chang", 19m, 1, 1, 17, "24 - 12 oz bottles"),
			P(3, "Aniseed Syrup", 10m, 2, 1, 13, "12 - 550 ml bottles"),
			P(4, "Chef Anton's Cajun Seasoning", 22m, 2, 2, 53, "48 - 6 oz jars"),
			P(5, "Chef Anton's Gumbo Mix", 21.35m, 2, 2, 0, "36 boxes", discontinued: true),
			P(6, "Grandma's Boysenberry Spread", 25m, 2, 3, 120, "12 - 8 oz jars"),
			P(7, "Northwoods Cranberry Sauce", 40m, 2, 3, 6, "12 - 12 oz jars"),
			P(8, "Genen Shouyu", 15.5m, 2, 1, 39, "24 - 250 ml bottles"),
			P(9, "Gula Malacca", 19.45m, 2, 2, 27, "20 - 2 kg bags"),
			P(10, "Uncle Bob's Organic Dried Pears", 30m, 7, 3, 15, "12 - 1 lb pkgs."),
			P(11, "Queso Cabrales", 21m, 4, 1, 22, "1 kg pkg."),
			P(12, "Queso Manchego La Pastora", 38m, 4, 1, 86, "10 - 500 g pkgs."),
			P(13, "Konbu", 6m, 8, 2, 24, "2 kg box"),
			P(14, "Tofu", 23.25m, 7, 2, 35, "40 - 100 g pkgs."),
			P(15, "Pavlova", 17.45m, 3, 3, 29, "32 - 500 g boxes"),
			P(16, "Alice Mutton", 39m, 6, 3, 0, "20 - 1 kg tins", discontinued: true),
			P(17, "Carnarvon Tigers", 62.5m, 8, 1, 42, "16 kg pkg."),
			P(18, "Teatime Chocolate Biscuits", 9.2m, 3, 2, 25, "10 boxes x 12 pieces"),
			P(19, "Sir Rodney's Marmalade", 81m, 3, 3, 40, "30 gift boxes"),
			P(20, "Gustaf's Knäckebröd", 21m, 5, 1, 104, "24 - 500 g pkgs."),
			P(21, "Tunnbröd", 9m, 5, 2, 61, "12 - 250 g pkgs."),
			P(22, "Mishi Kobe Niku", 97m, 6, 3, 29, "18 - 500 g pkgs."),
		];

		Employees =
		[
			E(1, "Nancy", "Davolio", "Sales Representative", 2, new DateTime(1948, 12, 8), new DateTime(1992, 5, 1)),
			E(2, "Andrew", "Fuller", "Vice President, Sales", null, new DateTime(1952, 2, 19), new DateTime(1992, 8, 14)),
			E(3, "Janet", "Leverling", "Sales Representative", 2, new DateTime(1963, 8, 30), new DateTime(1992, 4, 1)),
			E(4, "Margaret", "Peacock", "Sales Representative", 2, new DateTime(1937, 9, 19), new DateTime(1993, 5, 3)),
			E(5, "Steven", "Buchanan", "Sales Manager", 2, new DateTime(1955, 3, 4), new DateTime(1993, 10, 17)),
			E(6, "Michael", "Suyama", "Sales Representative", 5, new DateTime(1963, 7, 2), new DateTime(1993, 10, 17)),
			E(7, "Robert", "King", "Sales Representative", 5, new DateTime(1960, 5, 29), new DateTime(1994, 1, 2)),
			E(8, "Laura", "Callahan", "Inside Sales Coordinator", 2, new DateTime(1958, 1, 9), new DateTime(1994, 3, 5)),
			E(9, "Anne", "Dodsworth", "Sales Representative", 5, new DateTime(1966, 1, 27), new DateTime(1994, 11, 15)),
		];

		Customers =
		[
			C("ALFKI", "Alfreds Futterkiste", "Maria Anders", "Obere Str. 57", "Berlin", "12209", "Germany", AddressType.Work),
			C("ANATR", "Ana Trujillo Emparedados y helados", "Ana Trujillo", "Avda. de la Constitución 2222", "México D.F.", "05021", "Mexico", AddressType.Home),
			C("ANTON", "Antonio Moreno Taquería", "Antonio Moreno", "Mataderos 2312", "México D.F.", "05023", "Mexico", AddressType.Shipping),
			C("AROUT", "Around the Horn", "Thomas Hardy", "120 Hanover Sq.", "London", "WA1 1DP", "UK", AddressType.Work),
			C("BERGS", "Berglunds snabbköp", "Christina Berglund", "Berguvsvägen 8", "Luleå", "S-958 22", "Sweden", AddressType.Work),
		];

		Orders =
		[
			O(10248, "ALFKI", 5, new DateTime(1996, 7, 4), new DateTime(1996, 7, 16), 32.38m, "Vins et alcools Chevalier"),
			O(10249, "ANATR", 6, new DateTime(1996, 7, 5), new DateTime(1996, 7, 10), 11.61m, "Toms Spezialitäten"),
			O(10250, "ANTON", 4, new DateTime(1996, 7, 8), new DateTime(1996, 7, 12), 65.83m, "Hanari Carnes"),
			O(10251, "AROUT", 3, new DateTime(1996, 7, 8), new DateTime(1996, 7, 15), 41.34m, "Victuailles en stock"),
			O(10252, "BERGS", 4, new DateTime(1996, 7, 9), new DateTime(1996, 7, 11), 51.3m, "Suprêmes délices"),
			O(10253, "ALFKI", 3, new DateTime(1996, 7, 10), new DateTime(1996, 7, 16), 58.17m, "Hanari Carnes"),
		];

		OrderDetails =
		[
			D(10248, 11, 14m, 12), D(10248, 1, 18m, 10),
			D(10249, 14, 23.25m, 9), D(10249, 2, 19m, 40),
			D(10250, 1, 18m, 10), D(10250, 12, 38m, 35),
			D(10251, 22, 97m, 6), D(10251, 3, 10m, 15),
			D(10252, 20, 21m, 40), D(10252, 4, 22m, 25),
			D(10253, 2, 19m, 20), D(10253, 6, 25m, 42),
		];

		Transports =
		[
			new Ship { TransportID = 1, ShipName = "Titanic" },
			new Ship { TransportID = 2, ShipName = "Queen Mary" },
			new Truck { TransportID = 3, TruckNumber = "TR-1" },
		];

		GuidKeyed =
		[
			new() { Key = new Guid("d8f3f70f-c185-49ab-9a92-0c86c344ab1b"), Name = "first" },
			new() { Key = new Guid("4f2b7b5a-2c6e-4d2a-9f1e-7a2b3c4d5e6f"), Name = "second" },
		];

		Animals =
		[
			new() { Id = 1, Name = "Dog", DynamicProperties = new Dictionary<string, object>(StringComparer.Ordinal) { ["Legs"] = 4, ["Sound"] = "Woof" } },
			new() { Id = 2, Name = "Bird", DynamicProperties = new Dictionary<string, object>(StringComparer.Ordinal) { ["Legs"] = 2, ["CanFly"] = true } },
		];

		Pictures =
		[
			new() { Id = 1, Name = "Logo", Media = "image-bytes"u8.ToArray(), ContentBytes = "content-bytes"u8.ToArray() },
		];

		Company = new Company
		{
			Name = "Northwind Traders",
			EmployeeCount = 9,
			Headquarters = new Address { Street = "1 Market St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "USA", Type = AddressType.Work },
		};

		Relink();
	}

	private Product P(int id, string name, decimal price, int categoryId, int supplierId, short stock, string quantityPerUnit, bool discontinued = false) => new()
	{
		ProductID = id,
		ProductName = name,
		EnglishName = name,
		UnitPrice = price,
		CategoryID = categoryId,
		SupplierID = supplierId,
		UnitsInStock = stock,
		UnitsOnOrder = 0,
		ReorderLevel = 10,
		QuantityPerUnit = quantityPerUnit,
		Discontinued = discontinued,
		RowVersion = BumpRowVersion(),
	};

	private static Employee E(int id, string first, string last, string title, int? reportsTo, DateTime birth, DateTime hire) => new()
	{
		EmployeeID = id,
		FirstName = first,
		LastName = last,
		Title = title,
		ReportsTo = reportsTo,
		BirthDate = new DateTimeOffset(birth, TimeSpan.Zero),
		HireDate = new DateTimeOffset(hire, TimeSpan.Zero),
		Address = $"{id} Main Street",
		HomePhone = $"(206) 555-00{id:00}",
		Extension = (400 + id).ToString(System.Globalization.CultureInfo.InvariantCulture),
		Notes = $"{first} {last} joined in {hire.Year}.",
	};

	private static Customer C(string id, string company, string contact, string street, string city, string postalCode, string country, AddressType type) => new()
	{
		CustomerID = id,
		CompanyName = company,
		ContactName = contact,
		Address = new Address { Street = street, City = city, PostalCode = postalCode, Country = country, Type = type },
		Addresses =
		[
			new CustomerAddress { Id = 1, Street = street, City = city },
			new CustomerAddress { Id = 2, Street = "PO Box " + id, City = city },
		],
	};

	private static Order O(int id, string customerId, int employeeId, DateTime ordered, DateTime shipped, decimal freight, string shipName) => new()
	{
		OrderID = id,
		CustomerID = customerId,
		EmployeeID = employeeId,
		OrderDate = new DateTimeOffset(ordered, TimeSpan.Zero),
		RequiredDate = new DateTimeOffset(ordered.AddDays(28), TimeSpan.Zero),
		ShippedDate = new DateTimeOffset(shipped, TimeSpan.Zero),
		ShippedDateTimeOffset = new DateTimeOffset(shipped, TimeSpan.Zero),
		Freight = freight,
		ShipName = shipName,
	};

	private static Order_Detail D(int orderId, int productId, decimal price, short quantity) => new()
	{
		OrderID = orderId,
		ProductID = productId,
		UnitPrice = price,
		Quantity = quantity,
		Discount = 0f,
	};
}
