using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

/// <summary>Unbound functions and actions (operation imports).</summary>
public class ODataOperationImportController(NorthwindData data) : NorthwindController(data)
{
	// ---- functions ---------------------------------------------------------------------------------------------

	[HttpGet]
	public IActionResult PassThroughIntCollection([FromODataUri] IEnumerable<int> numbers) => Ok(numbers ?? []);

	[HttpGet]
	public IActionResult PassThroughString([FromODataUri] string value) => Ok(value);

	[HttpGet]
	public IActionResult PassThroughInt([FromODataUri] int value) => Ok(value);

	[HttpGet]
	public IActionResult PassThroughLong([FromODataUri] long value) => Ok(value);

	[HttpGet]
	public IActionResult PassThroughDateTime([FromODataUri] DateTimeOffset value) => Ok(value);

	[HttpGet]
	public IActionResult PassThroughGuid([FromODataUri] Guid value) => Ok(value);

	[HttpGet]
	public IActionResult PassThroughAddress([FromODataUri] Address address) => Ok(address);

	[HttpGet]
	public IActionResult ProductCount() => Ok(Data.Products.Count);

	[HttpGet]
	[NorthwindQuery]
	public IActionResult GetProductsByRating([FromODataUri] int rating) => Ok(Data.Products.Where(x => (x.Rating ?? 0) >= rating).AsQueryable());

	// ---- actions -----------------------------------------------------------------------------------------------

	[HttpPost]
	public IActionResult ResetData()
	{
		Data.Reset();
		return NoContent();
	}

	[HttpPost]
	public IActionResult Echo(ODataActionParameters parameters) => Ok(Convert.ToInt32(parameters?["value"], CultureInfo.InvariantCulture));

	[HttpPost]
	public IActionResult CreateProduct(ODataActionParameters parameters)
	{
		if (parameters is null)
		{
			return BadRequest();
		}

		var product = new Product
		{
			ProductName = (string)parameters["name"],
			UnitPrice = Convert.ToDecimal(parameters["price"], CultureInfo.InvariantCulture),
		};
		Data.Mutate(() =>
		{
			product.ProductID = Data.NextProductId();
			product.RowVersion = Data.BumpRowVersion();
			Data.Products.Add(product);
			Data.Relink();
		});
		return Created(product);
	}

	[HttpPost]
	[NorthwindQuery]
	public IActionResult ListProducts(ODataActionParameters parameters)
	{
		var categoryId = Convert.ToInt32(parameters?["categoryId"], CultureInfo.InvariantCulture);
		return Ok(Data.Products.Where(x => x.CategoryID == categoryId).AsQueryable());
	}

	[HttpPost]
	public IActionResult UpdateAddress(ODataActionParameters parameters) => Ok((Address)parameters!["address"]);

	[HttpPost]
	public IActionResult MergeProducts(ODataActionParameters parameters) => Ok(((IEnumerable<Product>)parameters!["products"]).Count());

	[HttpPost]
	public IActionResult GetAddressType() => Ok(AddressType.Shipping);

	[HttpPost]
	[NorthwindQuery]
	public IActionResult FilterCustomersByAddressType(ODataActionParameters parameters)
	{
		var type = (AddressType)parameters!["type"];
		return Ok(Data.Customers.Where(x => x.Address?.Type == type).AsQueryable());
	}
}
