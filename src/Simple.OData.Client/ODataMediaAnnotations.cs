namespace Simple.OData.Client;

/// <summary>
/// Control information about a media resource or stream property.
/// </summary>
public sealed class ODataMediaAnnotations
{
	/// <summary>The media type of the stream (<c>@odata.mediaContentType</c>).</summary>
	public string? ContentType { get; set; }

	/// <summary>The link used to read the stream (<c>@odata.mediaReadLink</c>).</summary>
	public Uri? ReadLink { get; set; }

	/// <summary>The link used to replace the stream (<c>@odata.mediaEditLink</c>).</summary>
	public Uri? EditLink { get; set; }

	/// <summary>The entity tag of the stream (<c>@odata.mediaEtag</c>).</summary>
	public string? ETag { get; set; }
}
