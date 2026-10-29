namespace Simple.OData.Client;

/// <summary>
/// A navigation link found in entry data that is being written.
/// </summary>
/// <param name="LinkName">The navigation property name.</param>
/// <param name="LinkData">The linked entity (an object, dictionary or key), or <see langword="null"/> to clear the link.</param>
/// <param name="ContentId">The batch Content-ID of the entry that owns the link, when inside a batch.</param>
public sealed record ReferenceLink(string LinkName, object? LinkData, string? ContentId);

/// <summary>
/// Entry data split into structural properties and navigation links according to the metadata.
/// </summary>
public sealed class EntryDetails
{
	/// <summary>Structural (and open-type) properties.</summary>
	public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

	/// <summary>Navigation links grouped by navigation property name.</summary>
	public IDictionary<string, List<ReferenceLink>> Links { get; } = new Dictionary<string, List<ReferenceLink>>(StringComparer.Ordinal);

	/// <summary>Whether <see cref="Properties"/> contains dynamic properties of an open type.</summary>
	public bool HasOpenTypeProperties { get; set; }

	/// <summary>Adds a structural property.</summary>
	public void AddProperty(string propertyName, object? propertyValue)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		Properties[propertyName] = propertyValue;
	}

	/// <summary>Adds a navigation link.</summary>
	public void AddLink(string linkName, object? linkData, string? contentId = null)
	{
		ArgumentNullException.ThrowIfNull(linkName);
		if (!Links.TryGetValue(linkName, out var links))
		{
			links = [];
			Links.Add(linkName, links);
		}

		links.Add(new ReferenceLink(linkName, linkData, contentId));
	}
}
