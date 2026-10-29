using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.OData.Edm;
using Simple.OData.Client.Http;
using Simple.OData.Client.Metadata;

namespace Simple.OData.Client;

/// <summary>
/// Resolves and caches the service metadata for one <see cref="ODataClientSettings"/> and owns the HTTP connection.
/// </summary>
internal sealed class Session : ISession, IDisposable
{
	private readonly SemaphoreSlim _initialization = new(1, 1);
	private readonly Lock _connectionLock = new();
	private MetadataResolver? _metadata;
	private CachedEdmModel? _cached;
	private HttpConnection? _connection;

	public Session(ODataClientSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (settings.BaseUri is null || string.IsNullOrEmpty(settings.BaseUri.AbsoluteUri))
		{
			throw new InvalidOperationException("Unable to create client session with no URI specified.");
		}

		Settings = settings;
		BaseUri = settings.BaseUri;

		if (settings.MetadataModel is { } model)
		{
			SetModel(new CachedEdmModel(CacheKey, string.Empty, model));
		}
		else if (!string.IsNullOrWhiteSpace(settings.MetadataDocument))
		{
			SetModel(EdmModelCache.GetOrAdd(DocumentKey(settings.MetadataDocument), settings.MetadataDocument, MetadataLoader.Parse));
		}
	}

	public static Session FromMetadata(Uri baseUri, string metadataDocument) =>
		new(new ODataClientSettings { BaseUri = baseUri, MetadataDocument = metadataDocument });

	public ODataClientSettings Settings { get; }

	public Uri BaseUri { get; }

	public bool IsInitialized => _metadata is not null;

	public IMetadata Metadata => _metadata ?? throw NotInitialized();

	internal MetadataResolver Resolver => _metadata ?? throw NotInitialized();

	public IEdmModel Model => _cached?.Model ?? throw NotInitialized();

	public string MetadataDocument => _cached?.Document ?? throw NotInitialized();

	public ITypeCache TypeCache => Settings.TypeCache;

	internal string CacheKey => BaseUri.AbsoluteUri;

	public HttpConnection HttpConnection
	{
		get
		{
			if (_connection is { } connection)
			{
				return connection;
			}

			lock (_connectionLock)
			{
				return _connection ??= new HttpConnection(Settings);
			}
		}
	}

	public async ValueTask InitializeAsync(CancellationToken cancellationToken)
	{
		if (IsInitialized)
		{
			return;
		}

		await _initialization.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (IsInitialized)
			{
				return;
			}

			var cached = await EdmModelCache.GetOrAddAsync(CacheKey, DownloadMetadataAsync, MetadataLoader.Parse, cancellationToken).ConfigureAwait(false);
			SetModel(cached);
		}
		finally
		{
			_initialization.Release();
		}
	}

	public void ClearMetadataCache()
	{
		EdmModelCache.Remove(CacheKey);
		_metadata = null;
		_cached = null;
	}

	public void Trace(string format, params object?[] args) => Settings.OnTrace?.Invoke(format, args);

	public bool IsTracing(ODataTrace flag) => Settings.OnTrace is not null && (Settings.TraceFilter & flag) == flag;

	public void Dispose()
	{
		_connection?.Dispose();
		_initialization.Dispose();
	}

	private void SetModel(CachedEdmModel cached)
	{
		_cached = cached;
		_metadata = new MetadataResolver(cached.Model, Settings.NameMatchResolver, Settings.IgnoreUnmappedProperties, Settings.UnqualifiedNameCall);
	}

	internal Uri MetadataUri
	{
		get
		{
			var path = ODataLiteral.Metadata;
			if (!string.IsNullOrEmpty(Settings.SchemaVersion))
			{
				path += "?" + ODataLiteral.SchemaVersion + "=" + Uri.EscapeDataString(Settings.SchemaVersion);
			}

			return Utils.CreateAbsoluteUri(BaseUri, path);
		}
	}

	private async Task<string> DownloadMetadataAsync(CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, MetadataUri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Settings.MetadataFormat == MetadataFormat.Json ? "application/json" : "application/xml"));
		request.Headers.TryAddWithoutValidation(HttpLiteral.ODataVersion, Settings.ProtocolVersion == ODataProtocolVersion.V401 ? "4.01" : "4.0");
		request.Headers.TryAddWithoutValidation(HttpLiteral.ODataMaxVersion, "4.01");

		Settings.BeforeRequest?.Invoke(request);
		if (Settings.BeforeRequestAsync is { } beforeAsync)
		{
			await beforeAsync(request, cancellationToken).ConfigureAwait(false);
		}

		Trace("{0} request: {1}", request.Method, request.RequestUri!.AbsoluteUri);
		using var response = Settings.RequestExecutor is { } executor
			? await executor(request).ConfigureAwait(false)
			: await HttpConnection.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
		Trace("Request completed: {0}", response.StatusCode);

		Settings.AfterResponse?.Invoke(response);
		if (Settings.AfterResponseAsync is { } afterAsync)
		{
			await afterAsync(response, cancellationToken).ConfigureAwait(false);
		}

		if (!response.IsSuccessStatusCode)
		{
			var body = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			throw new HttpRequestException($"Unable to download metadata from {request.RequestUri}: {(int)response.StatusCode} {response.ReasonPhrase}. {body}", null, response.StatusCode);
		}

		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
	}

	private static string DocumentKey(string document)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(document));
		return "document:" + Convert.ToHexString(hash);
	}

	private static InvalidOperationException NotInitialized() =>
		new("The session metadata has not been initialized. Call InitializeAsync or supply MetadataDocument/MetadataModel in the settings.");
}
