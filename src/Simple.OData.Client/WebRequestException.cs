using System.Net;

namespace Simple.OData.Client;

/// <summary>Raised when the service answers with a non-success status code.</summary>
public sealed class WebRequestException : Exception
{
	private WebRequestException(string? reasonPhrase, HttpStatusCode statusCode, Uri? requestUri, string? responseContent, ODataErrorDetails? error, WebRequestExceptionMessageSource messageSource, Exception? inner)
		: base(BuildMessage(statusCode, reasonPhrase, responseContent, error, messageSource), inner)
	{
		Code = statusCode;
		ReasonPhrase = reasonPhrase;
		Response = responseContent;
		RequestUri = requestUri;
		Error = error;
	}

	/// <summary>The HTTP status code.</summary>
	public HttpStatusCode Code { get; }

	/// <summary>The HTTP reason phrase.</summary>
	public string? ReasonPhrase { get; }

	/// <summary>The response body as text, when there was one.</summary>
	public string? Response { get; }

	/// <summary>The request URI.</summary>
	public Uri? RequestUri { get; }

	/// <summary>The parsed OData error payload, when the service returned one.</summary>
	public ODataErrorDetails? Error { get; }

	/// <summary>Creates the exception from a response, reading its body.</summary>
	public static async Task<WebRequestException> CreateFromResponseMessageAsync(
		HttpResponseMessage response,
		WebRequestExceptionMessageSource messageSource,
		ODataErrorDetails? error = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(response);
		var content = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return new WebRequestException(response.ReasonPhrase, response.StatusCode, response.RequestMessage?.RequestUri, content, error, messageSource, null);
	}

	/// <summary>Creates the exception from a status code and optional response content.</summary>
	public static WebRequestException CreateFromStatusCode(
		HttpStatusCode statusCode,
		WebRequestExceptionMessageSource messageSource,
		string? responseContent = null,
		ODataErrorDetails? error = null) =>
		new(statusCode.ToString(), statusCode, null, responseContent, error, messageSource, null);

	private static string BuildMessage(HttpStatusCode statusCode, string? reasonPhrase, string? responseContent, ODataErrorDetails? error, WebRequestExceptionMessageSource messageSource)
	{
		reasonPhrase = string.IsNullOrEmpty(reasonPhrase) ? statusCode.ToString() : reasonPhrase;
		var content = string.IsNullOrEmpty(responseContent) ? error?.ToString() : responseContent;
		return messageSource switch
		{
			WebRequestExceptionMessageSource.ReasonPhrase => reasonPhrase,
			WebRequestExceptionMessageSource.ResponseContent => string.IsNullOrEmpty(content) ? reasonPhrase : content,
			_ => $"Request failed with reason {(int)statusCode} {reasonPhrase}. Response content was {content}.",
		};
	}
}
