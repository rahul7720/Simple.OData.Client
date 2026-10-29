using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Simple.OData.Client;

/// <summary>A fluent client bound to an entity set, singleton or navigation path, producing results of type <typeparamref name="T"/>.</summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public interface IBoundClient<T> : IFluentClient<T, IBoundClient<T>>
	where T : class
{
	// ---- collection and payload --------------------------------------------------------------------------------

	/// <summary>Targets <paramref name="collectionName"/>, or the collection mapped to <typeparamref name="T"/> when omitted.</summary>
	IBoundClient<T> For(string? collectionName = null);

	/// <summary>Targets the collection named by a dynamic expression.</summary>
	IBoundClient<ODataEntry> For(ODataExpression expression);

	/// <summary>Restricts the collection to a derived type (<c>/Namespace.Type</c>).</summary>
	IBoundClient<IDictionary<string, object?>> As(string derivedCollectionName);

	/// <summary>Restricts the collection to the derived type <typeparamref name="TResult"/>.</summary>
	IBoundClient<TResult> As<TResult>(string? derivedCollectionName = null) where TResult : class;

	/// <summary>Restricts the collection to a derived type named by a dynamic expression.</summary>
	IBoundClient<ODataEntry> As(ODataExpression expression);

	/// <summary>Sets the payload (entry data or operation parameters) from the properties of <paramref name="value"/>.</summary>
	IBoundClient<T> Set(object value);

	/// <summary>Sets the payload from named values.</summary>
	IBoundClient<T> Set(IDictionary<string, object?> value);

	/// <summary>Sets the payload from a typed entry.</summary>
	IBoundClient<T> Set(T entry);

	/// <summary>Sets the payload from dynamic <c>x.Property = value</c> expressions.</summary>
	IBoundClient<T> Set(params ODataExpression[] value);

	/// <summary>Whether the filter addresses exactly one entry by key. Requires initialized metadata.</summary>
	bool FilterIsKey { get; }

	/// <summary>The key values recognised in the filter, or <see langword="null"/>. Requires initialized metadata.</summary>
	IReadOnlyDictionary<string, object?>? FilterAsKey { get; }

	/// <summary>Builds requests without sending them, for custom transports.</summary>
	IRequestBuilder<T> BuildRequestFor();

	// ---- execution ---------------------------------------------------------------------------------------------

	/// <summary>Retrieves the entries of the query.</summary>
	Task<IReadOnlyList<T>> FindEntriesAsync(CancellationToken cancellationToken = default);

	/// <summary>Retrieves the entries of the query with <c>$count=true</c>, capturing count, next link and delta link.</summary>
	Task<IReadOnlyList<T>> FindEntriesAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default);

	/// <summary>Retrieves a page addressed by a next link, capturing the annotations of that page.</summary>
	Task<IReadOnlyList<T>> FindEntriesAsync(Uri annotatedUri, ODataFeedAnnotations annotations, CancellationToken cancellationToken = default);

	/// <summary>Streams the entries of the query, following <c>@odata.nextLink</c> until the last page.</summary>
	IAsyncEnumerable<T> FindEntriesAsAsyncEnumerable(CancellationToken cancellationToken = default);

	/// <summary>Retrieves the single entry of the query, or <see langword="null"/>.</summary>
	Task<T?> FindEntryAsync(CancellationToken cancellationToken = default);

	/// <summary>Retrieves a primitive value (<c>$count</c>, <c>$value</c>, a property).</summary>
	Task<TResult?> FindScalarAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Inserts the payload as a new entry; returns the created entry when the service returns one.</summary>
	Task<T?> InsertEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Updates the entry addressed by key or key-like filter; falls back to <see cref="UpdateEntriesAsync"/> for other filters.</summary>
	Task<T?> UpdateEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Updates every entry matching the filter, client-side or via <c>$each</c> depending on <see cref="ODataClientSettings.BulkOperationMode"/>.</summary>
	Task<IReadOnlyList<T>> UpdateEntriesAsync(bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Deletes the entry addressed by key or key-like filter; falls back to <see cref="DeleteEntriesAsync"/> for other filters.</summary>
	Task DeleteEntryAsync(CancellationToken cancellationToken = default);

	/// <summary>Deletes every entry matching the filter. Returns the number of deleted entries, or -1 when a <c>$each</c> request did not report it.</summary>
	Task<int> DeleteEntriesAsync(CancellationToken cancellationToken = default);

	/// <summary>Creates a reference (<c>$ref</c>) to <paramref name="linkedEntryKey"/> through the navigation named <paramref name="linkName"/> or by the type of <typeparamref name="TResult"/>.</summary>
	Task LinkEntryAsync<TResult>(TResult linkedEntryKey, string? linkName = null, CancellationToken cancellationToken = default);

	/// <summary>Creates a reference (<c>$ref</c>) through the navigation selected by <paramref name="expression"/>.</summary>
	Task LinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, TResult linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Creates a reference (<c>$ref</c>) through the navigation named by a dynamic expression.</summary>
	Task LinkEntryAsync(ODataExpression expression, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Creates a reference (<c>$ref</c>) through the navigation named by a dynamic expression.</summary>
	Task LinkEntryAsync(ODataExpression expression, ODataEntry linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference of the single-valued navigation named by the type of <typeparamref name="TResult"/>.</summary>
	Task UnlinkEntryAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Removes the reference of the single-valued navigation <paramref name="linkName"/>.</summary>
	Task UnlinkEntryAsync(string linkName, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference of the single-valued navigation selected by <paramref name="expression"/>.</summary>
	Task UnlinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference of the navigation named by a dynamic expression.</summary>
	Task UnlinkEntryAsync(ODataExpression expression, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference to <paramref name="linkedEntryKey"/> from the navigation named <paramref name="linkName"/> or by the type of <typeparamref name="TResult"/>.</summary>
	Task UnlinkEntryAsync<TResult>(TResult linkedEntryKey, string? linkName = null, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference to <paramref name="linkedEntryKey"/> from the navigation selected by <paramref name="expression"/>.</summary>
	Task UnlinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, TResult linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference to <paramref name="linkedEntryKey"/> from the navigation named by a dynamic expression.</summary>
	Task UnlinkEntryAsync(ODataExpression expression, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Removes the reference to <paramref name="linkedEntryKey"/> from the navigation named by a dynamic expression.</summary>
	Task UnlinkEntryAsync(ODataExpression expression, ODataEntry linkedEntryKey, CancellationToken cancellationToken = default);
}
