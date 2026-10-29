using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

public sealed partial class ODataClient
{
	// ---- untyped API -------------------------------------------------------------------------------------------

	/// <inheritdoc />
	public async Task<IDictionary<string, object?>?> ExecuteFunctionAsSingleAsync(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		var results = await ExecuteFunctionAsEnumerableAsync(functionName, parameters, cancellationToken).ConfigureAwait(false);
		return results.Count > 0 ? results[0] : null;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteFunctionAsEnumerableAsync(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		return ExecuteAsEnumerableAsync(OperationCommand(parameters).Function(functionName), null, cancellationToken);
	}

	/// <inheritdoc />
	public Task<T?> ExecuteFunctionAsScalarAsync<T>(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		return ExecuteAsScalarAsync<T>(OperationCommand(parameters).Function(functionName), cancellationToken);
	}

	/// <inheritdoc />
	public Task<T[]> ExecuteFunctionAsArrayAsync<T>(string functionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		return ExecuteAsArrayAsync<T>(OperationCommand(parameters).Function(functionName), null, cancellationToken);
	}

	/// <inheritdoc />
	public Task ExecuteActionAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(actionName);
		return ExecuteAsync(OperationCommand(parameters).Action(actionName), cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object?>?> ExecuteActionAsSingleAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		var results = await ExecuteActionAsEnumerableAsync(actionName, parameters, cancellationToken).ConfigureAwait(false);
		return results.Count > 0 ? results[0] : null;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteActionAsEnumerableAsync(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(actionName);
		return ExecuteAsEnumerableAsync(OperationCommand(parameters).Action(actionName), null, cancellationToken);
	}

	/// <inheritdoc />
	public Task<T?> ExecuteActionAsScalarAsync<T>(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(actionName);
		return ExecuteAsScalarAsync<T>(OperationCommand(parameters).Action(actionName), cancellationToken);
	}

	/// <inheritdoc />
	public Task<T[]> ExecuteActionAsArrayAsync<T>(string actionName, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(actionName);
		return ExecuteAsArrayAsync<T>(OperationCommand(parameters).Action(actionName), null, cancellationToken);
	}

	// ---- command-based operations used by the fluent clients ---------------------------------------------------

	internal async Task ExecuteAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return;
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (resolved.Details.HasFunction)
		{
			await ExecuteFunctionAsync(resolved, null, cancellationToken).ConfigureAwait(false);
		}
		else if (resolved.Details.HasAction)
		{
			await ExecuteActionAsync(resolved, null, resultRequired: false, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			throw NotAnOperation();
		}
	}

	internal async Task<IDictionary<string, object?>?> ExecuteAsSingleAsync(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsEntry(IncludeAnnotations);
		}

		var results = await ExecuteAsEnumerableAsync(command, null, cancellationToken).ConfigureAwait(false);
		return results.Count > 0 ? results[0] : null;
	}

	internal async Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteAsEnumerableAsync(FluentCommand command, ODataFeedAnnotations? annotations, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponseEntries(annotations);
		}

		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		var resolved = command.Resolve(Session);
		if (resolved.Details.HasFunction)
		{
			return await ExecuteFunctionAsync(resolved, annotations, cancellationToken).ConfigureAwait(false);
		}

		if (resolved.Details.HasAction)
		{
			return await ExecuteActionAsync(resolved, annotations, resultRequired: true, cancellationToken).ConfigureAwait(false);
		}

		throw NotAnOperation();
	}

	internal async Task<T?> ExecuteAsScalarAsync<T>(FluentCommand command, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsScalar<T>();
		}

		var result = await ExecuteAsSingleAsync(command, cancellationToken).ConfigureAwait(false);
		if (IsBatchRequest || result is null || result.Count == 0)
		{
			return default;
		}

		return Session.TypeCache.Convert<T>(result.Values.First());
	}

	internal async Task<T[]> ExecuteAsArrayAsync<T>(FluentCommand command, ODataFeedAnnotations? annotations, CancellationToken cancellationToken)
	{
		if (IsBatchResponse)
		{
			return BatchResponse!.AsArray<T>();
		}

		var results = await ExecuteAsEnumerableAsync(command, annotations, cancellationToken).ConfigureAwait(false);
		if (IsBatchRequest)
		{
			return [];
		}

		var typeCache = Session.TypeCache;
		return results.SelectMany(x => x.Values).Select(x => typeCache.Convert<T>(x)!).ToArray();
	}

	private async Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteFunctionAsync(ResolvedCommand resolved, ODataFeedAnnotations? annotations, CancellationToken cancellationToken)
	{
		var request = await new RequestBuilder(resolved, this).FunctionRequestAsync().ConfigureAwait(false);
		var collection = TryGetReturnCollection(() => Session.Metadata.GetFunctionReturnCollection(resolved.Details.FunctionName!));
		var response = await FindResponseAsync(request, collection, annotations, cancellationToken).ConfigureAwait(false);
		return response?.AsEntries(IncludeAnnotations) ?? [];
	}

	private async Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteActionAsync(ResolvedCommand resolved, ODataFeedAnnotations? annotations, bool resultRequired, CancellationToken cancellationToken)
	{
		var request = await new RequestBuilder(resolved, this).ActionRequestAsync(resultRequired).ConfigureAwait(false);
		var collection = TryGetReturnCollection(() => Session.Metadata.GetActionReturnCollection(resolved.Details.ActionName!));
		var response = await FindResponseAsync(request, collection, annotations, cancellationToken).ConfigureAwait(false);
		return response?.AsEntries(IncludeAnnotations) ?? [];
	}

	private FluentCommand OperationCommand(IDictionary<string, object?>? parameters)
	{
		var command = CreateCommand();
		return parameters is null ? command : command.Set(parameters);
	}

	private static EntityCollection? TryGetReturnCollection(Func<EntityCollection?> lookup)
	{
		try
		{
			return lookup();
		}
		catch (UnresolvableObjectException)
		{
			return null;
		}
	}

	private static InvalidOperationException NotAnOperation() =>
		new("Only functions and actions can be executed; use the Find/Insert/Update/Delete methods for entity operations.");
}
