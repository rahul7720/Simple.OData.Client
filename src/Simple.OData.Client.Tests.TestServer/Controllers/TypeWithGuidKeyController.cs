using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class TypeWithGuidKeyController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.GuidKeyed.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(Guid key)
	{
		var item = Data.GuidKeyed.FirstOrDefault(x => x.Key == key);
		return item is null ? NotFound() : Ok(item);
	}

	public IActionResult Post([FromBody] TypeWithGuidKey item)
	{
		if (item is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (item.Key == Guid.Empty)
			{
				item.Key = Guid.NewGuid();
			}

			Data.GuidKeyed.Add(item);
		});
		return Created(item);
	}

	public IActionResult Delete(Guid key)
	{
		var item = Data.GuidKeyed.FirstOrDefault(x => x.Key == key);
		if (item is null)
		{
			return NotFound();
		}

		Data.Mutate(() => Data.GuidKeyed.Remove(item));
		return NoContent();
	}
}
