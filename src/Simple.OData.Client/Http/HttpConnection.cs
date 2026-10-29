using System.Net;

namespace Simple.OData.Client.Http;

/// <summary>
/// Owns the <see cref="System.Net.Http.HttpClient"/> used by a session. An externally supplied client is used as is and never disposed.
/// </summary>
internal sealed class HttpConnection : IDisposable
{
	private readonly bool _ownsClient;
	private HttpMessageHandler? _handler;

	public HttpConnection(ODataClientSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (settings.HttpClient is { } external)
		{
			HttpClient = external;
			return;
		}

		_handler = CreateHandler(settings);
		HttpClient = new HttpClient(_handler, disposeHandler: false);
		if (settings.RequestTimeout > TimeSpan.Zero)
		{
			HttpClient.Timeout = settings.RequestTimeout;
		}

		_ownsClient = true;
	}

	public HttpClient HttpClient { get; }

	public void Dispose()
	{
		if (_ownsClient)
		{
			HttpClient.Dispose();
		}

		_handler?.Dispose();
		_handler = null;
	}

	private static HttpMessageHandler CreateHandler(ODataClientSettings settings)
	{
		if (settings.OnCreateMessageHandler is { } factory)
		{
			return factory();
		}

		var handler = new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(2),
			AutomaticDecompression = DecompressionMethods.All,
			EnableMultipleHttp2Connections = true,
		};

		if (settings.Credentials is not null)
		{
			handler.Credentials = settings.Credentials;
			handler.PreAuthenticate = true;
		}

		settings.ConfigureHandler?.Invoke(handler);
		return handler;
	}
}
