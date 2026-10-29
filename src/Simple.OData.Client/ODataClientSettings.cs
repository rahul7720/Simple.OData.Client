using System.Net;
using Microsoft.OData.Edm;

namespace Simple.OData.Client;

/// <summary>
/// Configuration of an <c>ODataClient</c>. One settings instance describes one service and is shared by
/// every client, batch and session created from it.
/// </summary>
public sealed class ODataClientSettings
{
	private Uri? _baseOrRelativeUri;
	private INameMatchResolver _nameMatchResolver = ODataNameMatchResolver.NotStrict;
	private ITypeCache? _typeCache;

	/// <summary>Initializes empty settings; set <see cref="BaseUri"/> before use.</summary>
	public ODataClientSettings()
	{
	}

	/// <summary>Initializes settings for the service at <paramref name="baseUri"/>.</summary>
	public ODataClientSettings(Uri baseUri, ICredentials? credentials = null)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		BaseUri = baseUri;
		Credentials = credentials;
	}

	/// <summary>
	/// Initializes settings that send requests through an existing <see cref="System.Net.Http.HttpClient"/>.
	/// When the client has a <see cref="System.Net.Http.HttpClient.BaseAddress"/>, <paramref name="relativeUri"/> may point at the service root below it.
	/// </summary>
	public ODataClientSettings(HttpClient httpClient, Uri? relativeUri = null, ICredentials? credentials = null)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		if (httpClient.BaseAddress is not null && !httpClient.BaseAddress.IsAbsoluteUri)
		{
			throw new ArgumentException("HttpClient BaseAddress must be an absolute URI", nameof(httpClient));
		}

		if (relativeUri is not null && relativeUri.IsAbsoluteUri)
		{
			throw new ArgumentException("Must be a relative URI", nameof(relativeUri));
		}

		if (httpClient.BaseAddress is null && relativeUri is not null)
		{
			throw new ArgumentException("Must not specify relative URI when HttpClient has no BaseAddress", nameof(relativeUri));
		}

		HttpClient = httpClient;
		_baseOrRelativeUri = relativeUri;
		Credentials = credentials;
	}

	// ---- endpoint and transport --------------------------------------------------------------------------------

	/// <summary>The service root. Composed from <see cref="HttpClient"/>.<c>BaseAddress</c> and the relative URI when an external client is used.</summary>
	public Uri? BaseUri
	{
		get
		{
			if (HttpClient?.BaseAddress is { } baseAddress)
			{
				return _baseOrRelativeUri is null ? baseAddress : new Uri(baseAddress, _baseOrRelativeUri);
			}

			return _baseOrRelativeUri;
		}
		set
		{
			if (value is { IsAbsoluteUri: true } && HttpClient?.BaseAddress is not null)
			{
				throw new InvalidOperationException("Unable to set BaseUri when BaseAddress is specified on HttpClient.");
			}

			_baseOrRelativeUri = value;
		}
	}

	/// <summary>An external <see cref="System.Net.Http.HttpClient"/> to send requests with; it is not disposed by the client.</summary>
	public HttpClient? HttpClient { get; }

	/// <summary>Credentials for the default message handler.</summary>
	public ICredentials? Credentials { get; set; }

	/// <summary>Timeout applied to the default <see cref="System.Net.Http.HttpClient"/>; zero keeps the framework default.</summary>
	public TimeSpan RequestTimeout { get; set; }

	/// <summary>Creates the <see cref="HttpMessageHandler"/> used by the default <see cref="System.Net.Http.HttpClient"/>. Overrides <see cref="ConfigureHandler"/>.</summary>
	public Func<HttpMessageHandler>? OnCreateMessageHandler { get; set; }

	/// <summary>Customizes the default <see cref="SocketsHttpHandler"/> (proxy, certificates, decompression, …).</summary>
	public Action<SocketsHttpHandler>? ConfigureHandler { get; set; }

	/// <summary>Replaces HTTP transport entirely: receives every request and returns the response. Intended for tests.</summary>
	public Func<HttpRequestMessage, Task<HttpResponseMessage>>? RequestExecutor { get; set; }

	/// <summary>Runs before every request is sent.</summary>
	public Action<HttpRequestMessage>? BeforeRequest { get; set; }

	/// <summary>Runs before every request is sent, after <see cref="BeforeRequest"/>.</summary>
	public Func<HttpRequestMessage, CancellationToken, Task>? BeforeRequestAsync { get; set; }

	/// <summary>Runs after every response is received, before status handling.</summary>
	public Action<HttpResponseMessage>? AfterResponse { get; set; }

	/// <summary>Runs after every response is received, after <see cref="AfterResponse"/>.</summary>
	public Func<HttpResponseMessage, CancellationToken, Task>? AfterResponseAsync { get; set; }

	/// <summary>Maximum number of simultaneous requests; zero means unlimited.</summary>
	public int MaxConcurrentRequests { get; set; }

	// ---- metadata ---------------------------------------------------------------------------------------------

	/// <summary>A metadata document (CSDL XML or JSON) to use instead of downloading <c>$metadata</c>.</summary>
	public string? MetadataDocument { get; set; }

	/// <summary>A pre-built model to use instead of downloading <c>$metadata</c>.</summary>
	public IEdmModel? MetadataModel { get; set; }

	/// <summary>The format requested for <c>$metadata</c>.</summary>
	public MetadataFormat MetadataFormat { get; set; }

	/// <summary>The <c>$schemaversion</c> to request (OData 4.01).</summary>
	public string? SchemaVersion { get; set; }

	/// <summary>Resolves loosely spelled entity set, type and property names against the metadata.</summary>
	public INameMatchResolver NameMatchResolver
	{
		get => _nameMatchResolver;
		set => _nameMatchResolver = value ?? throw new ArgumentNullException(nameof(value));
	}

	/// <summary>CLR type information and custom converters. Created on first access with the <see cref="NameMatchResolver"/>.</summary>
	public ITypeCache TypeCache
	{
		get => _typeCache ??= new TypeCache(new TypeConverter(), NameMatchResolver);
		set => _typeCache = value ?? throw new ArgumentNullException(nameof(value));
	}

	// ---- protocol ---------------------------------------------------------------------------------------------

	/// <summary>The protocol version written on the wire.</summary>
	public ODataProtocolVersion ProtocolVersion { get; set; }

	/// <summary>The <c>odata.metadata</c> level requested in responses.</summary>
	public ODataMetadataLevel MetadataLevel { get; set; }

	/// <summary>Address single entities as <c>Products/1</c> instead of <c>Products(1)</c>.</summary>
	public bool KeyAsSegment { get; set; }

	/// <summary>Omit namespaces when invoking bound functions and actions.</summary>
	public bool UnqualifiedNameCall { get; set; }

	/// <summary>Write enum literals without their type prefix in URLs.</summary>
	public bool EnumPrefixFree { get; set; }

	/// <summary>Write <c>$ref</c> targets as absolute URIs.</summary>
	public bool UseAbsoluteReferenceUris { get; set; }

	/// <summary>The <c>$batch</c> wire format.</summary>
	public BatchFormat BatchFormat { get; set; }

	/// <summary>The form of request URIs inside multipart batches.</summary>
	public BatchPayloadUriOption BatchPayloadUriOption { get; set; }

	/// <summary>The method preferred for updates when both would work.</summary>
	public ODataUpdateMethod PreferredUpdateMethod { get; set; }

	/// <summary>How filter-based bulk updates and deletes are executed.</summary>
	public BulkOperationMode BulkOperationMode { get; set; }

	/// <summary>Do not send navigation property changes when updating entries.</summary>
	public bool IgnoreNavigationPropertiesOnUpdate { get; set; }

	/// <summary>Ignore properties that do not exist in the metadata instead of throwing <see cref="UnresolvableObjectException"/>.</summary>
	public bool IgnoreUnmappedProperties { get; set; }

	/// <summary>Treat <c>404 Not Found</c> as an empty result instead of throwing.</summary>
	public bool IgnoreResourceNotFoundException { get; set; }

	/// <summary>Add an <c>__annotations</c> entry (an <see cref="ODataEntryAnnotations"/>) to every result dictionary.</summary>
	public bool IncludeAnnotationsInResults { get; set; }

	/// <summary>The <c>odata.include-annotations</c> preference, e.g. <c>"*"</c> or <c>"display.*"</c>.</summary>
	public string? IncludeAnnotations { get; set; }

	/// <summary>Read untyped (open) property values as strings instead of inferring JSON types.</summary>
	public bool ReadUntypedAsString { get; set; } = true;

	/// <summary>The <c>odata.maxpagesize</c> preference.</summary>
	public int? MaxPageSize { get; set; }

	/// <summary>Send the <c>odata.track-changes</c> preference to obtain delta links.</summary>
	public bool TrackChanges { get; set; }

	/// <summary>Send the <c>omit-values=nulls</c> preference.</summary>
	public bool OmitNullValues { get; set; }

	/// <summary>Send the <c>odata.continue-on-error</c> preference with batches.</summary>
	public bool ContinueOnError { get; set; }

	/// <summary>Send the <c>respond-async</c> preference and follow <c>202 Accepted</c> monitors to completion.</summary>
	public bool RespondAsync { get; set; }

	/// <summary>Polling interval for asynchronous request monitors when the service sends no <c>Retry-After</c>.</summary>
	public TimeSpan AsyncPollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>Request <c>IEEE754Compatible=true</c> so 64-bit integers and decimals are exchanged as strings.</summary>
	public bool Ieee754Compatible { get; set; }

	/// <summary>Payload validations performed when reading and writing.</summary>
	public ValidationKinds Validations { get; set; } = ValidationKinds.All;

	/// <summary>What goes into the message of a <c>WebRequestException</c>.</summary>
	public WebRequestExceptionMessageSource WebRequestExceptionMessageSource { get; set; }

	// ---- diagnostics ------------------------------------------------------------------------------------------

	/// <summary>Receives trace messages as a format string and arguments.</summary>
	public Action<string, object?[]>? OnTrace { get; set; }

	/// <summary>Selects what <see cref="OnTrace"/> receives.</summary>
	public ODataTrace TraceFilter { get; set; }

	/// <summary>The clock used for polling delays; replace in tests.</summary>
	public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
