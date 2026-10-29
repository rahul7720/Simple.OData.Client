using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

/// <summary>Reads and writes the media resource or stream property addressed by a fluent command.</summary>
public sealed class MediaClient : FluentClientBase<IDictionary<string, object?>, MediaClient>, IMediaClient
{
	internal MediaClient(ODataClient client, FluentCommand? command = null, bool dynamicResults = false)
		: base(client, null, command, dynamicResults)
	{
	}

	/// <inheritdoc />
	public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default) => Client.GetMediaStreamAsync(Command, cancellationToken);

	/// <inheritdoc />
	public async Task<byte[]> GetStreamAsArrayAsync(CancellationToken cancellationToken = default)
	{
		using var stream = await Client.GetMediaStreamAsync(Command, cancellationToken).ConfigureAwait(false);
		return Utils.StreamToByteArray(stream);
	}

	/// <inheritdoc />
	public async Task<string> GetStreamAsStringAsync(CancellationToken cancellationToken = default)
	{
		using var stream = await Client.GetMediaStreamAsync(Command, cancellationToken).ConfigureAwait(false);
		return await Utils.StreamToStringAsync(stream, disposeStream: false, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task SetStreamAsync(Stream stream, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default) =>
		Client.SetMediaStreamAsync(Command, stream, contentType, optimisticConcurrency, cancellationToken);

	/// <inheritdoc />
	public Task SetStreamAsync(byte[] streamContent, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(streamContent);
		return Client.SetMediaStreamAsync(Command, Utils.ByteArrayToStream(streamContent), contentType, optimisticConcurrency, cancellationToken);
	}

	/// <inheritdoc />
	public Task SetStreamAsync(string streamContent, bool optimisticConcurrency = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(streamContent);
		return Client.SetMediaStreamAsync(Command, Utils.StringToStream(streamContent), "text/plain", optimisticConcurrency, cancellationToken);
	}
}
