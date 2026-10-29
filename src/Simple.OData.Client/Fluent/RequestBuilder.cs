using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client;

/// <summary>Builds <see cref="ODataRequest"/>s for a resolved command or raw command text. The session must be initialized.</summary>
internal sealed class RequestBuilder
{
	private readonly ResolvedCommand? _command;
	private readonly string? _commandText;
	private readonly IDictionary<string, string>? _headers;
	private readonly ODataClient _client;

	public RequestBuilder(ResolvedCommand command, ODataClient client)
	{
		_command = command ?? throw new ArgumentNullException(nameof(command));
		_client = client ?? throw new ArgumentNullException(nameof(client));
	}

	public RequestBuilder(string commandText, ODataClient client, IDictionary<string, string>? headers = null, CommandDetails? details = null)
	{
		_commandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_headers = headers;
		Details = details;
	}

	private Session Session => _client.Session;

	private RequestWriter Writer => _client.RequestWriter;

	private CommandDetails? Details { get; init; }

	private CommandDetails? EffectiveDetails => _command?.Details ?? Details;

	private IDictionary<string, string>? Headers => EffectiveDetails?.Headers ?? _headers;

	private ResolvedCommand Command => _command ?? throw new InvalidOperationException("This request requires a resolved command.");

	private string CommandText => _commandText ?? Command.Format();

	public async Task<ODataRequest> GetRequestAsync(bool scalarResult)
	{
		var request = await Writer.CreateGetRequestAsync(CommandText, scalarResult, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> InsertRequestAsync(bool resultRequired)
	{
		var command = Command;
		var request = await Writer.CreateInsertRequestAsync(command.QualifiedEntityCollectionName, command.Format(), command.CommandData, resultRequired, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> UpdateRequestAsync(bool resultRequired)
	{
		var command = Command;
		var entryKey = command.Details.UseEachSegment || command.IsSingleton ? new Dictionary<string, object?>() : RequireKey(command);
		var request = await Writer.CreateUpdateRequestAsync(command.QualifiedEntityCollectionName, command.Format(), entryKey, command.CommandData, resultRequired, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> UpdateRequestAsync(Stream stream, string? contentType, bool optimisticConcurrency)
	{
		var request = await Writer.CreatePutRequestAsync(CommandText, stream, contentType, optimisticConcurrency, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> DeleteRequestAsync()
	{
		var command = Command;
		if (!command.Details.UseEachSegment)
		{
			RequireKey(command);
		}

		var request = await Writer.CreateDeleteRequestAsync(command.QualifiedEntityCollectionName, command.Format(), Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> FunctionRequestAsync()
	{
		var command = Command;
		var request = await Writer.CreateFunctionRequestAsync(command.Format(), command.Details.FunctionName!, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> ActionRequestAsync(bool resultRequired)
	{
		var command = Command;
		var boundTypeName = command.EntityCollection is { } collection ? Session.Metadata.GetQualifiedTypeName(collection.Name) : null;
		var request = await Writer.CreateActionRequestAsync(command.Format(), command.Details.ActionName!, boundTypeName, command.CommandData, resultRequired, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> LinkRequestAsync(string linkName, IDictionary<string, object?> linkedEntryKey)
	{
		var command = Command;
		if (!command.IsSingleton)
		{
			RequireKey(command);
		}

		var collectionName = command.QualifiedEntityCollectionName;
		var linkedCollection = Session.Metadata.GetNavigationPropertyPartnerTypeName(collectionName, linkName);
		var linkIdent = FormatEntryKey(linkedCollection, linkedEntryKey);
		var request = await Writer.CreateLinkRequestAsync(collectionName, linkName, command.Format(), linkIdent, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	public async Task<ODataRequest> UnlinkRequestAsync(string linkName, IDictionary<string, object?>? linkedEntryKey)
	{
		var command = Command;
		if (!command.IsSingleton)
		{
			RequireKey(command);
		}

		var collectionName = command.QualifiedEntityCollectionName;
		string? linkIdent = null;
		if (linkedEntryKey is not null)
		{
			var linkedCollection = Session.Metadata.GetNavigationPropertyPartnerTypeName(collectionName, linkName);
			linkIdent = FormatEntryKey(linkedCollection, linkedEntryKey);
		}

		var request = await Writer.CreateUnlinkRequestAsync(collectionName, linkName, command.Format(), linkIdent, Headers).ConfigureAwait(false);
		return WithPreferences(request);
	}

	private ODataRequest WithPreferences(ODataRequest request)
	{
		if (EffectiveDetails is { } details)
		{
			if (details.MaxPageSize is { } maxPageSize)
			{
				request.AddPreference($"{HttpLiteral.MaxPageSize}={maxPageSize}");
			}

			if (details.TrackChanges)
			{
				request.AddPreference(HttpLiteral.TrackChanges);
			}
		}

		return request;
	}

	private string FormatEntryKey(string collection, IDictionary<string, object?> entryKey)
	{
		var details = new CommandDetails(null, null) { CollectionName = collection, NamedKeyValues = new Dictionary<string, object?>(entryKey) };
		return new FluentCommand(details).Resolve(Session).Format();
	}

	private static IReadOnlyDictionary<string, object?> RequireKey(ResolvedCommand command) =>
		command.KeyValues ?? command.FilterAsKey ?? throw new InvalidOperationException("No entry key specified.");
}

/// <summary>Builds the requests of a fluent command without sending them.</summary>
internal sealed class RequestBuilder<T>(FluentCommand command, ODataClient client) : IRequestBuilder<T>
	where T : class
{
	private Session Session => client.Session;

	public Task<IClientWithRequest<T>> FindEntriesAsync(CancellationToken cancellationToken = default) => BuildAsync(b => b.GetRequestAsync(scalarResult: false), includeCount: false, cancellationToken);

	public Task<IClientWithRequest<T>> FindEntriesAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default) => BuildAsync(b => b.GetRequestAsync(scalarResult: false), includeCount: true, cancellationToken);

	public Task<IClientWithRequest<T>> FindEntryAsync(CancellationToken cancellationToken = default) => BuildAsync(b => b.GetRequestAsync(scalarResult: false), includeCount: false, cancellationToken);

	public Task<IClientWithRequest<T>> FindScalarAsync(CancellationToken cancellationToken = default) => BuildAsync(b => b.GetRequestAsync(scalarResult: true), includeCount: false, cancellationToken);

	public Task<IClientWithRequest<T>> InsertEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default) => BuildAsync(b => b.InsertRequestAsync(resultRequired), includeCount: false, cancellationToken);

	public Task<IClientWithRequest<T>> UpdateEntryAsync(bool resultRequired = true, CancellationToken cancellationToken = default) => BuildAsync(b => b.UpdateRequestAsync(resultRequired), includeCount: false, cancellationToken);

	public Task<IClientWithRequest<T>> DeleteEntryAsync(CancellationToken cancellationToken = default) => BuildAsync(b => b.DeleteRequestAsync(), includeCount: false, cancellationToken);

	private async Task<IClientWithRequest<T>> BuildAsync(Func<RequestBuilder, Task<ODataRequest>> build, bool includeCount, CancellationToken cancellationToken)
	{
		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (includeCount)
		{
			resolved.WithCount();
		}

		var request = await build(new RequestBuilder(resolved, client)).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		return new ClientWithRequest<T>(request, client, resolved.EntityCollection);
	}
}
