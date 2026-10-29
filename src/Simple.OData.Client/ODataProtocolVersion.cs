namespace Simple.OData.Client;

/// <summary>
/// The OData protocol version the client speaks.
/// </summary>
public enum ODataProtocolVersion
{
	/// <summary>OData 4.0 (default). Requests carry <c>OData-Version: 4.0</c> and <c>OData-MaxVersion: 4.01</c>.</summary>
	V4,

	/// <summary>OData 4.01. Enables JSON batch, <c>$each</c>, deep update and 4.01 payload conventions.</summary>
	V401,
}
