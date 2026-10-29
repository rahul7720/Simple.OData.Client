using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class CategoriesController(NorthwindData data) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get() => Ok(Data.Categories.AsQueryable());

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var category = Find(key);
		return category is null ? NotFound() : Ok(category);
	}

	[NorthwindQuery]
	public IActionResult GetProducts(int key) => Find(key) is null ? NotFound() : Ok(Data.Products.Where(x => x.CategoryID == key).AsQueryable());

	public IActionResult Post([FromBody] Category category)
	{
		if (category is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (category.CategoryID == 0 || Data.Categories.Any(x => x.CategoryID == category.CategoryID))
			{
				category.CategoryID = Data.NextCategoryId();
			}

			Data.Categories.Add(category);
			foreach (var product in category.Products.ToList())
			{
				// a bound product (Products@odata.bind) arrives as a stub carrying only its key
				var existing = Data.Products.FirstOrDefault(x => x.ProductID == product.ProductID);
				if (existing is not null)
				{
					existing.CategoryID = category.CategoryID;
					existing.RowVersion = Data.BumpRowVersion();
					continue;
				}

				product.ProductID = product.ProductID == 0 ? Data.NextProductId() : product.ProductID;
				product.CategoryID = category.CategoryID;
				product.RowVersion = Data.BumpRowVersion();
				Data.Products.Add(product);
			}

			Data.Relink();
		});

		return Created(category);
	}

	public IActionResult Patch(int key, [FromBody] Delta<Category> delta)
	{
		var category = Find(key);
		if (category is null || delta is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			delta.Patch(category);
			if (delta.GetChangedPropertyNames().Contains(nameof(Category.Products)) && delta.TryGetPropertyValue(nameof(Category.Products), out var value) && value is IEnumerable<Product> products)
			{
				foreach (var product in products)
				{
					var existing = Data.Products.FirstOrDefault(x => x.ProductID == product.ProductID);
					if (existing is not null)
					{
						existing.CategoryID = key;
					}
				}
			}

			Data.Relink();
		});
		return Updated(category);
	}

	public IActionResult Delete(int key)
	{
		var category = Find(key);
		if (category is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			Data.Categories.Remove(category);
			foreach (var product in Data.Products.Where(x => x.CategoryID == key))
			{
				product.CategoryID = null;
			}

			Data.Relink();
		});
		return NoContent();
	}

	public IActionResult CreateRef(int key, string navigationProperty, [FromBody] Uri link)
	{
		var category = Find(key);
		if (category is null || link is null || navigationProperty != nameof(Category.Products))
		{
			return NotFound();
		}

		var productId = IntKeyFromLink(link);
		var product = Data.Products.FirstOrDefault(x => x.ProductID == productId);
		if (product is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			product.CategoryID = key;
			Data.Relink();
		});
		return NoContent();
	}

	public IActionResult DeleteRef(int key, string relatedKey, string navigationProperty)
	{
		var category = Find(key);
		if (category is null || navigationProperty != nameof(Category.Products))
		{
			return NotFound();
		}

		var productId = int.Parse(relatedKey, System.Globalization.CultureInfo.InvariantCulture);
		var product = Data.Products.FirstOrDefault(x => x.ProductID == productId && x.CategoryID == key);
		if (product is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			product.CategoryID = null;
			Data.Relink();
		});
		return NoContent();
	}

	private Category? Find(int key) => Data.Categories.FirstOrDefault(x => x.CategoryID == key);
}
