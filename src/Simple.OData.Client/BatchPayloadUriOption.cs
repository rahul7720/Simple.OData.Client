namespace Simple.OData.Client;

/// <summary>
/// The form of request URIs inside a multipart batch. Mirrors <c>Microsoft.OData.BatchPayloadUriOption</c>.
/// </summary>
public enum BatchPayloadUriOption
{
	/// <summary>Absolute URI with scheme, host, port and path (default).</summary>
	AbsoluteUri,

	/// <summary>Absolute resource path with a separate <c>Host</c> header.</summary>
	AbsoluteUriUsingHostHeader,

	/// <summary>Resource path relative to the batch request URI.</summary>
	RelativeUri,
}
