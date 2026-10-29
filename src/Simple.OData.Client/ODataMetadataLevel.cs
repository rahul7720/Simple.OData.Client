namespace Simple.OData.Client;

/// <summary>
/// The amount of control information requested in JSON responses (<c>odata.metadata</c> format parameter).
/// </summary>
public enum ODataMetadataLevel
{
	/// <summary><c>odata.metadata=minimal</c> (default).</summary>
	Minimal,

	/// <summary><c>odata.metadata=full</c>: every entry carries type, id and link annotations.</summary>
	Full,

	/// <summary><c>odata.metadata=none</c>: no control information; the client infers types from the request.</summary>
	None,
}
