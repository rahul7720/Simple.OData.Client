namespace Simple.OData.Client;

/// <summary>A request paired with its response, ready to be read.</summary>
public interface IClientWithResponse<T> : IDisposable
	where T : class
{
	/// <summary>The HTTP response.</summary>
	HttpResponseMessage ResponseMessage { get; }

	/// <summary>Reads the response as a collection, optionally capturing feed annotations.</summary>
	Task<IReadOnlyList<T>> ReadAsCollectionAsync(ODataFeedAnnotations? annotations = null, CancellationToken cancellationToken = default);

	/// <summary>Reads the response as a single entry.</summary>
	Task<T?> ReadAsSingleAsync(CancellationToken cancellationToken = default);

	/// <summary>Reads the response as a primitive value.</summary>
	Task<TResult?> ReadAsScalarAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Returns the raw response body.</summary>
	Task<Stream> GetResponseStreamAsync(CancellationToken cancellationToken = default);
}
