namespace Simple.OData.Client.Serialization;

/// <summary>A node of the payload tree while it is being read: a feed, an entry or a nested property.</summary>
internal sealed class ResponseNode
{
	public AnnotatedFeed? Feed { get; set; }

	public AnnotatedEntry? Entry { get; set; }

	public string? LinkName { get; set; }

	public bool IsNull { get; set; }

	public bool IsDeleted { get; init; }

	public List<object?>? Primitives { get; set; }

	public string? PendingPropertyName { get; set; }
}
