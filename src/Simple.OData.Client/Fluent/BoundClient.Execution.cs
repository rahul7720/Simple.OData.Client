using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

public sealed partial class BoundClient<T>
{
	/// <inheritdoc />
	public async Task<IReadOnlyList<T>> FindEntriesAsync(CancellationToken cancellationToken = default) =>
		ConvertResults(await Client.FindEntriesAsync(Command, null, includeCount: false, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public async Task<IReadOnlyList<T>> FindEntriesAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(annotations);
		return ConvertResults(await Client.FindEntriesAsync(Command, annotations, includeCount: true, cancellationToken).ConfigureAwait(false));
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<T>> FindEntriesAsync(Uri annotatedUri, ODataFeedAnnotations annotations, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(annotatedUri);
		ArgumentNullException.ThrowIfNull(annotations);
		return ConvertResults(await Client.FindEntriesAsync(Command, annotatedUri, annotations, cancellationToken).ConfigureAwait(false));
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<T> FindEntriesAsAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var annotations = new ODataFeedAnnotations();
		var page = await Client.FindEntriesAsync(Command, annotations, includeCount: false, cancellationToken).ConfigureAwait(false);
		while (true)
		{
			foreach (var entry in ConvertResults(page))
			{
				yield return entry;
			}

			if (annotations.NextPageLink is not { } next)
			{
				yield break;
			}

			page = await Client.FindEntriesAsync(Command, next, annotations, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<T?> FindEntryAsync(CancellationToken cancellationToken = default) =>
		ConvertResult(await Client.FindEntryAsync(Command, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public async Task<TResult?> FindScalarAsync<TResult>(CancellationToken cancellationToken = default)
	{
		var result = await Client.FindScalarAsync(Command, cancellationToken).ConfigureAwait(false);
		return Client.IsBatchRequest ? default : TypeCache.Convert<TResult>(result);
	}

	/// <inheritdoc />
	public async Task<T?> InsertEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default) =>
		ConvertResult(await Client.InsertEntryAsync(Command, resultRequired, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public async Task<T?> UpdateEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default)
	{
		var result = await Client.UpdateEntryAsync(Command, resultRequired, cancellationToken).ConfigureAwait(false);
		return resultRequired ? ConvertResult(result) : null;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<T>> UpdateEntriesAsync(bool resultRequired = true, CancellationToken cancellationToken = default) =>
		ConvertResults(await Client.UpdateEntriesAsync(Command, resultRequired, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public Task DeleteEntryAsync(CancellationToken cancellationToken = default) => Client.DeleteEntryAsync(Command, cancellationToken);

	/// <inheritdoc />
	public Task<int> DeleteEntriesAsync(CancellationToken cancellationToken = default) => Client.DeleteEntriesAsync(Command, cancellationToken);

	/// <inheritdoc />
	public Task LinkEntryAsync<TResult>(TResult linkedEntryKey, CancellationToken cancellationToken = default) =>
		LinkEntryAsync(linkedEntryKey, typeof(TResult).Name, cancellationToken);

	/// <inheritdoc />
	public Task LinkEntryAsync<TResult>(TResult linkedEntryKey, string linkName, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(linkedEntryKey);
		return Client.LinkEntryAsync(Command, linkName, ToKey(linkedEntryKey), cancellationToken);
	}

	/// <inheritdoc />
	public Task LinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, TResult linkedEntryKey, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(linkedEntryKey);
		return Client.LinkEntryAsync(Command, expression.ExtractColumnName(TypeCache), ToKey(linkedEntryKey), cancellationToken);
	}

	/// <inheritdoc />
	public Task LinkEntryAsync(string linkName, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.LinkEntryAsync(Command, linkName, linkedEntryKey, cancellationToken);

	/// <inheritdoc />
	public Task LinkEntryAsync(ODataExpression expression, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.LinkEntryAsync(Command, ReferenceOf(expression), linkedEntryKey, cancellationToken);

	/// <inheritdoc />
	public Task LinkEntryAsync(ODataExpression expression, ODataEntry linkedEntryKey, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(linkedEntryKey);
		return Client.LinkEntryAsync(Command, ReferenceOf(expression), linkedEntryKey.AsDictionary(), cancellationToken);
	}

	/// <inheritdoc />
	public Task UnlinkEntryAsync<TResult>(CancellationToken cancellationToken = default) => Client.UnlinkEntryAsync(Command, typeof(TResult).Name, null, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync(string linkName, CancellationToken cancellationToken = default) => Client.UnlinkEntryAsync(Command, linkName, null, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, expression.ExtractColumnName(TypeCache), null, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync(ODataExpression expression, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, ReferenceOf(expression), null, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync<TResult>(TResult linkedEntryKey, CancellationToken cancellationToken = default) =>
		linkedEntryKey switch
		{
			string name => UnlinkEntryAsync(name, cancellationToken),
			ODataExpression { Reference: not null } expression => UnlinkEntryAsync(expression, cancellationToken),
			_ => UnlinkEntryAsync(linkedEntryKey, typeof(TResult).Name, cancellationToken),
		};

	/// <inheritdoc />
	public Task UnlinkEntryAsync<TResult>(TResult linkedEntryKey, string linkName, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, linkName, linkedEntryKey is null ? null : ToKey(linkedEntryKey), cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync<TResult>(Expression<Func<T, TResult>> expression, TResult linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, expression.ExtractColumnName(TypeCache), linkedEntryKey is null ? null : ToKey(linkedEntryKey), cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync(string linkName, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, linkName, linkedEntryKey, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync(ODataExpression expression, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, ReferenceOf(expression), linkedEntryKey, cancellationToken);

	/// <inheritdoc />
	public Task UnlinkEntryAsync(ODataExpression expression, ODataEntry linkedEntryKey, CancellationToken cancellationToken = default) =>
		Client.UnlinkEntryAsync(Command, ReferenceOf(expression), linkedEntryKey?.AsDictionary(), cancellationToken);

	private IDictionary<string, object?> ToKey(object value) => value switch
	{
		IDictionary<string, object?> dictionary => dictionary,
		ODataEntry entry => entry.AsDictionary(),
		_ => TypeCache.ToDictionary(value),
	};
}
