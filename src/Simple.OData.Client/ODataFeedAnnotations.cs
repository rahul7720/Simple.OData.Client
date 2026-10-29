namespace Simple.OData.Client;

/// <summary>
/// Control information returned with an OData feed (collection response).
/// </summary>
public sealed class ODataFeedAnnotations
{
	/// <summary>The id of the collection (<c>@odata.id</c>).</summary>
	public string? Id { get; set; }

	/// <summary>The total count of the collection when <c>$count=true</c> was requested (<c>@odata.count</c>).</summary>
	public long? Count { get; set; }

	/// <summary>The link used to retrieve changes since this response (<c>@odata.deltaLink</c>).</summary>
	public Uri? DeltaLink { get; set; }

	/// <summary>The link to the next page of a server-paged response (<c>@odata.nextLink</c>).</summary>
	public Uri? NextPageLink { get; set; }

	/// <summary>Custom instance annotations, as the underlying OData library exposes them.</summary>
	public IReadOnlyList<object>? InstanceAnnotations { get; set; }

	/// <summary>Returns the instance annotations cast to <typeparamref name="T"/>.</summary>
	public IEnumerable<T> GetInstanceAnnotations<T>() => InstanceAnnotations?.OfType<T>() ?? [];

	internal void CopyFrom(ODataFeedAnnotations? source)
	{
		Id = source?.Id;
		Count = source?.Count;
		DeltaLink = source?.DeltaLink;
		NextPageLink = source?.NextPageLink;
		InstanceAnnotations = source?.InstanceAnnotations;
	}

	internal void Merge(ODataFeedAnnotations? source)
	{
		if (source is null)
		{
			return;
		}

		Id ??= source.Id;
		Count ??= source.Count;
		DeltaLink ??= source.DeltaLink;
		NextPageLink ??= source.NextPageLink;
		InstanceAnnotations ??= source.InstanceAnnotations;
	}
}
