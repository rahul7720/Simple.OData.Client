using System.Net.Http.Headers;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

/// <summary>An OData request prepared by the client: method, relative command text, payload and headers.</summary>
public sealed class ODataRequest
{
	private readonly Uri _uri;
	private readonly Stream? _content;
	private readonly string? _contentType;
	private readonly List<string> _preferences = [];
	private HttpRequestMessage? _message;
	private bool _sent;

	internal ODataRequest(string method, Session session, string commandText, IDictionary<string, string>? headers = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(method);
		ArgumentNullException.ThrowIfNull(session);
		ArgumentNullException.ThrowIfNull(commandText);
		Method = method;
		CommandText = commandText;
		_uri = Uri.TryCreate(commandText, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
			? absolute
			: Utils.CreateAbsoluteUri(session.BaseUri, commandText);
		Headers = headers is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
	}

	internal ODataRequest(string method, Session session, string commandText, HttpRequestMessage requestMessage)
		: this(method, session, commandText)
	{
		_message = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
	}

	internal ODataRequest(
		string method,
		Session session,
		string commandText,
		IDictionary<string, object?>? entryData,
		Stream? contentStream,
		string? mediaType = null,
		IDictionary<string, string>? headers = null)
		: this(method, session, commandText, headers)
	{
		EntryData = entryData;
		_content = contentStream;
		_contentType = mediaType;
	}

	/// <summary>The HTTP request message, created on first access from the request parts.</summary>
	public HttpRequestMessage RequestMessage => _message ??= CreateMessage();

	/// <summary>The message to send now: the current one, or a fresh copy when it has already been sent.</summary>
	internal HttpRequestMessage PrepareForSend()
	{
		var message = RequestMessage;
		if (_sent)
		{
			message = _message = CloneMessage(message);
		}

		_sent = true;
		return message;
	}

	/// <summary>The relative command text (resource path and query options).</summary>
	public string CommandText { get; }

	/// <summary>The HTTP method.</summary>
	public string Method { get; }

	/// <summary>The entry data the payload was written from, when any.</summary>
	public IDictionary<string, object?>? EntryData { get; }

	/// <summary>Whether the request manipulates a navigation link (<c>$ref</c>).</summary>
	public bool IsLink { get; init; }

	/// <summary>Whether the response is a single scalar value (<c>$count</c>, <c>$value</c>).</summary>
	public bool ReturnsScalarResult { get; init; }

	/// <summary>Whether a response body is expected.</summary>
	public bool ResultRequired { get; init; }

	/// <summary>Whether the target requires an <c>If-Match</c> header (optimistic concurrency).</summary>
	public bool CheckOptimisticConcurrency { get; init; }

	/// <summary>An explicit <c>If-Match</c> value.</summary>
	public string? IfMatch { get; set; }

	/// <summary>An explicit <c>If-None-Match</c> value.</summary>
	public string? IfNoneMatch { get; set; }

	/// <summary>Additional request headers.</summary>
	public IDictionary<string, string> Headers { get; }

	/// <summary>The <c>Prefer</c> header values.</summary>
	public IReadOnlyList<string> Preferences => _preferences;

	/// <summary>The media types acceptable in the response, or <see langword="null"/> when no body is expected.</summary>
	public IReadOnlyList<string>? Accept => ReturnsScalarResult || !ResultRequired ? null : [HttpLiteral.ApplicationJson];

	internal void AddPreference(string preference)
	{
		if (!_preferences.Contains(preference, StringComparer.OrdinalIgnoreCase))
		{
			_preferences.Add(preference);
		}
	}

	private HttpRequestMessage CloneMessage(HttpRequestMessage source)
	{
		var message = CreateMessage();
		foreach (var (name, values) in source.Headers)
		{
			if (!message.Headers.Contains(name))
			{
				message.Headers.TryAddWithoutValidation(name, values);
			}
		}

		if (message.Content is not null && source.Content is not null)
		{
			foreach (var (name, values) in source.Content.Headers)
			{
				if (!message.Content.Headers.Contains(name))
				{
					message.Content.Headers.TryAddWithoutValidation(name, values);
				}
			}
		}

		return message;
	}

	private HttpRequestMessage CreateMessage()
	{
		var message = new HttpRequestMessage(new HttpMethod(Method), _uri);
		if (_content is not null)
		{
			// payloads written by ODataLib sit in memory: share the buffer so the request can be sent again
			HttpContent content = _content is MemoryStream buffered && buffered.TryGetBuffer(out var segment)
				? new ReadOnlyMemoryContent(segment.AsMemory())
				: new StreamContent(_content);
			if (_content.CanSeek && content is StreamContent)
			{
				_content.Position = 0;
				content.Headers.ContentLength = _content.Length;
			}

			content.Headers.ContentType = MediaTypeHeaderValue.Parse(_contentType ?? HttpLiteral.ApplicationJson);
			message.Content = content;
		}

		foreach (var (name, value) in Headers)
		{
			if (message.Content is not null && name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
			{
				message.Content.Headers.TryAddWithoutValidation(name, value);
			}
			else
			{
				message.Headers.TryAddWithoutValidation(name, value);
			}
		}

		if (_preferences.Count > 0)
		{
			message.Headers.TryAddWithoutValidation(HttpLiteral.Prefer, string.Join(',', _preferences));
		}

		if (IfMatch is not null)
		{
			message.Headers.TryAddWithoutValidation(HttpLiteral.IfMatch, IfMatch);
		}

		if (IfNoneMatch is not null)
		{
			message.Headers.TryAddWithoutValidation(HttpLiteral.IfNoneMatch, IfNoneMatch);
		}

		return message;
	}
}
