using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class TransportController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.Transports.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var transport = Find(key);
		return transport is null ? NotFound() : Ok(transport);
	}

	[NorthwindQuery]
	public IActionResult GetFromShip() => Ok(Data.Transports.OfType<Ship>().AsQueryable());

	[NorthwindQuery]
	public IActionResult GetFromTruck() => Ok(Data.Transports.OfType<Truck>().AsQueryable());

	[NorthwindQuery]
	public IActionResult GetShip(int key) => Find(key) is Ship ship ? Ok(ship) : NotFound();

	[NorthwindQuery]
	public IActionResult GetTruck(int key) => Find(key) is Truck truck ? Ok(truck) : NotFound();

	public IActionResult Post([FromBody] Transport transport)
	{
		if (transport is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (transport.TransportID == 0 || Data.Transports.Any(x => x.TransportID == transport.TransportID))
			{
				transport.TransportID = Data.NextTransportId();
			}

			Data.Transports.Add(transport);
		});
		return Created(transport);
	}

	[HttpPost("odata/Transport/NorthwindModel.Ship")]
	public IActionResult PostShip([FromBody] Ship ship) => Post(ship);

	[HttpPost("odata/Transport/NorthwindModel.Truck")]
	public IActionResult PostTruck([FromBody] Truck truck) => Post(truck);

	[HttpPatch("odata/Transport({key})/NorthwindModel.Ship")]
	public IActionResult PatchShip(int key, [FromBody] Delta<Ship> delta)
	{
		var ship = Find(key) as Ship;
		if (ship is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() => delta.Patch(ship));
		return Updated(ship);
	}

	[HttpDelete("odata/Transport({key})/NorthwindModel.Ship")]
	public IActionResult DeleteShip(int key) => Find(key) is Ship ? Delete(key) : NotFound();

	public IActionResult Patch(int key, [FromBody] Delta<Transport> delta)
	{
		var transport = Find(key);
		if (transport is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() => delta.Patch(transport));
		return Updated(transport);
	}

	public IActionResult Delete(int key)
	{
		var transport = Find(key);
		if (transport is null)
		{
			return NotFound();
		}

		Data.Mutate(() => Data.Transports.Remove(transport));
		return NoContent();
	}

	private Transport? Find(int key) => Data.Transports.FirstOrDefault(x => x.TransportID == key);
}
