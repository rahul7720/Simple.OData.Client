using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class CustomersController(NorthwindData data) : NorthwindController(data)
{
	[EnableQuery]
	public IActionResult Get() => Ok(Data.Customers.AsQueryable());

	[EnableQuery]
	public IActionResult Get(string key)
	{
		var customer = Find(key);
		return customer is null ? NotFound() : Ok(customer);
	}

	[EnableQuery]
	public IActionResult GetOrders(string key) => Find(key) is null ? NotFound() : Ok(Data.Orders.Where(x => x.CustomerID == key).AsQueryable());

	[EnableQuery]
	public IActionResult GetAddresses(string key)
	{
		var customer = Find(key);
		return customer is null ? NotFound() : Ok(customer.Addresses.AsQueryable());
	}

	[HttpGet("odata/Customers({key})/Addresses({addressKey})")]
	[EnableQuery]
	public IActionResult GetAddress(string key, int addressKey)
	{
		var address = Find(key)?.Addresses.FirstOrDefault(x => x.Id == addressKey);
		return address is null ? NotFound() : Ok(address);
	}

	[HttpPost("odata/Customers({key})/Addresses")]
	public IActionResult PostToAddresses(string key, [FromBody] CustomerAddress address)
	{
		var customer = Find(key);
		if (customer is null || address is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			if (address.Id == 0 || customer.Addresses.Any(x => x.Id == address.Id))
			{
				address.Id = customer.Addresses.Count == 0 ? 1 : customer.Addresses.Max(x => x.Id) + 1;
			}

			customer.Addresses.Add(address);
		});
		return Created(address);
	}

	public IActionResult Post([FromBody] Customer customer)
	{
		if (customer is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (string.IsNullOrEmpty(customer.CustomerID))
			{
				customer.CustomerID = customer.CompanyName.Length >= 5 ? customer.CompanyName[..5].ToUpperInvariant() : Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
			}

			Data.Customers.Add(customer);
			Data.Relink();
		});
		return Created(customer);
	}

	public IActionResult Patch(string key, [FromBody] Delta<Customer> delta)
	{
		var customer = Find(key);
		if (customer is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() => delta.Patch(customer));
		return Updated(customer);
	}

	public IActionResult Delete(string key)
	{
		var customer = Find(key);
		if (customer is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.Customers.Remove(customer);
			Data.Relink();
		});
		return NoContent();
	}

	private Customer? Find(string key) => Data.Customers.FirstOrDefault(x => x.CustomerID == key);
}
