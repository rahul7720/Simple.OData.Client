using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client;

public sealed partial class ODataClient
{
	// ---- untyped API -------------------------------------------------------------------------------------------

	/// <inheritdoc />
	public async Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesAsync(string commandText, ODataFeedAnnotations? annotations = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (IsBatchResponse)
		{
			return BatchResponseEntries(annotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(commandText, this).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		var response = await FindResponseAsync(request, null, annotations, cancellationToken).ConfigureAwait(false);
		return response?.AsEntries(IncludeAnnotations) ?? [];
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object?>?> FindEntryAsync(string commandText, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (IsBatchResponse)
		{
			return BatchResponse!.AsEntry(IncludeAnnotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(commandText, this).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		var response = await FindResponseAsync(request, null, null, cancellationToken).ConfigureAwait(false);
		return response?.AsEntry(IncludeAnnotations);
	}

	/// <inheritdoc />
	public async Task<object?> FindScalarAsync(string commandText, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (IsBatchResponse)
		{
			return BatchResponse!.AsScalar<object>();
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(commandText, this).GetRequestAsync(scalarResult: true).ConfigureAwait(false);
		return await ExecuteRequestWithResultAsync(request, null, x => x.AsScalar<object>(), () => null, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object?>?> GetEntryAsync(string collection, IDictionary<string, object?> entryKey, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryKey);
		return FindEntryAsync(CreateCommand().For(collection).Key(entryKey), cancellationToken);
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object?>?> InsertEntryAsync(string collection, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryData);
		RemoveAnnotationProperties(entryData);
		return InsertEntryAsync(CreateCommand().For(collection).Set(entryData), resultRequired, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object?>?> UpdateEntryAsync(string collection, IDictionary<string, object?> entryKey, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryKey);
		ArgumentNullException.ThrowIfNull(entryData);
		RemoveAnnotationProperties(entryKey);
		RemoveAnnotationProperties(entryData);
		return UpdateEntryAsync(CreateCommand().For(collection).Key(entryKey).Set(entryData), resultRequired, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<IDictionary<string, object?>>> UpdateEntriesAsync(string collection, string commandText, IDictionary<string, object?> entryData, bool resultRequired = true, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(entryData);
		RemoveAnnotationProperties(entryData);
		return UpdateEntriesAsync(CreateCommand().For(collection).Filter(ExtractFilter(collection, commandText)).Set(entryData), resultRequired, cancellationToken);
	}

	/// <inheritdoc />
	public Task DeleteEntryAsync(string collection, IDictionary<string, object?> entryKey, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryKey);
		RemoveAnnotationProperties(entryKey);
		return DeleteEntryAsync(CreateCommand().For(collection).Key(entryKey), cancellationToken);
	}

	/// <inheritdoc />
	public Task<int> DeleteEntriesAsync(string collection, string commandText, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(commandText);
		return DeleteEntriesAsync(CreateCommand().For(collection).Filter(ExtractFilter(collection, commandText)), cancellationToken);
	}

	/// <inheritdoc />
	public Task LinkEntryAsync(string collection, IDictionary<string, object?> entryKey, string linkName, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryKey);
		ArgumentNullException.ThrowIfNull(linkedEntryKey);
		RemoveAnnotationProperties(entryKey);
		RemoveAnnotationProperties(linkedEntryKey);
		return LinkEntryAsync(CreateCommand().For(collection).Key(entryKey), linkName, linkedEntryKey, cancellationToken);
	}

	/// <inheritdoc />
	public Task UnlinkEntryAsync(string collection, IDictionary<string, object?> entryKey, string linkName, IDictionary<string, object?>? linkedEntryKey = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(collection);
		ArgumentNullException.ThrowIfNull(entryKey);
		RemoveAnnotationProperties(entryKey);
		if (linkedEntryKey is not null)
		{
			RemoveAnnotationProperties(linkedEntryKey);
		}

		return UnlinkEntryAsync(CreateCommand().For(collection).Key(entryKey), linkName, linkedEntryKey, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<Stream> GetMediaStreamAsync(string commandText, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (IsBatchResponse)
		{
			return Stream.Null;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(commandText, this).GetRequestAsync(scalarResult: true).ConfigureAwait(false);
		return await ExecuteGetStreamRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SetMediaStreamAsync(string commandText, Stream stream, string contentType, bool optimisticConcurrency = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(stream);
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(commandText, this).UpdateRequestAsync(stream, contentType, optimisticConcurrency).ConfigureAwait(false);
		await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<ODataDelta> FindDeltaAsync(Uri deltaLink, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(deltaLink);
		if (IsBatchRequest || IsBatchResponse)
		{
			throw new InvalidOperationException("Delta requests cannot be part of a batch.");
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(deltaLink.AbsoluteUri, this).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		try
		{
			using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			return await new ResponseReader(Session).ReadDeltaAsync(response, TryResolveCollection(deltaLink), cancellationToken).ConfigureAwait(false);
		}
		catch (WebRequestException ex) when (IsIgnoredNotFound(ex))
		{
			return new ODataDelta { Changed = [], Deleted = [], AddedLinks = [], DeletedLinks = [] };
		}
	}

	/// <inheritdoc />
	public async Task<ODataResponse> GetResponseAsync(ODataRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (IsBatchResponse)
		{
			return BatchResponse!;
		}

		if (IsBatchRequest)
		{
			return ODataResponse.Empty(Session.TypeCache);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			return await new ResponseReader(Session).ReadAsync(response, null, cancellationToken).ConfigureAwait(false);
		}
		catch (WebRequestException ex) when (IsIgnoredNotFound(ex))
		{
			return ODataResponse.Empty(Session.TypeCache);
		}
	}

	/// <inheritdoc />
	public Task<Stream> GetResponseStreamAsync(ODataRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return ExecuteGetStreamRequestAsync(request, cancellationToken);
	}

	// ---- command-based operations used by the fluent clients ---------------------------------------------------

	internal async Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesAsync(FluentCommand command, ODataFeedAnnotations? annotations, bool includeCount, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponseEntries(annotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (includeCount)
		{
			resolved.WithCount();
		}

		var request = await new RequestBuilder(resolved, this).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		var response = await FindResponseAsync(request, ExpectedCollection(resolved), annotations, cancellationToken).ConfigureAwait(false);
		if (response is null)
		{
			return [];
		}

		await EnrichWithMediaPropertiesAsync(response.Feed?.Entries, resolved.Details.MediaProperties, cancellationToken).ConfigureAwait(false);
		return response.AsEntries(IncludeAnnotations);
	}

	internal async Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesAsync(FluentCommand command, Uri annotatedUri, ODataFeedAnnotations? annotations, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponseEntries(annotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(annotatedUri.AbsoluteUri, this, command.Details.Headers, command.Details).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		var response = await FindResponseAsync(request, TryResolveCollection(command), annotations, cancellationToken).ConfigureAwait(false);
		if (response is null)
		{
			return [];
		}

		await EnrichWithMediaPropertiesAsync(response.Feed?.Entries, command.Details.MediaProperties, cancellationToken).ConfigureAwait(false);
		return response.AsEntries(IncludeAnnotations);
	}

	internal async Task<IDictionary<string, object?>?> FindEntryAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsEntry(IncludeAnnotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		var request = await new RequestBuilder(resolved, this).GetRequestAsync(scalarResult: false).ConfigureAwait(false);
		var response = await FindResponseAsync(request, ExpectedCollection(resolved), null, cancellationToken).ConfigureAwait(false);
		if (response is null)
		{
			return null;
		}

		await EnrichWithMediaPropertiesAsync(response.Feed?.Entries.FirstOrDefault(), resolved.Details.MediaProperties, cancellationToken).ConfigureAwait(false);
		return response.AsEntry(IncludeAnnotations);
	}

	internal async Task<object?> FindScalarAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsScalar<object>();
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(command.Resolve(Session), this).GetRequestAsync(scalarResult: true).ConfigureAwait(false);
		return await ExecuteRequestWithResultAsync(request, null, x => x.AsScalar<object>(), () => null, cancellationToken).ConfigureAwait(false);
	}

	internal async Task<IDictionary<string, object?>?> InsertEntryAsync(FluentCommand command, bool resultRequired, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsEntry(IncludeAnnotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		var request = await new RequestBuilder(resolved, this).InsertRequestAsync(resultRequired).ConfigureAwait(false);
		var result = await ExecuteRequestWithResultAsync(request, ExpectedCollection(resolved), x => x.AsEntry(IncludeAnnotations), () => null, cancellationToken, () => resolved.CommandData).ConfigureAwait(false);
		if (result is null && resultRequired && !IsBatchRequest && HasAllKeyProperties(resolved))
		{
			result = await GetEntryAsync(request.CommandText, resolved.CommandData, cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	internal async Task<IDictionary<string, object?>?> UpdateEntryAsync(FluentCommand command, bool resultRequired, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsEntry(IncludeAnnotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (!resolved.Details.HasKey)
		{
			if (!resolved.Details.HasFilter)
			{
				throw new InvalidOperationException("No entry key specified.");
			}

			var results = await UpdateEntriesAsync(resolved, resultRequired, cancellationToken).ConfigureAwait(false);
			return results.Count > 0 ? results[0] : null;
		}

		var request = await new RequestBuilder(resolved, this).UpdateRequestAsync(resultRequired).ConfigureAwait(false);
		var result = await ExecuteRequestWithResultAsync(request, ExpectedCollection(resolved), x => x.AsEntry(IncludeAnnotations), () => null, cancellationToken, () => request.EntryData).ConfigureAwait(false);
		if (result is null && resultRequired && !IsBatchRequest)
		{
			result = await GetUpdatedResultAsync(resolved, cancellationToken).ConfigureAwait(false);
		}

		if (!Settings.IgnoreNavigationPropertiesOnUpdate && request.EntryData is { } entryData)
		{
			await UnlinkRemovedReferencesAsync(resolved, entryData, cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	internal async Task<IReadOnlyList<IDictionary<string, object?>>> UpdateEntriesAsync(FluentCommand command, bool resultRequired, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponseEntries(null);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return await UpdateEntriesAsync(command.Resolve(Session), resultRequired, cancellationToken).ConfigureAwait(false);
	}

	internal async Task DeleteEntryAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (!resolved.Details.HasKey)
		{
			if (!resolved.Details.HasFilter)
			{
				throw new InvalidOperationException("No entry key specified.");
			}

			await DeleteEntriesAsync(resolved, cancellationToken).ConfigureAwait(false);
			return;
		}

		var request = await new RequestBuilder(resolved, this).DeleteRequestAsync().ConfigureAwait(false);
		await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	internal async Task<int> DeleteEntriesAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return 0;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return await DeleteEntriesAsync(command.Resolve(Session), cancellationToken).ConfigureAwait(false);
	}

	internal async Task LinkEntryAsync(FluentCommand command, string linkName, IDictionary<string, object?> linkedEntryKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(linkName);
		ArgumentNullException.ThrowIfNull(linkedEntryKey);
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(command.Resolve(Session), this).LinkRequestAsync(linkName, linkedEntryKey).ConfigureAwait(false);
		await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	internal async Task UnlinkEntryAsync(FluentCommand command, string linkName, IDictionary<string, object?>? linkedEntryKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(linkName);
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(command.Resolve(Session), this).UnlinkRequestAsync(linkName, linkedEntryKey).ConfigureAwait(false);
		await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	internal async Task<Stream> GetMediaStreamAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return Stream.Null;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(command.Resolve(Session), this).GetRequestAsync(scalarResult: true).ConfigureAwait(false);
		return await ExecuteGetStreamRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	internal async Task SetMediaStreamAsync(FluentCommand command, Stream stream, string contentType, bool optimisticConcurrency, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var request = await new RequestBuilder(command.Resolve(Session), this).UpdateRequestAsync(stream, contentType, optimisticConcurrency).ConfigureAwait(false);
		await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
	}

	// ---- bulk operations ---------------------------------------------------------------------------------------

	private async Task<IReadOnlyList<IDictionary<string, object?>>> UpdateEntriesAsync(ResolvedCommand resolved, bool resultRequired, CancellationToken cancellationToken)
	{
		if (resolved.Details.HasKey)
		{
			var request = await new RequestBuilder(resolved, this).UpdateRequestAsync(resultRequired).ConfigureAwait(false);
			var single = await ExecuteRequestWithResultAsync(request, ExpectedCollection(resolved), x => x.AsEntry(IncludeAnnotations), () => null, cancellationToken, () => request.EntryData).ConfigureAwait(false);
			return single is null ? [] : [single];
		}

		if (Settings.BulkOperationMode == BulkOperationMode.Each)
		{
			var request = await new RequestBuilder(resolved.Each(), this).UpdateRequestAsync(resultRequired).ConfigureAwait(false);
			return await ExecuteRequestWithResultAsync(request, ExpectedCollection(resolved), x => x.AsEntries(IncludeAnnotations), () => [], cancellationToken).ConfigureAwait(false);
		}

		var collectionName = resolved.QualifiedEntityCollectionName;
		var entryData = resolved.CommandData;
		var results = new List<IDictionary<string, object?>>();
		foreach (var entry in await FindEntriesForIterationAsync(resolved, cancellationToken).ConfigureAwait(false))
		{
			var updated = await UpdateEntryAsync(CreateCommand().For(collectionName).Key(entry).Set(entryData), resultRequired, cancellationToken).ConfigureAwait(false);
			if (updated is not null)
			{
				results.Add(updated);
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		return results;
	}

	private async Task<int> DeleteEntriesAsync(ResolvedCommand resolved, CancellationToken cancellationToken)
	{
		if (resolved.Details.HasKey)
		{
			var request = await new RequestBuilder(resolved, this).DeleteRequestAsync().ConfigureAwait(false);
			await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			return 1;
		}

		if (Settings.BulkOperationMode == BulkOperationMode.Each)
		{
			var request = await new RequestBuilder(resolved.Each(), this).DeleteRequestAsync().ConfigureAwait(false);
			await ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			return -1;
		}

		var collectionName = resolved.QualifiedEntityCollectionName;
		var count = 0;
		foreach (var entry in await FindEntriesForIterationAsync(resolved, cancellationToken).ConfigureAwait(false))
		{
			await DeleteEntryAsync(CreateCommand().For(collectionName).Key(entry), cancellationToken).ConfigureAwait(false);
			count++;
			cancellationToken.ThrowIfCancellationRequested();
		}

		return count;
	}

	/// <summary>Reads the entries a bulk operation applies to, always through a direct request (never into a batch).</summary>
	private Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesForIterationAsync(ResolvedCommand resolved, CancellationToken cancellationToken) =>
		new ODataClient(this).FindEntriesAsync(resolved.Format(), null, cancellationToken);
}
