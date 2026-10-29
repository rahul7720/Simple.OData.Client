using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Query;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>
/// <c>[EnableQuery]</c> with limits loose enough for the end-to-end scenarios (deep expansions, nested any/all) and
/// server-driven paging when the client prefers <c>odata.maxpagesize</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NorthwindQueryAttribute : EnableQueryAttribute
{
	private const string MaxPageSizePreference = "odata.maxpagesize=";

	public NorthwindQueryAttribute()
	{
		MaxExpansionDepth = 10;
		MaxAnyAllExpressionDepth = 5;
		MaxNodeCount = 1000;
		// a configured page size makes EnableQuery run the query pipeline even without query options,
		// which is what lets a preferred odata.maxpagesize page a plain collection request
		PageSize = 100_000;
	}

	public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
	{
		ArgumentNullException.ThrowIfNull(queryOptions);
		if (TryGetPreferredPageSize(queryOptions.Request, out var pageSize))
		{
			queryOptions.Request.HttpContext.Response.Headers.Append("Preference-Applied", MaxPageSizePreference + pageSize);
			return queryOptions.ApplyTo(queryable, new ODataQuerySettings { PageSize = pageSize });
		}

		return base.ApplyQuery(queryable, queryOptions);
	}

	private static bool TryGetPreferredPageSize(HttpRequest request, out int pageSize)
	{
		pageSize = 0;
		foreach (var header in request.Headers["Prefer"])
		{
			foreach (var part in (header ?? string.Empty).Split(','))
			{
				var preference = part.Trim();
				if (preference.StartsWith(MaxPageSizePreference, StringComparison.OrdinalIgnoreCase)
					&& int.TryParse(preference[MaxPageSizePreference.Length..], out pageSize) && pageSize > 0)
				{
					return true;
				}
			}
		}

		return false;
	}
}
