using System.Collections.Concurrent;
using Microsoft.OData.Edm;

namespace Simple.OData.Client.Metadata;

/// <summary>A parsed metadata document cached per service root.</summary>
internal sealed record CachedEdmModel(string Key, string Document, IEdmModel Model);

/// <summary>
/// Process-wide cache of parsed metadata documents keyed by service root URL, so that every client created for the
/// same service shares one model and the metadata document is downloaded once.
/// </summary>
internal static class EdmModelCache
{
	private static readonly ConcurrentDictionary<string, CachedEdmModel> Models = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

	public static CachedEdmModel GetOrAdd(string key, string document, Func<string, IEdmModel> parse)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(document);
		ArgumentNullException.ThrowIfNull(parse);
		return Models.GetOrAdd(key, static (k, state) => new CachedEdmModel(k, state.document, state.parse(state.document)), (document, parse));
	}

	public static async ValueTask<CachedEdmModel> GetOrAddAsync(string key, Func<CancellationToken, Task<string>> load, Func<string, IEdmModel> parse, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(load);
		ArgumentNullException.ThrowIfNull(parse);

		if (Models.TryGetValue(key, out var cached))
		{
			return cached;
		}

		var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Models.TryGetValue(key, out cached))
			{
				return cached;
			}

			var document = await load(cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			cached = new CachedEdmModel(key, document, parse(document));
			Models[key] = cached;
			return cached;
		}
		finally
		{
			gate.Release();
		}
	}

	public static bool TryGet(string key, out CachedEdmModel? cached) => Models.TryGetValue(key, out cached);

	public static bool Remove(string key)
	{
		Gates.TryRemove(key, out _);
		return Models.TryRemove(key, out _);
	}

	public static void Clear()
	{
		Models.Clear();
		Gates.Clear();
	}
}
