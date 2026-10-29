namespace Simple.OData.Client;

/// <summary>
/// An <see cref="IPluralizer"/> built from two delegates.
/// </summary>
/// <param name="pluralize">The pluralize function.</param>
/// <param name="singularize">The singularize function.</param>
public sealed class Pluralizer(Func<string, string> pluralize, Func<string, string> singularize) : IPluralizer
{
	private readonly Func<string, string> _pluralize = pluralize ?? throw new ArgumentNullException(nameof(pluralize));
	private readonly Func<string, string> _singularize = singularize ?? throw new ArgumentNullException(nameof(singularize));

	/// <inheritdoc />
	public string Pluralize(string word) => _pluralize(word);

	/// <inheritdoc />
	public string Singularize(string word) => _singularize(word);
}
