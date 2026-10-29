namespace Simple.OData.Client;

/// <summary>
/// HTTP header names and values used by the client.
/// </summary>
internal static class HttpLiteral
{
	public const string ContentType = "Content-Type";
	public const string ContentLength = "Content-Length";
	public const string ContentId = "Content-ID";
	public const string ODataVersion = "OData-Version";
	public const string ODataMaxVersion = "OData-MaxVersion";
	public const string ODataEntityId = "OData-EntityId";
	public const string IfMatch = "If-Match";
	public const string IfNoneMatch = "If-None-Match";
	public const string Prefer = "Prefer";
	public const string PreferenceApplied = "Preference-Applied";
	public const string Location = "Location";
	public const string RetryAfter = "Retry-After";
	public const string ETag = "ETag";
	public const string ReturnRepresentation = "return=representation";
	public const string ReturnMinimal = "return=minimal";
	public const string RespondAsync = "respond-async";
	public const string OmitNullValues = "omit-values=nulls";
	public const string ContinueOnError = "odata.continue-on-error";
	public const string TrackChanges = "odata.track-changes";
	public const string MaxPageSize = "odata.maxpagesize";
	public const string IncludeAnnotations = "odata.include-annotations";
	public const string ApplicationJson = "application/json";
	public const string ApplicationXml = "application/xml";
	public const string ApplicationHttp = "application/http";
	public const string MultipartMixed = "multipart/mixed";
}
