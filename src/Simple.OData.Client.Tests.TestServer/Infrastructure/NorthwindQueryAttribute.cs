using Microsoft.AspNetCore.OData.Query;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary><c>[EnableQuery]</c> with limits loose enough for the end-to-end scenarios (deep expansions, nested any/all).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NorthwindQueryAttribute : EnableQueryAttribute
{
	public NorthwindQueryAttribute()
	{
		MaxExpansionDepth = 10;
		MaxAnyAllExpressionDepth = 5;
		MaxNodeCount = 1000;
	}
}
