using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.OData;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class ProductsController(NorthwindData data, AsyncMonitorStore monitor) : NorthwindController(data)
{
	[NorthwindQuery]
	public IActionResult Get()
	{
		if (Request.Headers["Prefer"].Any(x => x?.Contains("odata.track-changes", StringComparison.OrdinalIgnoreCase) == true))
		{
			Request.ODataFeature().DeltaLink = new Uri(ServiceRoot, $"Products/{NorthwindEdmModel.Namespace}.Changes()?$deltatoken=1");
			Response.Headers["Preference-Applied"] = "odata.track-changes";
		}

		return Ok(Data.Products.AsQueryable());
	}

	[NorthwindQuery]
	public IActionResult Get(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(product);
	}

	[HttpGet("odata/Products(ProductName={name})")]
	[NorthwindQuery]
	public IActionResult GetByName(string name)
	{
		var product = Data.Products.FirstOrDefault(x => x.ProductName == name);
		return product is null ? NotFound() : Ok(product);
	}

	[HttpPatch("odata/Products(ProductName={name})")]
	public IActionResult PatchByName(string name, [FromBody] Delta<Product> delta)
	{
		var product = Data.Products.FirstOrDefault(x => x.ProductName == name);
		return product is null ? NotFound() : Patch(product.ProductID, delta);
	}

	[HttpDelete("odata/Products(ProductName={name})")]
	public IActionResult DeleteByName(string name)
	{
		var product = Data.Products.FirstOrDefault(x => x.ProductName == name);
		return product is null ? NotFound() : Delete(product.ProductID);
	}

	[NorthwindQuery]
	public IActionResult GetCategory(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(SingleResult.Create(Data.Categories.Where(x => x.CategoryID == product.CategoryID).AsQueryable()));
	}

	[NorthwindQuery]
	public IActionResult GetSupplier(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(SingleResult.Create(Data.Suppliers.Where(x => x.SupplierID == product.SupplierID).AsQueryable()));
	}

	[NorthwindQuery]
	public IActionResult GetOrder_Details(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(Data.OrderDetails.Where(x => x.ProductID == key).AsQueryable());
	}

	/// <summary>Primitive property access: <c>Products(1)/ProductName</c> and <c>Products(1)/ProductName/$value</c>.</summary>
	public IActionResult GetProductName(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(product.ProductName);
	}

	public IActionResult GetUnitPrice(int key)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(product.UnitPrice);
	}

	public IActionResult Post([FromBody] Product product)
	{
		if (product is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (product.ProductID == 0 || Data.Products.Any(x => x.ProductID == product.ProductID))
			{
				product.ProductID = Data.NextProductId();
			}

			product.CategoryID ??= product.Category?.CategoryID;
			product.SupplierID ??= product.Supplier?.SupplierID;
			product.RowVersion = Data.BumpRowVersion();
			Data.Products.Add(product);
			Data.Relink();
		});

		return Created(product);
	}

	public IActionResult Patch(int key, [FromBody] Delta<Product> delta)
	{
		if (delta is null)
		{
			return BadRequest();
		}

		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		if (IfMatchFails(product.RowVersion))
		{
			return PreconditionFailed();
		}

		Data.Mutate(() =>
		{
			delta.Patch(product);
			ApplyNavigationChanges(product, delta);
			product.RowVersion = Data.BumpRowVersion();
			Data.Relink();
		});

		return Updated(product);
	}

	public IActionResult Put(int key, [FromBody] Product update)
	{
		if (update is null)
		{
			return BadRequest();
		}

		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		if (IfMatchFails(product.RowVersion))
		{
			return PreconditionFailed();
		}

		Data.Mutate(() =>
		{
			update.ProductID = key;
			update.RowVersion = Data.BumpRowVersion();
			update.CategoryID ??= update.Category?.CategoryID;
			update.SupplierID ??= update.Supplier?.SupplierID;
			Data.Products[Data.Products.IndexOf(product)] = update;
			Data.Relink();
		});

		return Updated(update);
	}

	public IActionResult Delete(int key)
	{
		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		if (IfMatchFails(product.RowVersion))
		{
			return PreconditionFailed();
		}

		Data.Mutate(() =>
		{
			Data.Products.Remove(product);
			Data.OrderDetails.RemoveAll(x => x.ProductID == key);
			Data.Relink();
		});

		return NoContent();
	}

	// ---- references --------------------------------------------------------------------------------------------

	public IActionResult CreateRef(int key, string navigationProperty, [FromBody] Uri link)
	{
		var product = Find(key);
		if (product is null || link is null)
		{
			return NotFound();
		}

		switch (navigationProperty)
		{
			case nameof(Product.Category):
				var categoryId = IntKeyFromLink(link);
				if (!Data.Categories.Any(x => x.CategoryID == categoryId))
				{
					return NotFound();
				}

				Data.Mutate(() =>
				{
					product.CategoryID = categoryId;
					Data.Relink();
				});
				return NoContent();
			case nameof(Product.Supplier):
				var supplierId = IntKeyFromLink(link);
				Data.Mutate(() =>
				{
					product.SupplierID = supplierId;
					Data.Relink();
				});
				return NoContent();
			default:
				return BadRequest();
		}
	}

	public IActionResult DeleteRef(int key, string navigationProperty)
	{
		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		switch (navigationProperty)
		{
			case nameof(Product.Category):
				Data.Mutate(() =>
				{
					product.CategoryID = null;
					Data.Relink();
				});
				return NoContent();
			case nameof(Product.Supplier):
				Data.Mutate(() =>
				{
					product.SupplierID = null;
					Data.Relink();
				});
				return NoContent();
			default:
				return BadRequest();
		}
	}

	// ---- operations --------------------------------------------------------------------------------------------

	[HttpGet]
	public IActionResult MostExpensive()
	{
		var product = Data.Products.OrderByDescending(x => x.UnitPrice).FirstOrDefault();
		return product is null ? NotFound() : Ok(product);
	}

	[HttpGet]
	public IActionResult Discounted(int key, [FromODataUri] decimal percent)
	{
		var product = Find(key);
		return product is null ? NotFound() : Ok(product.UnitPrice * (1 - percent / 100));
	}

	[HttpGet]
	[NorthwindQuery]
	public IActionResult Slow()
	{
		if (Request.Headers["Prefer"].Any(x => x?.Contains("respond-async", StringComparison.OrdinalIgnoreCase) == true))
		{
			var id = monitor.Create("/odata/Products");
			Response.Headers["Preference-Applied"] = "respond-async";
			Response.Headers.Location = AsyncMonitorStore.LocationFor(id);
			return StatusCode(StatusCodes.Status202Accepted);
		}

		return Ok(Data.Products.AsQueryable());
	}

	[HttpGet]
	public IActionResult Changes()
	{
		var changes = new DeltaSet<Product>();
		var first = Data.Products.OrderBy(x => x.ProductID).First();
		var changed = new Delta<Product>();
		changed.TrySetPropertyValue(nameof(Product.ProductID), first.ProductID);
		changed.TrySetPropertyValue(nameof(Product.ProductName), first.ProductName + " (changed)");
		changes.Add(changed);

		var deleted = new DeltaDeletedResource<Product> { Id = new Uri(ServiceRoot, "Products(2)"), Reason = DeltaDeletedEntryReason.Deleted };
		deleted.TrySetPropertyValue(nameof(Product.ProductID), 2);
		changes.Add(deleted);

		Request.ODataFeature().DeltaLink = new Uri(ServiceRoot, $"Products/{NorthwindEdmModel.Namespace}.Changes()?$deltatoken=2");
		return Ok(changes);
	}

	[HttpPost]
	public IActionResult Rate(int key, ODataActionParameters parameters)
	{
		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			product.Rating = Convert.ToInt32(parameters?["rating"], System.Globalization.CultureInfo.InvariantCulture);
			product.RowVersion = Data.BumpRowVersion();
		});
		return NoContent();
	}

	[HttpPost]
	public IActionResult CheckOut(int key) => ToggleCheckedOut(key, true);

	[HttpPost]
	public IActionResult Return(int key) => ToggleCheckedOut(key, false);

	[HttpPost]
	public IActionResult DiscontinueAll()
	{
		var count = 0;
		Data.Mutate(() =>
		{
			foreach (var product in Data.Products)
			{
				product.Discontinued = true;
				product.RowVersion = Data.BumpRowVersion();
				count++;
			}
		});
		return Ok(count);
	}

	private IActionResult ToggleCheckedOut(int key, bool checkedOut)
	{
		var product = Find(key);
		if (product is null)
		{
			return NotFound();
		}

		Data.Mutate(() =>
		{
			product.CheckedOut = checkedOut;
			product.RowVersion = Data.BumpRowVersion();
		});
		return Ok(product);
	}

	private Product? Find(int key) => Data.Products.FirstOrDefault(x => x.ProductID == key);

	private static void ApplyNavigationChanges(Product product, Delta<Product> delta)
	{
		var changed = delta.GetChangedPropertyNames().ToHashSet(StringComparer.Ordinal);
		if (changed.Contains(nameof(Product.Category)))
		{
			product.CategoryID = ReferencedKey(delta, nameof(Product.Category), nameof(Category.CategoryID), (Category c) => c.CategoryID) ?? product.Category?.CategoryID;
		}

		if (changed.Contains(nameof(Product.Supplier)))
		{
			product.SupplierID = ReferencedKey(delta, nameof(Product.Supplier), nameof(Supplier.SupplierID), (Supplier s) => s.SupplierID) ?? product.Supplier?.SupplierID;
		}
	}

	/// <summary>Reads the key of a navigation target bound with <c>@odata.bind</c>, whether it arrived as an entity or as a nested delta.</summary>
	private static int? ReferencedKey<TEntity>(Delta<Product> delta, string navigation, string keyName, Func<TEntity, int> keyOf)
		where TEntity : class
	{
		if (!delta.TryGetPropertyValue(navigation, out var value))
		{
			return null;
		}

		return value switch
		{
			TEntity entity => keyOf(entity),
			IDelta nested when nested.TryGetPropertyValue(keyName, out var key) && key is int id => id,
			_ => null,
		};
	}
}
