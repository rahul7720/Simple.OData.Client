using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Microsoft.AspNetCore.OData.Results;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class OrdersController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.Orders.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var order = Find(key);
		return order is null ? NotFound() : Ok(order);
	}

	[NorthwindQuery]
	public IActionResult GetOrder_Details(int key) => Find(key) is null ? NotFound() : Ok(Data.OrderDetails.Where(x => x.OrderID == key).AsQueryable());

	[NorthwindQuery]
	public IActionResult GetCustomer(int key)
	{
		var order = Find(key);
		return order is null ? NotFound() : Ok(SingleResult.Create(Data.Customers.Where(x => x.CustomerID == order.CustomerID).AsQueryable()));
	}

	[NorthwindQuery]
	public IActionResult GetEmployee(int key)
	{
		var order = Find(key);
		return order is null ? NotFound() : Ok(SingleResult.Create(Data.Employees.Where(x => x.EmployeeID == order.EmployeeID).AsQueryable()));
	}

	public IActionResult Post([FromBody] Order order)
	{
		if (order is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (order.OrderID == 0 || Data.Orders.Any(x => x.OrderID == order.OrderID))
			{
				order.OrderID = Data.NextOrderId();
			}

			order.CustomerID ??= order.Customer?.CustomerID;
			order.EmployeeID ??= order.Employee?.EmployeeID;
			Data.Orders.Add(order);
			foreach (var detail in order.Order_Details)
			{
				detail.OrderID = order.OrderID;
				if (!Data.OrderDetails.Any(x => x.OrderID == detail.OrderID && x.ProductID == detail.ProductID))
				{
					Data.OrderDetails.Add(detail);
				}
			}

			Data.Relink();
		});
		return Created(order);
	}

	public IActionResult Patch(int key, [FromBody] Delta<Order> delta)
	{
		var order = Find(key);
		if (order is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			delta.Patch(order);
			Data.Relink();
		});
		return Updated(order);
	}

	public IActionResult Delete(int key)
	{
		var order = Find(key);
		if (order is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.Orders.Remove(order);
			Data.OrderDetails.RemoveAll(x => x.OrderID == key);
			Data.Relink();
		});
		return NoContent();
	}

	private Order? Find(int key) => Data.Orders.FirstOrDefault(x => x.OrderID == key);
}
