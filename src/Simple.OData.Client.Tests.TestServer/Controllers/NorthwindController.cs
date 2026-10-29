using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.UriParser;
using Simple.OData.Client.Tests.TestServer.Data;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

/// <summary>Shared helpers of the Northwind controllers: data access, ETag validation and <c>$ref</c> link parsing.</summary>
public abstract class NorthwindController(NorthwindData data) : ODataController
{
	protected NorthwindData Data { get; } = data;

	protected static Uri ServiceRoot { get; } = NorthwindTestServer.BaseAddress;

	protected IActionResult PreconditionFailed() => StatusCode(StatusCodes.Status412PreconditionFailed);

	/// <summary>Whether an <c>If-Match</c> header is present and does not match the entity's current ETag (<c>*</c> always matches).</summary>
	protected bool IfMatchFails(long rowVersion)
	{
		var ifMatch = Request.Headers.IfMatch;
		if (ifMatch.Count == 0 || ifMatch.Any(x => x == "*"))
		{
			return false;
		}

		var current = Request.CreateETag(new Dictionary<string, object>(StringComparer.Ordinal) { ["RowVersion"] = rowVersion }, TimeZoneInfo.Utc);
		return !ifMatch.Any(x => string.Equals(x, current, StringComparison.Ordinal));
	}

	/// <summary>Parses the key of the entity a <c>$ref</c> link points at.</summary>
	protected IReadOnlyDictionary<string, object> KeyFromLink(Uri link)
	{
		ArgumentNullException.ThrowIfNull(link);
		var absolute = link.IsAbsoluteUri ? link : new Uri(ServiceRoot, link);
		var parser = new ODataUriParser(Request.GetModel(), ServiceRoot, absolute);
		var path = parser.ParsePath();
		var keySegment = path.LastSegment as KeySegment ?? throw new InvalidOperationException($"The link {link} does not address an entity.");
		return keySegment.Keys.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
	}

	protected int IntKeyFromLink(Uri link) => Convert.ToInt32(KeyFromLink(link).Values.Single(), System.Globalization.CultureInfo.InvariantCulture);

	protected string StringKeyFromLink(Uri link) => (string)KeyFromLink(link).Values.Single();
}
