using Microsoft.Spatial;

namespace Simple.OData.Client;

/// <summary>
/// Placeholders for OData built-in functions that have no natural .NET counterpart. Use them inside filter
/// expressions; they translate to the OData function and throw when invoked directly.
/// </summary>
public static class ODataFunctions
{
	/// <summary><c>matchesPattern(input, pattern)</c> (OData 4.01).</summary>
	public static bool MatchesPattern(string input, string pattern) => throw NotCallable();

	/// <summary><c>hassubset(set, subset)</c> (OData 4.01).</summary>
	public static bool HasSubset<T>(IEnumerable<T> set, IEnumerable<T> subset) => throw NotCallable();

	/// <summary><c>hassubsequence(sequence, subsequence)</c> (OData 4.01).</summary>
	public static bool HasSubsequence<T>(IEnumerable<T> sequence, IEnumerable<T> subsequence) => throw NotCallable();

	/// <summary><c>geo.distance(a, b)</c>.</summary>
	public static double GeoDistance(Geography a, Geography b) => throw NotCallable();

	/// <summary><c>geo.length(line)</c>.</summary>
	public static double GeoLength(Geography line) => throw NotCallable();

	/// <summary><c>geo.intersects(a, b)</c>.</summary>
	public static bool GeoIntersects(Geography a, Geography b) => throw NotCallable();

	/// <summary><c>now()</c>.</summary>
	public static DateTimeOffset Now() => throw NotCallable();

	/// <summary><c>mindatetime()</c>.</summary>
	public static DateTimeOffset MinDateTime() => throw NotCallable();

	/// <summary><c>maxdatetime()</c>.</summary>
	public static DateTimeOffset MaxDateTime() => throw NotCallable();

	/// <summary><c>totaloffsetminutes(value)</c>.</summary>
	public static int TotalOffsetMinutes(DateTimeOffset value) => throw NotCallable();

	/// <summary><c>totalseconds(duration)</c>.</summary>
	public static decimal TotalSeconds(TimeSpan duration) => throw NotCallable();

	/// <summary><c>fractionalseconds(value)</c>.</summary>
	public static decimal FractionalSeconds(DateTimeOffset value) => throw NotCallable();

	private static InvalidOperationException NotCallable() =>
		new("ODataFunctions are only valid inside OData expressions; they translate to OData built-in functions and cannot be invoked directly.");
}
