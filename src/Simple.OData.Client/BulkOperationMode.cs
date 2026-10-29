namespace Simple.OData.Client;

/// <summary>
/// How update and delete operations that address several entries by filter are executed.
/// </summary>
public enum BulkOperationMode
{
	/// <summary>Query the matching entries, then update or delete them one by one (default; works with every service).</summary>
	ClientSide,

	/// <summary>Send a single <c>PATCH</c> or <c>DELETE</c> to <c>EntitySet/$each?$filter=…</c> (OData 4.01).</summary>
	Each,
}
