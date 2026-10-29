using System.Diagnostics.CodeAnalysis;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client;

/// <summary>
/// The fluent client for unbound functions, actions and singletons. Public so that dynamically bound calls can resolve
/// its members at runtime; it is created only through <see cref="ODataClient.Unbound{T}"/>.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public sealed class UnboundClient<T> : FluentClientBase<T, IUnboundClient<T>>, IUnboundClient<T>
	where T : class
{
	internal UnboundClient(ODataClient client, FluentCommand? command = null, bool dynamicResults = false)
		: base(client, null, command, dynamicResults)
	{
	}

	/// <inheritdoc />
	public IUnboundClient<T> Set(object value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public IUnboundClient<T> Set(IDictionary<string, object?> value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public IUnboundClient<T> Set(T entry)
	{
		Command.Set(entry);
		return this;
	}

	/// <inheritdoc />
	public IUnboundClient<T> Set(params ODataExpression[] value)
	{
		Command.Set(value);
		return this;
	}

	/// <inheritdoc />
	public IUnboundClient<IDictionary<string, object?>> As(string derivedCollectionName)
	{
		Command.As(derivedCollectionName);
		return new UnboundClient<IDictionary<string, object?>>(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IUnboundClient<TResult> As<TResult>(string? derivedCollectionName = null)
		where TResult : class
	{
		Command.As(derivedCollectionName ?? typeof(TResult).Name);
		return new UnboundClient<TResult>(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IUnboundClient<ODataEntry> As(ODataExpression expression)
	{
		Command.As(expression);
		return new UnboundClient<ODataEntry>(Client, Command, dynamicResults: true);
	}
}
