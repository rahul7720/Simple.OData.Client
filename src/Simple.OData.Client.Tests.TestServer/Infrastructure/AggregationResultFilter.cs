using System.Collections;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query.Wrapper;
using Microsoft.OData.UriParser;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>
/// Serializes <c>$apply</c> results (aggregation wrappers) as an OData JSON feed. The OData output formatter of the
/// preview package does not accept the wrapper types, so this filter runs after <c>[EnableQuery]</c> and writes the
/// payload itself: <c>{"@odata.context":"…$metadata#Products(CategoryID,Total)","value":[…]}</c>.
/// </summary>
public sealed class AggregationResultFilter : IActionFilter, IOrderedFilter
{
	/// <summary>Runs after <c>EnableQueryAttribute</c> (order 0) in the "executed" phase.</summary>
	public int Order => -100;

	public void OnActionExecuting(ActionExecutingContext context)
	{
	}

	public void OnActionExecuted(ActionExecutedContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (context.Result is not ObjectResult { Value: IEnumerable items and not string })
		{
			return;
		}

		var wrappers = items.Cast<object?>().ToList();
		if (wrappers.Count == 0 && !IsWrapperCollection(items.GetType()) || wrappers.Count > 0 && wrappers.Any(x => x is not DynamicTypeWrapper))
		{
			return;
		}

		var request = context.HttpContext.Request;
		var entitySet = request.ODataFeature().Path?.FirstSegment is EntitySetSegment segment ? segment.EntitySet.Name : "Products";
		var rows = wrappers.Cast<DynamicTypeWrapper>().Select(ToDictionary).ToList();
		var projection = rows.Count > 0 ? "(" + string.Join(',', rows[0].Keys) + ")" : string.Empty;
		var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["@odata.context"] = new Uri(NorthwindTestServer.BaseAddress, "$metadata#" + entitySet + projection).AbsoluteUri,
			["value"] = rows,
		};

		context.HttpContext.Response.Headers["OData-Version"] = "4.0";
		context.Result = new ContentResult
		{
			StatusCode = 200,
			ContentType = "application/json;odata.metadata=minimal",
			Content = JsonSerializer.Serialize(payload),
		};
	}

	private static bool IsWrapperCollection(Type type)
	{
		var element = type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;
		return element is not null && typeof(DynamicTypeWrapper).IsAssignableFrom(element);
	}

	private static Dictionary<string, object?> ToDictionary(DynamicTypeWrapper wrapper)
	{
		var result = new Dictionary<string, object?>(StringComparer.Ordinal);
		foreach (var (name, value) in wrapper.Values)
		{
			result[name] = value is DynamicTypeWrapper nested ? ToDictionary(nested) : value;
		}

		return result;
	}
}
