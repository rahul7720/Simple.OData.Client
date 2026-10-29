using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.OData.Edm;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Http;
using Simple.OData.Client.Metadata;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client;

/// <summary>
/// The OData V4 / V4.01 client. One instance serves one service; it is safe to share between threads and should be
/// disposed when it was created from a <see cref="Uri"/> or <see cref="ODataClientSettings"/> without an external <see cref="HttpClient"/>.
/// </summary>
public sealed partial class ODataClient : IODataClient, IDisposable
{
	private readonly bool _ownsSession;

	/// <summary>Creates a client for the service at <paramref name="baseUri"/>.</summary>
	public ODataClient(Uri baseUri)
		: this(new ODataClientSettings(baseUri))
	{
	}

	/// <summary>Creates a client with the given settings.</summary>
	public ODataClient(ODataClientSettings settings)
		: this(settings, batchEntries: null)
	{
	}

	/// <summary>Creates a client that sends requests through <paramref name="httpClient"/>, optionally with further settings.</summary>
	public ODataClient(HttpClient httpClient, ODataClientSettings? settings = null)
		: this(SettingsFor(httpClient, settings))
	{
	}

	internal ODataClient(ODataClientSettings settings, ConcurrentDictionary<object, IDictionary<string, object?>>? batchEntries)
	{
		ArgumentNullException.ThrowIfNull(settings);
		Settings = settings;
		Session = new Session(settings);
		_ownsSession = true;
		Runner = new RequestRunner(Session);
		SetBatchEntries(batchEntries);
	}

	/// <summary>Creates a client sharing the session of <paramref name="client"/>; with <paramref name="batchEntries"/> it writes operations into a batch instead of sending them.</summary>
	internal ODataClient(ODataClient client, ConcurrentDictionary<object, IDictionary<string, object?>>? batchEntries = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		Settings = client.Settings;
		Session = client.Session;
		Runner = client.Runner;
		SetBatchEntries(batchEntries);
	}

	/// <summary>Creates a client that answers every operation from one operation response of a batch.</summary>
	internal ODataClient(ODataClient client, ODataResponse batchResponse)
	{
		ArgumentNullException.ThrowIfNull(client);
		Settings = client.Settings;
		Session = client.Session;
		Runner = client.Runner;
		BatchResponse = batchResponse ?? throw new ArgumentNullException(nameof(batchResponse));
	}

	internal ODataClientSettings Settings { get; }

	internal Session Session { get; }

	internal RequestRunner Runner { get; }

	internal RequestWriter RequestWriter => new(Session, BatchWriter);

	internal ConcurrentDictionary<object, IDictionary<string, object?>>? BatchEntries { get; private set; }

	internal Lazy<BatchWriter>? BatchWriter { get; private set; }

	internal ODataResponse? BatchResponse { get; }

	/// <summary>Whether operations are written into a batch instead of being sent.</summary>
	internal bool IsBatchRequest => BatchWriter is not null;

	/// <summary>Whether operations are answered from a batch response.</summary>
	internal bool IsBatchResponse => BatchResponse is not null;

	private bool IncludeAnnotations => Settings.IncludeAnnotationsInResults;

	// ---- static ------------------------------------------------------------------------------------------------

	/// <summary>Downloads and parses the metadata of the service at <paramref name="baseUri"/>.</summary>
	public static async Task<IEdmModel> GetMetadataAsync(Uri baseUri, CancellationToken cancellationToken = default)
	{
		using var session = new Session(new ODataClientSettings(baseUri));
		await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return session.Model;
	}

	/// <summary>Downloads the metadata document of the service at <paramref name="baseUri"/>.</summary>
	public static async Task<string> GetMetadataDocumentAsync(Uri baseUri, CancellationToken cancellationToken = default)
	{
		using var session = new Session(new ODataClientSettings(baseUri));
		await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return session.MetadataDocument;
	}

	/// <summary>Parses a CSDL XML or CSDL JSON document.</summary>
	public static IEdmModel ParseMetadata(string csdl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(csdl);
		return MetadataLoader.Parse(csdl);
	}

	/// <summary>Removes every cached metadata model; the next client resolving a service downloads it again.</summary>
	public static void ClearMetadataCache() => EdmModelCache.Clear();

	// ---- fluent entry points -----------------------------------------------------------------------------------

	/// <inheritdoc />
	public IBoundClient<IDictionary<string, object?>> For(string collectionName)
	{
		ArgumentException.ThrowIfNullOrEmpty(collectionName);
		return new BoundClient<IDictionary<string, object?>>(this).For(collectionName);
	}

	/// <inheritdoc />
	public IBoundClient<ODataEntry> For(ODataExpression expression) => new BoundClient<ODataEntry>(this, dynamicResults: true).For(expression);

	/// <inheritdoc />
	public IBoundClient<T> For<T>(string? collectionName = null)
		where T : class => new BoundClient<T>(this).For(collectionName);

	/// <inheritdoc />
	public IUnboundClient<object> Unbound() => new UnboundClient<object>(this);

	/// <inheritdoc />
	public IUnboundClient<T> Unbound<T>()
		where T : class => new UnboundClient<T>(this);

	// ---- metadata ----------------------------------------------------------------------------------------------

	/// <inheritdoc />
	public async Task<IEdmModel> GetMetadataAsync(CancellationToken cancellationToken = default)
	{
		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return Session.Model;
	}

	/// <inheritdoc />
	public async Task<string> GetMetadataDocumentAsync(CancellationToken cancellationToken = default)
	{
		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return Session.MetadataDocument;
	}

	/// <inheritdoc />
	public Task<string> GetCommandTextAsync<T>(string collection, Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(expression);
		return new BoundClient<IDictionary<string, object?>>(this).For(collection).Filter(ODataExpression.FromLinqExpression(expression.Body)).GetCommandTextAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<string> GetCommandTextAsync(string collection, ODataExpression expression, CancellationToken cancellationToken = default) =>
		new BoundClient<IDictionary<string, object?>>(this).For(collection).Filter(expression).GetCommandTextAsync(cancellationToken);

	/// <summary>Disposes the batch writer and, when this client created it, the session and its HTTP connection.</summary>
	public void Dispose()
	{
		if (BatchWriter is { IsValueCreated: true } writer)
		{
			writer.Value.Dispose();
		}

		if (_ownsSession)
		{
			Session.Dispose();
		}
	}

	private void SetBatchEntries(ConcurrentDictionary<object, IDictionary<string, object?>>? batchEntries)
	{
		if (batchEntries is null)
		{
			return;
		}

		BatchEntries = batchEntries;
		BatchWriter = new Lazy<BatchWriter>(() => new BatchWriter(Session, batchEntries));
	}

	private FluentCommand CreateCommand() => new(null, BatchEntries);

	private static ODataClientSettings SettingsFor(HttpClient httpClient, ODataClientSettings? settings)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		if (settings is null)
		{
			return new ODataClientSettings(httpClient);
		}

		settings.AttachHttpClient(httpClient);
		return settings;
	}
}
