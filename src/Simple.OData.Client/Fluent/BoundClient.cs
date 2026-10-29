using System.Diagnostics.CodeAnalysis;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

/// <summary>
/// The fluent client bound to an entity set, singleton or navigation path. Public so that dynamically bound calls can
/// resolve its members at runtime; it is created only through <see cref="ODataClient.For{T}(string?)"/> and its siblings.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public sealed partial class BoundClient<T> : FluentClientBase<T, IBoundClient<T>>, IBoundClient<T>
	where T : class
{
	internal BoundClient(ODataClient client, FluentCommand? parentCommand = null, FluentCommand? command = null, bool dynamicResults = false)
		: base(client, parentCommand, command, dynamicResults)
	{
	}

	/// <inheritdoc />
	public IBoundClient<T> For(string? collectionName = null)
	{
		Command.For(collectionName ?? TypeCache.GetMappedName(typeof(T)));
		return this;
	}

	/// <inheritdoc />
	public IBoundClient<ODataEntry> For(ODataExpression expression)
	{
		Command.For(expression);
		return CreateClientForODataEntry();
	}

	/// <inheritdoc />
	public IBoundClient<IDictionary<string, object?>> As(string derivedCollectionName)
	{
		Command.As(derivedCollectionName);
		return new BoundClient<IDictionary<string, object?>>(Client, ParentCommand, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IBoundClient<TResult> As<TResult>(string? derivedCollectionName = null)
		where TResult : class
	{
		Command.As(derivedCollectionName ?? typeof(TResult).Name);
		return new BoundClient<TResult>(Client, ParentCommand, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IBoundClient<ODataEntry> As(ODataExpression expression)
	{
		Command.As(expression);
		return CreateClientForODataEntry();
	}

	/// <inheritdoc />
	public IBoundClient<T> Set(object value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public IBoundClient<T> Set(IDictionary<string, object?> value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public IBoundClient<T> Set(T entry)
	{
		Command.Set(entry);
		return this;
	}

	/// <inheritdoc />
	public IBoundClient<T> Set(params ODataExpression[] value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public bool FilterIsKey => FilterAsKey is not null;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object?>? FilterAsKey => Command.Details.HasKey ? null : Command.Resolve(Session).FilterAsKey;

	/// <inheritdoc />
	public IRequestBuilder<T> BuildRequestFor() => new RequestBuilder<T>(Command, Client);

	private BoundClient<ODataEntry> CreateClientForODataEntry() => new(Client, ParentCommand, Command, dynamicResults: true);
}
