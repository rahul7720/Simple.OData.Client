namespace Simple.OData.Client;

/// <summary>
/// Dictionary keys the client reserves for data it attaches to entries.
/// </summary>
internal static class ReservedNames
{
	public const string Annotations = "__annotations";
	public const string Result = "__result";
	public const string MediaEntity = "__entity";
	public const string ODataType = "@odata.type";
	public const string ODataTypeShort = "@type";
	public const string ODataId = "@odata.id";
	public const string ODataIdShort = "@id";
}
