using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Microsoft.AspNetCore.OData.Results;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class EmployeesController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.Employees.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var employee = Find(key);
		return employee is null ? NotFound() : Ok(employee);
	}

	[NorthwindQuery]
	public IActionResult GetSuperior(int key)
	{
		var employee = Find(key);
		return employee is null ? NotFound() : Ok(SingleResult.Create(Data.Employees.Where(x => x.EmployeeID == employee.ReportsTo).AsQueryable()));
	}

	[NorthwindQuery]
	public IActionResult GetSubordinates(int key) => Find(key) is null ? NotFound() : Ok(Data.Employees.Where(x => x.ReportsTo == key).AsQueryable());

	[HttpGet("odata/Employees({key})/Superior/Superior")]
	[NorthwindQuery]
	public IActionResult GetSuperiorOfSuperior(int key)
	{
		var grandSuperior = Find(Find(key)?.ReportsTo)?.ReportsTo is { } id ? Find(id) : null;
		return grandSuperior is null ? NotFound() : Ok(grandSuperior);
	}

	[HttpGet("odata/Employees({key})/Superior/Superior/Subordinates")]
	[NorthwindQuery]
	public IActionResult GetSubordinatesOfSuperiorOfSuperior(int key)
	{
		var grandSuperior = Find(Find(key)?.ReportsTo)?.ReportsTo is { } id ? Find(id) : null;
		return grandSuperior is null ? NotFound() : Ok(Data.Employees.Where(x => x.ReportsTo == grandSuperior.EmployeeID).AsQueryable());
	}

	[HttpGet("odata/Employees({key})/Superior/Superior/Subordinates({subordinateKey})")]
	[NorthwindQuery]
	public IActionResult GetSubordinateOfSuperiorOfSuperior(int key, int subordinateKey)
	{
		var grandSuperior = Find(Find(key)?.ReportsTo)?.ReportsTo is { } id ? Find(id) : null;
		var subordinate = grandSuperior is null ? null : Data.Employees.FirstOrDefault(x => x.ReportsTo == grandSuperior.EmployeeID && x.EmployeeID == subordinateKey);
		return subordinate is null ? NotFound() : Ok(subordinate);
	}

	public IActionResult Post([FromBody] Employee employee)
	{
		if (employee is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (employee.EmployeeID == 0 || Data.Employees.Any(x => x.EmployeeID == employee.EmployeeID))
			{
				employee.EmployeeID = Data.NextEmployeeId();
			}

			employee.ReportsTo ??= employee.Superior?.EmployeeID;
			Data.Employees.Add(employee);
			foreach (var subordinate in employee.Subordinates.Where(x => x.EmployeeID == 0))
			{
				subordinate.EmployeeID = Data.NextEmployeeId();
				subordinate.ReportsTo = employee.EmployeeID;
				Data.Employees.Add(subordinate);
			}

			Data.Relink();
		});
		return Created(employee);
	}

	public IActionResult Patch(int key, [FromBody] Delta<Employee> delta)
	{
		var employee = Find(key);
		if (employee is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			delta.Patch(employee);
			if (delta.GetChangedPropertyNames().Contains(nameof(Employee.Superior)) && delta.TryGetPropertyValue(nameof(Employee.Superior), out var superior))
			{
				employee.ReportsTo = (superior as Employee)?.EmployeeID;
			}

			Data.Relink();
		});
		return Updated(employee);
	}

	public IActionResult Delete(int key)
	{
		var employee = Find(key);
		if (employee is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.Employees.Remove(employee);
			foreach (var subordinate in Data.Employees.Where(x => x.ReportsTo == key))
			{
				subordinate.ReportsTo = null;
			}

			Data.Relink();
		});
		return NoContent();
	}

	public IActionResult CreateRef(int key, string navigationProperty, [FromBody] Uri link)
	{
		var employee = Find(key);
		if (employee is null || link is null)
		{
			return NotFound();
		}

		var relatedId = IntKeyFromLink(link);
		var related = Find(relatedId);
		if (related is null)
		{
			return NotFound();
		}

		switch (navigationProperty)
		{
			case nameof(Employee.Superior):
				Data.Mutate(() =>
				{
					employee.ReportsTo = relatedId;
					Data.Relink();
				});
				return NoContent();
			case nameof(Employee.Subordinates):
				Data.Mutate(() =>
				{
					related.ReportsTo = key;
					Data.Relink();
				});
				return NoContent();
			default:
				return BadRequest();
		}
	}

	public IActionResult DeleteRef(int key, string navigationProperty)
	{
		var employee = Find(key);
		if (employee is null || navigationProperty != nameof(Employee.Superior))
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			employee.ReportsTo = null;
			Data.Relink();
		});
		return NoContent();
	}

	public IActionResult DeleteRef(int key, string relatedKey, string navigationProperty)
	{
		var employee = Find(key);
		if (employee is null || navigationProperty != nameof(Employee.Subordinates))
		{
			return NotFound();
		}

		var subordinate = Find(int.Parse(relatedKey, System.Globalization.CultureInfo.InvariantCulture));
		if (subordinate is null || subordinate.ReportsTo != key)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			subordinate.ReportsTo = null;
			Data.Relink();
		});
		return NoContent();
	}

	private Employee? Find(int? key) => key is null ? null : Data.Employees.FirstOrDefault(x => x.EmployeeID == key);
}
