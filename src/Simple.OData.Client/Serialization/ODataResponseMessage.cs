using Microsoft.OData;
using Microsoft.OData.Edm;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client.Serialization;

/// <summary>Adapts an <see cref="HttpResponseMessage"/> for ODataLib, buffering the body so it can be inspected and re-read.</summary>
internal sealed class ODataResponseMessage(HttpResponseMessage response) : IODataResponseMessage, IDisposable
{
	private MemoryStream? _buffer;

	/// <summary>The navigation source the payload is expected to come from, used when the payload carries no context URL.</summary>
	public IEdmNavigationSource? ExpectedNavigationSource { get; set; }

	/// <summary>The structured type the payload is expected to describe.</summary>
	public IEdmStructuredType? ExpectedType { get; set; }

	public IEnumerable<KeyValuePair<string, string>> Headers
	{
		get
		{
			foreach (var header in response.Headers)
			{
				yield return new KeyValuePair<string, string>(header.Key, header.Value.FirstOrDefault() ?? string.Empty);
			}

			if (response.Content is not null)
			{
				foreach (var header in response.Content.Headers)
				{
					yield return new KeyValuePair<string, string>(header.Key, header.Value.FirstOrDefault() ?? string.Empty);
				}
			}
		}
	}

	public int StatusCode
	{
		get => (int)response.StatusCode;
		set => throw new NotSupportedException("The status code of a received response cannot be changed.");
	}

	public string? GetHeader(string headerName)
	{
		if (response.Content is not null && response.Content.Headers.TryGetValues(headerName, out var contentValues))
		{
			return contentValues.FirstOrDefault();
		}

		return response.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() : null;
	}

	public void SetHeader(string headerName, string headerValue) => throw new NotSupportedException("Headers of a received response cannot be changed.");

	public Stream GetStream()
	{
		if (_buffer is null)
		{
			_buffer = new MemoryStream();
			response.Content?.CopyTo(_buffer, null, CancellationToken.None);
		}

		_buffer.Position = 0;
		return _buffer;
	}

	public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
	{
		await BufferAsync(cancellationToken).ConfigureAwait(false);
		_buffer!.Position = 0;
		return _buffer;
	}

	/// <summary>Buffers the body and returns whether it has any content.</summary>
	public async ValueTask<bool> HasContentAsync(CancellationToken cancellationToken)
	{
		await BufferAsync(cancellationToken).ConfigureAwait(false);
		return _buffer!.Length > 0;
	}

	/// <summary>The buffered body bytes.</summary>
	public ReadOnlyMemory<byte> Content => _buffer is null ? ReadOnlyMemory<byte>.Empty : _buffer.GetBuffer().AsMemory(0, (int)_buffer.Length);

	/// <summary>The <c>Content-Type</c> header.</summary>
	public string? ContentType => GetHeader(HttpLiteral.ContentType);

	private async ValueTask BufferAsync(CancellationToken cancellationToken)
	{
		if (_buffer is not null)
		{
			return;
		}

		var buffer = new MemoryStream();
		if (response.Content is not null)
		{
			await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		_buffer = buffer;
	}

	/// <summary>
	/// A request-message view over the same buffered body. ODataLib does not require a context URL in request
	/// payloads, which lets <c>odata.metadata=none</c> responses be read against an expected entity set.
	/// </summary>
	public IODataRequestMessage AsRequest() => new RequestView(this);

	public void Dispose()
	{
		_buffer?.Dispose();
		_buffer = null;
	}

	private sealed class RequestView(ODataResponseMessage owner) : IODataRequestMessage
	{
		public IEnumerable<KeyValuePair<string, string>> Headers => owner.Headers;

		public Uri Url { get; set; } = new("http://localhost/");

		public string Method { get; set; } = RestVerbs.Get;

		public string? GetHeader(string headerName) => owner.GetHeader(headerName);

		public void SetHeader(string headerName, string headerValue) => owner.SetHeader(headerName, headerValue);

		public Stream GetStream() => owner.GetStream();

		public Task<Stream> GetStreamAsync(CancellationToken cancellationToken) => owner.GetStreamAsync(cancellationToken);
	}
}
