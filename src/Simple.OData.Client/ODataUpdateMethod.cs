namespace Simple.OData.Client;

/// <summary>
/// The HTTP method preferred for entry updates.
/// </summary>
public enum ODataUpdateMethod
{
	/// <summary>Send only the changed properties with <c>PATCH</c> (default).</summary>
	Patch,

	/// <summary>Replace the entry with <c>PUT</c>.</summary>
	Put,
}
