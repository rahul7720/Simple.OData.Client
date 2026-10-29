using Microsoft.OData;

namespace Simple.OData.Client.Serialization;

/// <summary>An in-memory request message ODataLib writes payloads into.</summary>
internal sealed class ODataRequestMessage : IODataRequestMessage
{
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
	private MemoryStream? _stream;

	public IEnumerable<KeyValuePair<string, string>> Headers => _headers;

	public Uri Url { get; set; } = new("http://localhost/");

	public string Method { get; set; } = "GET";

	public string? GetHeader(string headerName) => _headers.GetValueOrDefault(headerName);

	public void SetHeader(string headerName, string headerValue) => _headers[headerName] = headerValue;

	public Stream GetStream() => _stream ??= new MemoryStream();

	public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default) => Task.FromResult(GetStream());

	/// <summary>The written payload rewound to its start, or <see langword="null"/> when nothing was written.</summary>
	public MemoryStream? TakeContent()
	{
		if (_stream is null || _stream.Length == 0)
		{
			return null;
		}

		_stream.Position = 0;
		return _stream;
	}
}
