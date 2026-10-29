using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

/// <summary>A feed read from a response: its entries, annotations and, for delta payloads, deletions and link changes.</summary>
public sealed class AnnotatedFeed
{
	/// <summary>Creates a feed from entries and optional annotations.</summary>
	public AnnotatedFeed(IEnumerable<AnnotatedEntry>? entries = null, ODataFeedAnnotations? annotations = null)
	{
		Entries = entries is null ? [] : [.. entries];
		Annotations = annotations;
	}

	/// <summary>The entries of the feed.</summary>
	public IList<AnnotatedEntry> Entries { get; }

	/// <summary>Feed-level annotations (count, next link, delta link).</summary>
	public ODataFeedAnnotations? Annotations { get; private set; }

	/// <summary>Deleted entries of a delta feed.</summary>
	public IList<ODataDeletedEntry> DeletedEntries { get; } = [];

	/// <summary>Added links of a delta feed.</summary>
	public IList<ODataDeltaLink> AddedLinks { get; } = [];

	/// <summary>Deleted links of a delta feed.</summary>
	public IList<ODataDeltaLink> DeletedLinks { get; } = [];

	/// <summary>Sets or merges the feed annotations.</summary>
	public void SetAnnotations(ODataFeedAnnotations? annotations)
	{
		if (annotations is null)
		{
			return;
		}

		if (Annotations is null)
		{
			Annotations = annotations;
		}
		else
		{
			Annotations.Merge(annotations);
		}
	}
}

/// <summary>An entry read from a response together with its annotations.</summary>
public sealed class AnnotatedEntry(IDictionary<string, object?> data, ODataEntryAnnotations? annotations = null)
{
	/// <summary>The entry values by property name; expanded navigation properties are nested dictionaries or lists of dictionaries.</summary>
	public IDictionary<string, object?> Data { get; } = data ?? throw new ArgumentNullException(nameof(data));

	/// <summary>Entry-level annotations (id, type, links, etag).</summary>
	public ODataEntryAnnotations? Annotations { get; private set; } = annotations;

	/// <summary>Annotations of the last expanded collection navigation property (its count or next link).</summary>
	public ODataFeedAnnotations? LinkAnnotations { get; private set; }

	/// <summary>Sets or merges the entry annotations.</summary>
	public void SetAnnotations(ODataEntryAnnotations? annotations)
	{
		if (annotations is null)
		{
			return;
		}

		if (Annotations is null)
		{
			Annotations = annotations;
		}
		else
		{
			Annotations.Merge(annotations);
		}
	}

	/// <summary>Sets or merges the annotations of an expanded collection.</summary>
	public void SetLinkAnnotations(ODataFeedAnnotations? annotations)
	{
		if (annotations is null)
		{
			return;
		}

		if (LinkAnnotations is null)
		{
			LinkAnnotations = annotations;
		}
		else
		{
			LinkAnnotations.Merge(annotations);
		}
	}

	/// <summary>The entry values, with the annotations added under <c>__annotations</c> when requested.</summary>
	public IDictionary<string, object?> GetData(bool includeAnnotations)
	{
		if (!includeAnnotations || Annotations is null)
		{
			return Data;
		}

		return new Dictionary<string, object?>(Data, StringComparer.Ordinal) { [ReservedNames.Annotations] = Annotations };
	}
}

/// <summary>A response read from the service: status, headers and the payload as a feed, batch, value or error.</summary>
public sealed class ODataResponse
{
	private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly ITypeCache _typeCache;

	private ODataResponse(ITypeCache typeCache, int statusCode, IReadOnlyDictionary<string, string>? headers)
	{
		_typeCache = typeCache;
		StatusCode = statusCode;
		Headers = headers ?? NoHeaders;
	}

	/// <summary>The HTTP status code.</summary>
	public int StatusCode { get; }

	/// <summary>The response headers (first value of each).</summary>
	public IReadOnlyDictionary<string, string> Headers { get; }

	/// <summary>The <c>Location</c> header.</summary>
	public string? Location => Headers.GetValueOrDefault(HttpLiteral.Location);

	/// <summary>The <c>OData-EntityId</c> header.</summary>
	public string? ODataEntityId => Headers.GetValueOrDefault(HttpLiteral.ODataEntityId);

	/// <summary>The <c>ETag</c> header.</summary>
	public string? ETag => Headers.GetValueOrDefault(HttpLiteral.ETag);

	/// <summary>The payload as a feed (a single entry or value is a feed with one entry).</summary>
	public AnnotatedFeed? Feed { get; private init; }

	/// <summary>The individual responses of a batch.</summary>
	public IReadOnlyList<ODataResponse>? Batch { get; private init; }

	/// <summary>The exception representing a failed request.</summary>
	public Exception? Exception { get; private init; }

	/// <summary>The OData error payload, when the service returned one.</summary>
	public ODataErrorDetails? Error { get; private init; }

	/// <summary>The entries of the payload, unwrapping single-value results.</summary>
	public IReadOnlyList<IDictionary<string, object?>> AsEntries(bool includeAnnotations)
	{
		if (Feed is null || Feed.Entries.Count == 0)
		{
			return [];
		}

		var unwrap = Feed.Entries[0].Data.ContainsKey(ReservedNames.Result);
		var result = new List<IDictionary<string, object?>>(Feed.Entries.Count);
		foreach (var entry in Feed.Entries)
		{
			result.Add(unwrap ? ExtractResult(entry, includeAnnotations) : entry.GetData(includeAnnotations));
		}

		return result;
	}

	/// <summary>The first entry of the payload, or <see langword="null"/>.</summary>
	public IDictionary<string, object?>? AsEntry(bool includeAnnotations)
	{
		var entries = AsEntries(includeAnnotations);
		return entries.Count > 0 ? entries[0] : null;
	}

	/// <summary>The payload as a single value converted to <typeparamref name="T"/>.</summary>
	public T? AsScalar<T>()
	{
		var entry = AsEntry(false);
		if (entry is null || entry.Count == 0)
		{
			return default;
		}

		var value = entry.Values.First();
		return value is null ? default : _typeCache.Convert<T>(value);
	}

	/// <summary>The payload as an array of values converted to <typeparamref name="T"/>.</summary>
	public T[] AsArray<T>() => AsEntries(false).SelectMany(x => x.Values).Select(x => _typeCache.Convert<T>(x)!).ToArray();

	internal static ODataResponse FromFeed(ITypeCache typeCache, AnnotatedFeed feed, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		new(typeCache, statusCode, headers) { Feed = feed };

	internal static ODataResponse FromEntry(ITypeCache typeCache, AnnotatedEntry entry, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		new(typeCache, statusCode, headers) { Feed = new AnnotatedFeed([entry]) };

	internal static ODataResponse FromValue(ITypeCache typeCache, object? value, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		FromProperty(typeCache, ReservedNames.Result, value, statusCode, headers);

	internal static ODataResponse FromProperty(ITypeCache typeCache, string propertyName, object? value, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		new(typeCache, statusCode, headers)
		{
			Feed = new AnnotatedFeed([new AnnotatedEntry(new Dictionary<string, object?>(StringComparer.Ordinal) { [propertyName] = value })]),
		};

	internal static ODataResponse FromCollection(ITypeCache typeCache, IEnumerable<object?> items, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		new(typeCache, statusCode, headers)
		{
			Feed = new AnnotatedFeed(items.Select(x => new AnnotatedEntry(new Dictionary<string, object?>(StringComparer.Ordinal) { [ReservedNames.Result] = x }))),
		};

	internal static ODataResponse FromBatch(ITypeCache typeCache, IReadOnlyList<ODataResponse> batch, int statusCode, IReadOnlyDictionary<string, string>? headers) =>
		new(typeCache, statusCode, headers) { Batch = batch };

	internal static ODataResponse FromStatusCode(ITypeCache typeCache, int statusCode, IReadOnlyDictionary<string, string>? headers, Exception? exception = null) =>
		new(typeCache, statusCode, headers) { Exception = exception };

	internal static ODataResponse FromError(ITypeCache typeCache, int statusCode, IReadOnlyDictionary<string, string>? headers, ODataErrorDetails? error, Exception? exception) =>
		new(typeCache, statusCode, headers) { Error = error, Exception = exception };

	internal static ODataResponse Empty(ITypeCache typeCache) => new(typeCache, 200, null) { Feed = new AnnotatedFeed() };

	private static IDictionary<string, object?> ExtractResult(AnnotatedEntry entry, bool includeAnnotations)
	{
		var data = entry.Data;
		if (data.Count == 1 && data.TryGetValue(ReservedNames.Result, out var result) && result is IDictionary<string, object?> nested)
		{
			return nested;
		}

		return entry.GetData(includeAnnotations);
	}
}
