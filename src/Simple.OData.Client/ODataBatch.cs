using System.Collections.Concurrent;

namespace Simple.OData.Client;

/// <summary>
/// Groups several client operations into a single <c>$batch</c> request. Operations are added with <c>+=</c> and
/// sent together by <c>ExecuteAsync</c>.
/// </summary>
public sealed partial class ODataBatch
{
	private readonly List<Func<IODataClient, Task>> _actions = [];
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<object, IDictionary<string, object?>> _entryMap = new();

	/// <summary>Creates a batch against the service at <paramref name="baseUri"/>.</summary>
	public ODataBatch(Uri baseUri)
		: this(new ODataClientSettings { BaseUri = baseUri })
	{
	}

	/// <summary>Creates a batch with the given client settings.</summary>
	public ODataBatch(ODataClientSettings settings)
	{
		Settings = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	internal ODataClientSettings Settings { get; }

	internal IReadOnlyList<Func<IODataClient, Task>> Actions => _actions;

	internal IReadOnlyDictionary<string, string> Headers => _headers;

	/// <summary>Maps the objects passed to batch operations to their entry data, so later operations can reference them by Content-ID.</summary>
	internal ConcurrentDictionary<object, IDictionary<string, object?>> EntryMap => _entryMap;

	/// <summary>Adds an operation to the batch.</summary>
	public static ODataBatch operator +(ODataBatch batch, Func<IODataClient, Task> action)
	{
		ArgumentNullException.ThrowIfNull(batch);
		ArgumentNullException.ThrowIfNull(action);
		batch._actions.Add(action);
		return batch;
	}

	/// <summary>Adds an operation to the batch (named form of <c>+</c>).</summary>
	public static ODataBatch Add(ODataBatch batch, Func<IODataClient, Task> action) => batch + action;

	/// <summary>Adds a header to the batch request.</summary>
	public ODataBatch WithHeader(string name, string value)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		_headers[name] = value;
		return this;
	}

	/// <summary>Adds headers to the batch request.</summary>
	public ODataBatch WithHeaders(IDictionary<string, string> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		foreach (var (name, value) in headers)
		{
			_headers[name] = value;
		}

		return this;
	}
}
