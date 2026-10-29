using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Infrastructure;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

/// <summary>OData 4.01 <c>$each</c> on the Products set: PATCH/DELETE every product matching <c>$filter</c>.</summary>
[ApiController]
[Route("odata/Products/$each")]
public sealed class ProductsEachController(NorthwindData data, IEdmModel model) : ControllerBase
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

	[HttpPatch]
	public async Task<IActionResult> Patch()
	{
		using var document = await JsonDocument.ParseAsync(Request.Body).ConfigureAwait(false);
		var targets = Select().ToList();
		data.Mutate(() =>
		{
			foreach (var product in targets)
			{
				Apply(product, document.RootElement);
				product.RowVersion = data.BumpRowVersion();
			}

			data.Relink();
		});

		if (!Request.Headers["Prefer"].Any(x => x?.Contains("return=representation", StringComparison.OrdinalIgnoreCase) == true))
		{
			return NoContent();
		}

		var payload = new
		{
			context = new Uri(NorthwindTestServer.BaseAddress, "$metadata#Products").AbsoluteUri,
			value = targets.Select(Project).ToList(),
		};
		var json = JsonSerializer.Serialize(payload).Replace("\"context\":", "\"@odata.context\":", StringComparison.Ordinal);
		Response.Headers["OData-Version"] = "4.01";
		return Content(json, "application/json;odata.metadata=minimal");
	}

	[HttpDelete]
	public IActionResult Delete()
	{
		var targets = Select().ToList();
		data.Mutate(() =>
		{
			foreach (var product in targets)
			{
				data.Products.Remove(product);
				data.OrderDetails.RemoveAll(x => x.ProductID == product.ProductID);
			}

			data.Relink();
		});
		Response.Headers["OData-Version"] = "4.01";
		return NoContent();
	}

	private IEnumerable<Product> Select()
	{
		var filter = Request.Query["$filter"].FirstOrDefault();
		if (string.IsNullOrEmpty(filter))
		{
			return data.Products;
		}

		var parser = new ODataUriParser(model, new Uri("Products?$filter=" + Uri.EscapeDataString(filter), UriKind.Relative));
		var clause = parser.ParseFilter();
		var context = new QueryBinderContext(model, new ODataQuerySettings(), typeof(Product));
		var binder = new FilterBinder();
		return (IQueryable<Product>)binder.ApplyBind(data.Products.AsQueryable(), clause, context);
	}

	private static void Apply(Product product, JsonElement body)
	{
		foreach (var property in body.EnumerateObject())
		{
			if (property.Name.StartsWith('@') || property.Name.Contains('@', StringComparison.Ordinal))
			{
				continue;
			}

			var target = typeof(Product).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (target is null || !target.CanWrite || target.PropertyType.IsClass && target.PropertyType != typeof(string))
			{
				continue;
			}

			target.SetValue(product, JsonSerializer.Deserialize(property.Value.GetRawText(), target.PropertyType, JsonOptions));
		}
	}

	private static Dictionary<string, object?> Project(Product product) => new(StringComparer.Ordinal)
	{
		["ProductID"] = product.ProductID,
		["ProductName"] = product.ProductName,
		["EnglishName"] = product.EnglishName,
		["QuantityPerUnit"] = product.QuantityPerUnit,
		["UnitPrice"] = product.UnitPrice,
		["UnitsInStock"] = product.UnitsInStock,
		["UnitsOnOrder"] = product.UnitsOnOrder,
		["ReorderLevel"] = product.ReorderLevel,
		["Discontinued"] = product.Discontinued,
		["CategoryID"] = product.CategoryID,
		["SupplierID"] = product.SupplierID,
		["Rating"] = product.Rating,
		["CheckedOut"] = product.CheckedOut,
		["RowVersion"] = product.RowVersion,
	};
}
