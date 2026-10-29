namespace Simple.OData.Client;

/// <summary>
/// Selects what goes into the message of a <c>WebRequestException</c>.
/// </summary>
public enum WebRequestExceptionMessageSource
{
	/// <summary>The HTTP reason phrase (default).</summary>
	ReasonPhrase,

	/// <summary>The response body.</summary>
	ResponseContent,

	/// <summary>Both the reason phrase and the response body.</summary>
	Both,
}
