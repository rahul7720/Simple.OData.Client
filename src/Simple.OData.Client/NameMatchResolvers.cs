namespace Simple.OData.Client;

/// <summary>
/// Built-in <see cref="INameMatchResolver"/> instances.
/// </summary>
public static class ODataNameMatchResolver
{
	/// <summary>Names must match exactly (ordinal comparison).</summary>
	public static INameMatchResolver Strict { get; } = new ExactMatchResolver();

	/// <summary>Names match when their lower-case alphanumeric characters match (punctuation and whitespace ignored).</summary>
	public static INameMatchResolver Alphanumeric { get; } = new ExactMatchResolver(alphanumericComparison: true);

	/// <summary>Same as <see cref="Alphanumeric"/>; kept as a distinct instance for configuration clarity.</summary>
	public static INameMatchResolver AlphanumericCaseInsensitive { get; } = new ExactMatchResolver(alphanumericComparison: true, StringComparison.OrdinalIgnoreCase);

	/// <summary>Names match alphanumerically, or as singular/plural forms of each other (the default).</summary>
	public static INameMatchResolver NotStrict { get; } = new BestMatchResolver();

	internal static string StripNamespace(string name)
	{
		var index = name.LastIndexOf('.');
		return index < 0 ? name : name[(index + 1)..];
	}
}

/// <summary>
/// Matches names exactly, optionally after removing punctuation, whitespace and case differences.
/// </summary>
/// <param name="alphanumericComparison">When <see langword="true"/>, names are homogenized (lower-cased, punctuation and whitespace removed) before comparison.</param>
/// <param name="comparison">The string comparison used after optional homogenization.</param>
public sealed class ExactMatchResolver(bool alphanumericComparison = false, StringComparison comparison = StringComparison.Ordinal) : INameMatchResolver
{
	/// <inheritdoc />
	public bool IsMatch(string actualName, string requestedName)
	{
		ArgumentNullException.ThrowIfNull(actualName);
		ArgumentNullException.ThrowIfNull(requestedName);

		var actual = ODataNameMatchResolver.StripNamespace(actualName);
		var requested = ODataNameMatchResolver.StripNamespace(requestedName);
		if (alphanumericComparison)
		{
			actual = actual.Homogenize();
			requested = requested.Homogenize();
		}

		return actual.Equals(requested, comparison);
	}
}

/// <summary>
/// Matches homogenized names, treating singular and plural forms of a word as equivalent.
/// </summary>
public sealed class BestMatchResolver : INameMatchResolver
{
	private readonly IPluralizer _pluralizer;

	/// <summary>Creates a resolver that uses the cached built-in pluralizer.</summary>
	public BestMatchResolver() : this(Pluralizers.Cached)
	{
	}

	/// <summary>Creates a resolver that uses the supplied pluralizer.</summary>
	public BestMatchResolver(IPluralizer pluralizer)
	{
		ArgumentNullException.ThrowIfNull(pluralizer);
		_pluralizer = pluralizer;
	}

	/// <inheritdoc />
	public bool IsMatch(string actualName, string requestedName)
	{
		ArgumentNullException.ThrowIfNull(actualName);
		ArgumentNullException.ThrowIfNull(requestedName);

		var actual = ODataNameMatchResolver.StripNamespace(actualName).Homogenize();
		var requested = ODataNameMatchResolver.StripNamespace(requestedName).Homogenize();

		return actual == requested
			|| actual == _pluralizer.Singularize(requested)
			|| actual == _pluralizer.Pluralize(requested)
			|| _pluralizer.Singularize(actual) == requested
			|| _pluralizer.Pluralize(actual) == requested;
	}
}
