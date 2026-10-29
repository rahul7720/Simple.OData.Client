namespace Simple.OData.Client;

/// <summary>
/// Built-in <see cref="IPluralizer"/> instances.
/// </summary>
public static class Pluralizers
{
	/// <summary>The rule-based English pluralizer.</summary>
	public static IPluralizer Simple { get; } = new SimplePluralizer();

	/// <summary>The rule-based English pluralizer with memoized results (used by default).</summary>
	public static IPluralizer Cached { get; } = new CachedPluralizer(Simple);
}
