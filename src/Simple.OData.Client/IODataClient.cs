using System.Linq.Expressions;
using Microsoft.OData.Edm;

namespace Simple.OData.Client;

/// <summary>The OData client surface: fluent query entry points and raw, command-text based operations.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public partial interface IODataClient
{
	/// <summary>Starts a fluent query against <paramref name="collectionName"/> with dictionary results.</summary>
	IBoundClient<IDictionary<string, object?>> For(string collectionName);

	/// <summary>Starts a fluent query against the collection named by a dynamic <paramref name="expression"/>.</summary>
	IBoundClient<ODataEntry> For(ODataExpression expression);

	/// <summary>Starts a fluent query against the collection mapped to <typeparamref name="T"/>, or <paramref name="collectionName"/> when given.</summary>
	IBoundClient<T> For<T>(string? collectionName = null) where T : class;

	/// <summary>Starts a fluent request for an unbound function, action or singleton.</summary>
	IUnboundClient<object> Unbound();

	/// <summary>Starts a fluent request for an unbound function, action or singleton with typed results.</summary>
	IUnboundClient<T> Unbound<T>() where T : class;

	/// <summary>Returns the service model, downloading the metadata document when needed.</summary>
	Task<IEdmModel> GetMetadataAsync(CancellationToken cancellationToken = default);

	/// <summary>Returns the raw metadata document (CSDL XML or JSON).</summary>
	Task<string> GetMetadataDocumentAsync(CancellationToken cancellationToken = default);

	/// <summary>Formats the request URL for a typed filter against <paramref name="collection"/>.</summary>
	Task<string> GetCommandTextAsync<T>(string collection, Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);

	/// <summary>Formats the request URL for a dynamic filter against <paramref name="collection"/>.</summary>
	Task<string> GetCommandTextAsync(string collection, ODataExpression expression, CancellationToken cancellationToken = default);

	/// <summary>Retrieves the entries addressed by <paramref name="commandText"/>, optionally capturing feed annotations.</summary>
	Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesAsync(string commandText, ODataFeedAnnotations? annotations = null, CancellationToken cancellationToken = default);

	/// <summary>Retrieves the single entry addressed by <paramref name="commandText"/>, or <see langword="null"/>.</summary>
	Task<IDictionary<string, object?>?> FindEntryAsync(string commandText, CancellationToken cancellationToken = default);

	/// <summary>Retrieves a scalar value (<c>$count</c>, <c>$value</c>, a primitive property).</summary>
	Task<object?> FindScalarAsync(string commandText, CancellationToken cancellationToken = default);

	/// <summary>Retrieves one entry of <paramref name="collection"/> by key.</summary>
	Task<IDictionary<string, object?>?> GetEntryAsync(string collection, IDictionary<string, object?> entryKey, CancellationToken cancellationToken = default);

	/// <summary>Inserts an entry into <paramref name="collection"/>.</summary>
	Task<IDictionary<string, object?>?> InsertEntryAsync(string collection, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Updates the entry of <paramref name="collection"/> identified by <paramref name="entryKey"/>.</summary>
	Task<IDictionary<string, object?>?> UpdateEntryAsync(string collection, IDictionary<string, object?> entryKey, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Updates every entry selected by <paramref name="commandText"/> (a filter) in <paramref name="collection"/>.</summary>
	Task<IReadOnlyList<IDictionary<string, object?>>> UpdateEntriesAsync(string collection, string commandText, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default);

	/// <summary>Deletes the entry of <paramref name="collection"/> identified by <paramref name="entryKey"/>.</summary>
	Task DeleteEntryAsync(string collection, IDictionary<string, object?> entryKey, CancellationToken cancellationToken = default);

	/// <summary>Deletes every entry selected by <paramref name="commandText"/> (a filter) in <paramref name="collection"/>. Returns the number of deleted entries, or -1 when the server did not report it.</summary>
	Task<int> DeleteEntriesAsync(string collection, string commandText, CancellationToken cancellationToken = default);

	/// <summary>Creates a reference (<c>$ref</c>) from an entry to <paramref name="linkedEntryKey"/>.</summary>
	Task LinkEntryAsync(string collection, IDictionary<string, object?> entryKey, string linkName, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default);

	/// <summary>Removes a reference (<c>$ref</c>); <paramref name="linkedEntryKey"/> identifies the member of a collection navigation.</summary>
	Task UnlinkEntryAsync(string collection, IDictionary<string, object?> entryKey, string linkName, IDictionary<string, object?>? linkedEntryKey = null, CancellationToken cancellationToken = default);

	/// <summary>Reads a media resource or stream property.</summary>
	Task<Stream> GetMediaStreamAsync(string commandText, CancellationToken cancellationToken = default);

	/// <summary>Writes a media resource or stream property.</summary>
	Task SetMediaStreamAsync(string commandText, Stream stream, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default);

	/// <summary>Calls an unbound function returning a single entry or complex value.</summary>
	Task<IDictionary<string, object?>?> ExecuteFunctionAsSingleAsync(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Calls an unbound function returning a collection.</summary>
	Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteFunctionAsEnumerableAsync(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Calls an unbound function returning a primitive value.</summary>
	Task<T?> ExecuteFunctionAsScalarAsync<T>(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Calls an unbound function returning a collection of primitive values.</summary>
	Task<T[]> ExecuteFunctionAsArrayAsync<T>(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Invokes an unbound action without reading a result.</summary>
	Task ExecuteActionAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Invokes an unbound action returning a single entry or complex value.</summary>
	Task<IDictionary<string, object?>?> ExecuteActionAsSingleAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Invokes an unbound action returning a collection.</summary>
	Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteActionAsEnumerableAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Invokes an unbound action returning a primitive value.</summary>
	Task<T?> ExecuteActionAsScalarAsync<T>(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Invokes an unbound action returning a collection of primitive values.</summary>
	Task<T[]> ExecuteActionAsArrayAsync<T>(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

	/// <summary>Follows a delta link and reads the changes since it was issued.</summary>
	Task<ODataDelta> FindDeltaAsync(Uri deltaLink, CancellationToken cancellationToken = default);

	/// <summary>Sends a prepared request and parses the response.</summary>
	Task<ODataResponse> GetResponseAsync(ODataRequest request, CancellationToken cancellationToken = default);

	/// <summary>Sends a prepared request and returns the raw response body.</summary>
	Task<Stream> GetResponseStreamAsync(ODataRequest request, CancellationToken cancellationToken = default);
}
