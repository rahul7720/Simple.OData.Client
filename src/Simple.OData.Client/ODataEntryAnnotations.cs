namespace Simple.OData.Client;

/// <summary>
/// Control information (<c>@odata.*</c> annotations) returned with an OData entry.
/// </summary>
public sealed class ODataEntryAnnotations
{
	/// <summary>Describes a related entity link.</summary>
	public sealed record AssociationLink(string Name, Uri Uri);

	/// <summary>The entity id (<c>@odata.id</c>).</summary>
	public string? Id { get; set; }

	/// <summary>The qualified type name of the entry (<c>@odata.type</c>).</summary>
	public string? TypeName { get; set; }

	/// <summary>The link used to read the entry (<c>@odata.readLink</c>).</summary>
	public Uri? ReadLink { get; set; }

	/// <summary>The link used to edit the entry (<c>@odata.editLink</c>).</summary>
	public Uri? EditLink { get; set; }

	/// <summary>The entity tag (<c>@odata.etag</c>).</summary>
	public string? ETag { get; set; }

	/// <summary>Association links of the entry, when present.</summary>
	public IReadOnlyList<AssociationLink>? AssociationLinks { get; set; }

	/// <summary>Media resource information for media entities.</summary>
	public ODataMediaAnnotations? MediaResource { get; set; }

	/// <summary>Custom instance annotations, as the underlying OData library exposes them (<see cref="Microsoft.OData.ODataInstanceAnnotation"/>).</summary>
	public IReadOnlyList<object>? InstanceAnnotations { get; set; }

	/// <summary>Returns the instance annotations cast to <typeparamref name="T"/>.</summary>
	public IEnumerable<T> GetInstanceAnnotations<T>() => InstanceAnnotations?.OfType<T>() ?? [];

	internal void Merge(ODataEntryAnnotations? source)
	{
		if (source is null)
		{
			return;
		}

		Id ??= source.Id;
		TypeName ??= source.TypeName;
		ReadLink ??= source.ReadLink;
		EditLink ??= source.EditLink;
		ETag ??= source.ETag;
		AssociationLinks ??= source.AssociationLinks;
		MediaResource ??= source.MediaResource;
		InstanceAnnotations ??= source.InstanceAnnotations;
	}
}
