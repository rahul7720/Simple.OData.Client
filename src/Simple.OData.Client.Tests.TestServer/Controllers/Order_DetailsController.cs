using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Microsoft.AspNetCore.OData.Results;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class Order_DetailsController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.OrderDetails.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int keyOrderID, int keyProductID)
	{
		var detail = Find(keyOrderID, keyProductID);
		return detail is null ? NotFound() : Ok(detail);
	}

	[NorthwindQuery]
	public IActionResult GetProduct(int keyOrderID, int keyProductID) =>
		Find(keyOrderID, keyProductID) is null ? NotFound() : Ok(SingleResult.Create(Data.Products.Where(x => x.ProductID == keyProductID).AsQueryable()));

	[NorthwindQuery]
	public IActionResult GetOrder(int keyOrderID, int keyProductID) =>
		Find(keyOrderID, keyProductID) is null ? NotFound() : Ok(SingleResult.Create(Data.Orders.Where(x => x.OrderID == keyOrderID).AsQueryable()));

	public IActionResult Post([FromBody] Order_Detail detail)
	{
		if (detail is null)
		{
			return BadRequest();
		}

		if (Find(detail.OrderID, detail.ProductID) is not null)
		{
			return Conflict();
		}

		Data.Mutate(() =>
		{
			Data.OrderDetails.Add(detail);
			Data.Relink();
		});
		return Created(detail);
	}

	public IActionResult Patch(int keyOrderID, int keyProductID, [FromBody] Delta<Order_Detail> delta)
	{
		var detail = Find(keyOrderID, keyProductID);
		if (detail is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() => delta.Patch(detail));
		return Updated(detail);
	}

	public IActionResult Delete(int keyOrderID, int keyProductID)
	{
		var detail = Find(keyOrderID, keyProductID);
		if (detail is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.OrderDetails.Remove(detail);
			Data.Relink();
		});
		return NoContent();
	}

	private Order_Detail? Find(int orderId, int productId) => Data.OrderDetails.FirstOrDefault(x => x.OrderID == orderId && x.ProductID == productId);
}
