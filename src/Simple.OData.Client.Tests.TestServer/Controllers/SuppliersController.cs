using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class SuppliersController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.Suppliers.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var supplier = Data.Suppliers.FirstOrDefault(x => x.SupplierID == key);
		return supplier is null ? NotFound() : Ok(supplier);
	}

	[NorthwindQuery]
	public IActionResult GetProducts(int key) => Ok(Data.Products.Where(x => x.SupplierID == key).AsQueryable());

	public IActionResult Post([FromBody] Supplier supplier)
	{
		if (supplier is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (supplier.SupplierID == 0 || Data.Suppliers.Any(x => x.SupplierID == supplier.SupplierID))
			{
				supplier.SupplierID = Data.Suppliers.Count == 0 ? 1 : Data.Suppliers.Max(x => x.SupplierID) + 1;
			}

			Data.Suppliers.Add(supplier);
			Data.Relink();
		});
		return Created(supplier);
	}

	public IActionResult Delete(int key)
	{
		var supplier = Data.Suppliers.FirstOrDefault(x => x.SupplierID == key);
		if (supplier is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.Suppliers.Remove(supplier);
			Data.Relink();
		});
		return NoContent();
	}
}
