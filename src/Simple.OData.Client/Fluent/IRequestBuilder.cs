namespace Simple.OData.Client;

/// <summary>Builds the request of a fluent command without sending it, for custom transports and inspection.</summary>
public interface IRequestBuilder<T>
	where T : class
{
	/// <summary>Builds the request that retrieves the entries of the query.</summary>
	Task<IClientWithRequest<T>> FindEntriesAsync(CancellationToken cancellationToken = default);

	/// <summary>Builds the request that retrieves the entries of the query with <c>$count=true</c>.</summary>
	Task<IClientWithRequest<T>> FindEntriesAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default);

	/// <summary>Builds the request that retrieves a single entry.</summary>
	Task<IClientWithRequest<T>> FindEntryAsync(CancellationToken cancellationToken = default);

	/// <summary>Builds the request that retrieves a primitive value.</summary>
	Task<IClientWithRequest<T>> FindScalarAsync(CancellationToken cancellationToken = default);

	/// <summary>Builds the insert request.</summary>
	Task<IClientWithRequest<T>> InsertEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Builds the update request.</summary>
	Task<IClientWithRequest<T>> UpdateEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Builds the delete request.</summary>
	Task<IClientWithRequest<T>> DeleteEntryAsync(CancellationToken cancellationToken = default);
}
