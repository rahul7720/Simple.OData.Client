using System.Runtime.CompilerServices;

namespace Simple.OData.Client.Tests;

/// <summary>A fact that runs only when <c>ODATA_LIVE_TESTS=1</c>: the public services.odata.org endpoints are outside our control.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveFactAttribute : FactAttribute
{
	public const string Variable = "ODATA_LIVE_TESTS";

	public LiveFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		if (Environment.GetEnvironmentVariable(Variable) != "1")
		{
			Skip = $"Set {Variable}=1 to run against services.odata.org";
		}
	}
}
