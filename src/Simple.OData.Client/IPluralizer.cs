namespace Simple.OData.Client;

/// <summary>
/// Provides pluralization and singularization of words when resolving names of resources and properties.
/// </summary>
public interface IPluralizer
{
	/// <summary>Returns the plural form of <paramref name="word"/>.</summary>
	string Pluralize(string word);

	/// <summary>Returns the singular form of <paramref name="word"/>.</summary>
	string Singularize(string word);
}
