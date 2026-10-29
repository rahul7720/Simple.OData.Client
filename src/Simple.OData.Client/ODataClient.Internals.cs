using System.Net;
using System.Runtime.ExceptionServices;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client;

public sealed partial class ODataClient
{
	// ---- request execution -------------------------------------------------------------------------------------

	/// <summary>Sends a request and parses the response, capturing feed annotations; <see langword="null"/> when there is no body to read.</summary>
	private async Task<ODataResponse?> FindResponseAsync(ODataRequest request, EntityCollection? collection, ODataFeedAnnotations? annotations, CancellationToken cancellationToken)
	{
		var response = await ExecuteRequestWithResultAsync<ODataResponse?>(request, collection, x => x, () => null, cancellationToken).ConfigureAwait(false);
		annotations?.CopyFrom(response?.Feed?.Annotations);
		return response;
	}

	private async Task<TResult> ExecuteRequestWithResultAsync<TResult>(
		ODataRequest request,
		EntityCollection? collection,
		Func<ODataResponse, TResult> createResult,
		Func<TResult> createEmptyResult,
		CancellationToken cancellationToken,
		Func<TResult>? createBatchResult = null)
	{
		if (IsBatchRequest)
		{
			return (createBatchResult ?? createEmptyResult)();
		}

		try
		{
			using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			if (HasBody(request, response))
			{
				return createResult(await new ResponseReader(Session).ReadAsync(response, collection, cancellationToken).ConfigureAwait(false));
			}

			return createEmptyResult();
		}
		catch (WebRequestException ex) when (IsIgnoredNotFound(ex))
		{
			return createEmptyResult();
		}
	}

	private async Task ExecuteRequestAsync(ODataRequest request, CancellationToken cancellationToken)
	{
		if (IsBatchRequest)
		{
			return;
		}

		try
		{
			using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (WebRequestException ex) when (IsIgnoredNotFound(ex))
		{
		}
	}

	private async Task<Stream> ExecuteGetStreamRequestAsync(ODataRequest request, CancellationToken cancellationToken)
	{
		if (IsBatchRequest)
		{
			return Stream.Null;
		}

		try
		{
			using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
			if (!HasBody(request, response))
			{
				return Stream.Null;
			}

			var stream = new MemoryStream();
			await response.Content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
			stream.Position = 0;
			return stream;
		}
		catch (WebRequestException ex) when (IsIgnoredNotFound(ex))
		{
			return Stream.Null;
		}
	}

	private static bool HasBody(ODataRequest request, HttpResponseMessage response) =>
		response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent && (request.Method == RestVerbs.Get || request.ResultRequired);

	private bool IsIgnoredNotFound(WebRequestException exception) => Settings.IgnoreResourceNotFoundException && exception.Code == HttpStatusCode.NotFound;

	private IReadOnlyList<IDictionary<string, object?>> BatchResponseEntries(ODataFeedAnnotations? annotations)
	{
		var response = BatchResponse!;
		annotations?.CopyFrom(response.Feed?.Annotations);
		return response.AsEntries(IncludeAnnotations);
	}

	// ---- batch -------------------------------------------------------------------------------------------------

	/// <summary>Writes the batch actions into one <c>$batch</c> request, sends it, and replays the actions against the operation responses.</summary>
	internal async Task ExecuteBatchAsync(IReadOnlyList<Func<IODataClient, Task>> actions, IDictionary<string, string>? headers, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(actions);
		if (actions.Count == 0)
		{
			return;
		}

		var writer = (BatchWriter ?? throw new InvalidOperationException("The client is not in batch mode.")).Value;
		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);

		var responseIndexes = new List<int>(actions.Count);
		var boundActions = actions.Select(action => (Func<Task>)(() => action(this))).ToList();
		var request = await writer.CreateBatchRequestAsync(boundActions, responseIndexes, headers).ConfigureAwait(false);
		if (request is null)
		{
			return;
		}

		using var response = await Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
		var batchResponse = await new ResponseReader(Session).ReadAsync(response, null, cancellationToken).ConfigureAwait(false);
		await AssignBatchResultsAsync(batchResponse, actions, responseIndexes).ConfigureAwait(false);
	}

	private async Task AssignBatchResultsAsync(ODataResponse batchResponse, IReadOnlyList<Func<IODataClient, Task>> actions, List<int> responseIndexes)
	{
		var responses = batchResponse.Batch ?? throw new InvalidOperationException("The service did not return a batch response.");
		Exception? firstFailure = null;
		for (var index = 0; index < actions.Count; index++)
		{
			var responseIndex = responseIndexes[index];
			if (responseIndex < 0 || responseIndex >= responses.Count)
			{
				continue;
			}

			var operationResponse = responses[responseIndex];
			if (operationResponse.Exception is { } failure)
			{
				firstFailure ??= failure;
				continue;
			}

			await actions[index](new ODataClient(this, operationResponse)).ConfigureAwait(false);
		}

		if (firstFailure is not null && !Settings.ContinueOnError)
		{
			ExceptionDispatchInfo.Capture(firstFailure).Throw();
		}
	}

	// ---- helpers -----------------------------------------------------------------------------------------------

	/// <summary>The collection the response is read against; none for aggregations, whose shape differs from the entity type.</summary>
	private static EntityCollection? ExpectedCollection(ResolvedCommand resolved) =>
		resolved.Details.ApplyClauses.Count > 0 ? null : resolved.EntityCollection;

	private EntityCollection? TryResolveCollection(FluentCommand command)
	{
		try
		{
			return ExpectedCollection(command.Resolve(Session));
		}
		catch (Exception ex) when (ex is UnresolvableObjectException or InvalidOperationException)
		{
			return null;
		}
	}

	private EntityCollection? TryResolveCollection(Uri uri)
	{
		var relative = Session.BaseUri.IsBaseOf(uri) ? Session.BaseUri.MakeRelativeUri(uri).ToString() : uri.PathAndQuery.TrimStart('/');
		var collectionName = Utils.ExtractCollectionName(Uri.UnescapeDataString(relative));
		if (string.IsNullOrEmpty(collectionName))
		{
			return null;
		}

		try
		{
			return Session.Metadata.GetEntityCollection(collectionName);
		}
		catch (UnresolvableObjectException)
		{
			return null;
		}
	}

	private bool HasAllKeyProperties(ResolvedCommand resolved)
	{
		if (resolved.EntityCollection is not { } collection)
		{
			return false;
		}

		var resolver = Settings.NameMatchResolver;
		var keys = resolved.CommandData.Keys;
		return Session.Metadata.GetDeclaredKeyPropertyNames(collection.Name).All(keyName => keys.Any(name => resolver.IsMatch(name, keyName)));
	}

	/// <summary>Re-reads an updated entry, honouring key values changed by the update.</summary>
	private async Task<IDictionary<string, object?>?> GetUpdatedResultAsync(ResolvedCommand resolved, CancellationToken cancellationToken)
	{
		if (resolved.IsSingleton)
		{
			return await FindEntryAsync(resolved.Format(), cancellationToken).ConfigureAwait(false);
		}

		if ((resolved.KeyValues ?? resolved.FilterAsKey) is not { } entryKey)
		{
			return null;
		}

		var updatedKey = new Dictionary<string, object?>(entryKey);
		foreach (var (name, value) in resolved.CommandData)
		{
			if (updatedKey.ContainsKey(name))
			{
				updatedKey[name] = value;
			}
		}

		try
		{
			var command = new FluentCommand(resolved).Key(updatedKey);
			return await FindEntryAsync(command.Resolve(Session).Format(), cancellationToken).ConfigureAwait(false);
		}
		catch (WebRequestException)
		{
			return null;
		}
	}

	/// <summary>Removes the references of single-valued navigation properties the update set to <see langword="null"/>.</summary>
	private async Task UnlinkRemovedReferencesAsync(ResolvedCommand resolved, IDictionary<string, object?> entryData, CancellationToken cancellationToken)
	{
		if (resolved.EntityCollection is not { } collection)
		{
			return;
		}

		var details = Session.Metadata.ParseEntryDetails(collection.Name, entryData);
		foreach (var (linkName, links) in details.Links)
		{
			if (links.Any(link => link.LinkData is null))
			{
				var navigationName = Session.Metadata.GetNavigationPropertyExactName(collection.Name, linkName);
				await UnlinkEntryAsync(new FluentCommand(resolved), navigationName, null, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static string ExtractFilter(string collection, string commandText)
	{
		var prefix = collection + "?" + ODataLiteral.Filter + "=";
		return commandText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? commandText[prefix.Length..] : commandText;
	}

	/// <summary>Strips the <c>__annotations</c> entries a previous read may have added, recursively.</summary>
	private static void RemoveAnnotationProperties(IDictionary<string, object?> entryData)
	{
		foreach (var key in entryData.Keys.Where(IsAnnotationKey).ToArray())
		{
			entryData.Remove(key);
		}

		foreach (var value in entryData.Values)
		{
			switch (value)
			{
				case IDictionary<string, object?> nested:
					RemoveAnnotationProperties(nested);
					break;
				case IEnumerable<IDictionary<string, object?>> items:
					foreach (var item in items)
					{
						RemoveAnnotationProperties(item);
					}

					break;
			}
		}
	}

	private static bool IsAnnotationKey(string key) =>
		key == ReservedNames.Annotations || key.StartsWith(ReservedNames.Annotations + "_", StringComparison.Ordinal);

	// ---- media -------------------------------------------------------------------------------------------------

	private async Task EnrichWithMediaPropertiesAsync(IEnumerable<AnnotatedEntry>? entries, List<string> mediaProperties, CancellationToken cancellationToken)
	{
		if (entries is null || mediaProperties.Count == 0)
		{
			return;
		}

		foreach (var entry in entries)
		{
			await EnrichWithMediaPropertiesAsync(entry, mediaProperties, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task EnrichWithMediaPropertiesAsync(AnnotatedEntry? entry, List<string> mediaProperties, CancellationToken cancellationToken)
	{
		if (entry is null || mediaProperties.Count == 0)
		{
			return;
		}

		if (entry.Annotations?.MediaResource is { } mediaResource)
		{
			var entityMediaPropertyName = mediaProperties.FirstOrDefault(x => !entry.Data.ContainsKey(x)) ?? ReservedNames.MediaEntity;
			await ReadMediaStreamValueAsync(entry.Data, entityMediaPropertyName, mediaResource, cancellationToken).ConfigureAwait(false);
		}

		foreach (var propertyName in mediaProperties)
		{
			if (entry.Data.TryGetValue(propertyName, out var value) && value is ODataMediaAnnotations annotations)
			{
				await ReadMediaStreamValueAsync(entry.Data, propertyName, annotations, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task ReadMediaStreamValueAsync(IDictionary<string, object?> entry, string propertyName, ODataMediaAnnotations annotations, CancellationToken cancellationToken)
	{
		if ((annotations.ReadLink ?? annotations.EditLink) is not { } link)
		{
			return;
		}

		entry[propertyName] = await GetMediaStreamAsync(link.AbsoluteUri, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
	}
}
