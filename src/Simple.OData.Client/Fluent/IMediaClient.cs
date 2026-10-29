namespace Simple.OData.Client;

/// <summary>Reads and writes media resources and stream properties.</summary>
public interface IMediaClient
{
	/// <summary>Reads the stream.</summary>
	Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default);

	/// <summary>Reads the stream into a byte array.</summary>
	Task<byte[]> GetStreamAsArrayAsync(CancellationToken cancellationToken = default);

	/// <summary>Reads the stream as UTF-8 text.</summary>
	Task<string> GetStreamAsStringAsync(CancellationToken cancellationToken = default);

	/// <summary>Writes the stream with the given content type.</summary>
	Task SetStreamAsync(Stream stream, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default);

	/// <summary>Writes the bytes with the given content type.</summary>
	Task SetStreamAsync(byte[] streamContent, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default);

	/// <summary>Writes the text as <c>text/plain</c>.</summary>
	Task SetStreamAsync(string streamContent, bool optimisticConcurrency = false, CancellationToken cancellationToken = default);

	/// <summary>Formats the request URL relative to the service root.</summary>
	Task<string> GetCommandTextAsync(CancellationToken cancellationToken = default);
}
