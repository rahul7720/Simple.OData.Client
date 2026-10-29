using System.Net;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client;

/// <summary>A request paired with its response, ready to be read.</summary>
internal sealed class ClientWithResponse<T>(ODataClient client, ODataRequest request, HttpResponseMessage responseMessage, EntityCollection? entityCollection) : IClientWithResponse<T>
	where T : class
{
	private Session Session => client.Session;

	private ITypeCache TypeCache => Session.TypeCache;

	public HttpResponseMessage ResponseMessage { get; } = responseMessage ?? throw new ArgumentNullException(nameof(responseMessage));

	public void Dispose() => ResponseMessage.Dispose();

	public async Task<Stream> GetResponseStreamAsync(CancellationToken cancellationToken = default)
	{
		if (!HasBody)
		{
			return Stream.Null;
		}

		var stream = new MemoryStream();
		await ResponseMessage.Content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
		stream.Position = 0;
		return stream;
	}

	public async Task<IReadOnlyList<T>> ReadAsCollectionAsync(ODataFeedAnnotations? annotations = null, CancellationToken cancellationToken = default)
	{
		var response = await ReadAsync(cancellationToken).ConfigureAwait(false);
		if (response is null)
		{
			annotations?.CopyFrom(null);
			return [];
		}

		annotations?.CopyFrom(response.Feed?.Annotations);
		var entries = response.AsEntries(Session.Settings.IncludeAnnotationsInResults);
		var results = new List<T>(entries.Count);
		foreach (var entry in entries)
		{
			results.Add(TypeCache.ToObject<T>(entry)!);
		}

		return results;
	}

	public async Task<T?> ReadAsSingleAsync(CancellationToken cancellationToken = default)
	{
		var response = await ReadAsync(cancellationToken).ConfigureAwait(false);
		var entry = response?.AsEntry(Session.Settings.IncludeAnnotationsInResults);
		return entry is null ? null : TypeCache.ToObject<T>(entry);
	}

	public async Task<TResult?> ReadAsScalarAsync<TResult>(CancellationToken cancellationToken = default)
	{
		var response = await ReadAsync(cancellationToken).ConfigureAwait(false);
		return response is null ? default : response.AsScalar<TResult>();
	}

	private bool HasBody =>
		ResponseMessage.IsSuccessStatusCode && ResponseMessage.StatusCode != HttpStatusCode.NoContent &&
		(request.Method == RestVerbs.Get || request.ResultRequired);

	private async Task<ODataResponse?> ReadAsync(CancellationToken cancellationToken)
	{
		if (!HasBody)
		{
			return null;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return await new ResponseReader(Session).ReadAsync(ResponseMessage, entityCollection, cancellationToken).ConfigureAwait(false);
	}
}
