using System.Collections.Concurrent;

namespace Simple.OData.Client;

/// <summary>
/// Memoizes the results of another <see cref="IPluralizer"/>.
/// </summary>
public sealed class CachedPluralizer(IPluralizer inner) : IPluralizer
{
	private readonly IPluralizer _inner = inner ?? throw new ArgumentNullException(nameof(inner));
	private readonly ConcurrentDictionary<string, string> _plurals = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, string> _singulars = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public string Pluralize(string word) => _plurals.GetOrAdd(word, static (w, p) => p.Pluralize(w), _inner);

	/// <inheritdoc />
	public string Singularize(string word) => _singulars.GetOrAdd(word, static (w, p) => p.Singularize(w), _inner);
}
