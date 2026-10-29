using System.Diagnostics.CodeAnalysis;

namespace Simple.OData.Client;

/// <summary>A fluent client for unbound functions, actions and singletons, producing results of type <typeparamref name="T"/>.</summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public interface IUnboundClient<T> : IFluentClient<T, IUnboundClient<T>>
	where T : class
{
	/// <summary>Restricts the result to a derived type (<c>/Namespace.Type</c>).</summary>
	IUnboundClient<IDictionary<string, object?>> As(string derivedCollectionName);

	/// <summary>Restricts the result to the derived type <typeparamref name="TResult"/>.</summary>
	IUnboundClient<TResult> As<TResult>(string? derivedCollectionName = null) where TResult : class;

	/// <summary>Restricts the result to a derived type named by a dynamic expression.</summary>
	IUnboundClient<ODataEntry> As(ODataExpression expression);

	/// <summary>Sets the operation parameters from the properties of <paramref name="value"/>.</summary>
	IUnboundClient<T> Set(object value);

	/// <summary>Sets the operation parameters from named values.</summary>
	IUnboundClient<T> Set(IDictionary<string, object?> value);

	/// <summary>Sets the operation parameters from a typed object.</summary>
	IUnboundClient<T> Set(T entry);

	/// <summary>Sets the operation parameters from dynamic <c>x.Name = value</c> expressions.</summary>
	IUnboundClient<T> Set(params ODataExpression[] value);
}
