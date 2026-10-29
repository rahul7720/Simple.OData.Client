using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class AnimalsController(NorthwindData data) : NorthwindController(data)
{
	[EnableQuery]
	public IActionResult Get() => Ok(Data.Animals.AsQueryable());

	[EnableQuery]
	public IActionResult Get(int key)
	{
		var animal = Data.Animals.FirstOrDefault(x => x.Id == key);
		return animal is null ? NotFound() : Ok(animal);
	}

	public IActionResult Post([FromBody] Animal animal)
	{
		if (animal is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (animal.Id == 0 || Data.Animals.Any(x => x.Id == animal.Id))
			{
				animal.Id = Data.NextAnimalId();
			}

			Data.Animals.Add(animal);
		});
		return Created(animal);
	}

	public IActionResult Patch(int key, [FromBody] Delta<Animal> delta)
	{
		var animal = Data.Animals.FirstOrDefault(x => x.Id == key);
		if (animal is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() => delta.Patch(animal));
		return Updated(animal);
	}

	public IActionResult Delete(int key)
	{
		var animal = Data.Animals.FirstOrDefault(x => x.Id == key);
		if (animal is null)
		{
			return NotFound();
		}

		Data.Mutate(() => Data.Animals.Remove(animal));
		return NoContent();
	}
}
