namespace Simple.OData.Client;

/// <summary>An entry removed from a tracked collection.</summary>
/// <param name="Id">The entity id (URL) of the removed entry.</param>
/// <param name="Reason"><c>deleted</c> when the entity was deleted, <c>changed</c> when it left the collection.</param>
/// <param name="Data">Key or other property values sent with the removal, if any.</param>
public sealed record ODataDeletedEntry(string? Id, string? Reason, IDictionary<string, object?>? Data);

/// <summary>A link added to or removed from a navigation property.</summary>
public sealed record ODataDeltaLink(Uri Source, string Relationship, Uri Target);

/// <summary>The content of a delta response: changed and deleted entities, link changes and the next delta link.</summary>
public sealed class ODataDelta
{
	/// <summary>Entities that were added or changed.</summary>
	public required IReadOnlyList<IDictionary<string, object?>> Changed { get; init; }

	/// <summary>Entities that were deleted or left the collection.</summary>
	public required IReadOnlyList<ODataDeletedEntry> Deleted { get; init; }

	/// <summary>Links that were added.</summary>
	public required IReadOnlyList<ODataDeltaLink> AddedLinks { get; init; }

	/// <summary>Links that were removed.</summary>
	public required IReadOnlyList<ODataDeltaLink> DeletedLinks { get; init; }

	/// <summary>The link to request the next set of changes.</summary>
	public Uri? DeltaLink { get; init; }

	/// <summary>The link to the next page of this delta response.</summary>
	public Uri? NextPageLink { get; init; }
}
