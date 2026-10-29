using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

/// <summary>The <c>Company</c> singleton.</summary>
public class CompanyController(NorthwindData data) : NorthwindController(data)
{
	[EnableQuery]
	public IActionResult Get() => Ok(Data.Company);

	public IActionResult Patch([FromBody] Delta<Company> delta)
	{
		if (delta is null)
		{
			return BadRequest();
		}

		Data.Mutate(() => delta.Patch(Data.Company));
		return Updated(Data.Company);
	}
}
