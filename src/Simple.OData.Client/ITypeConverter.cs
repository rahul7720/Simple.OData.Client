namespace Simple.OData.Client;

/// <summary>
/// A registry of custom conversions used when materializing response values into CLR types.
/// </summary>
public interface ITypeConverter
{
	/// <summary>Registers a converter that builds <typeparamref name="T"/> from a property dictionary (complex values).</summary>
	void RegisterTypeConverter<T>(Func<IDictionary<string, object?>, T> converter);

	/// <summary>Registers a converter that builds <typeparamref name="T"/> from a scalar value.</summary>
	void RegisterTypeConverter<T>(Func<object, T> converter);

	/// <summary>Registers a converter that builds <paramref name="type"/> from a property dictionary (complex values).</summary>
	void RegisterTypeConverter(Type type, Func<IDictionary<string, object?>, object> converter);

	/// <summary>Registers a converter that builds <paramref name="type"/> from a scalar value.</summary>
	void RegisterTypeConverter(Type type, Func<object, object> converter);

	/// <summary>Returns whether a dictionary converter is registered for <paramref name="type"/>.</summary>
	bool HasDictionaryConverter(Type type);

	/// <summary>Returns whether a scalar converter is registered for <paramref name="type"/>.</summary>
	bool HasObjectConverter(Type type);

	/// <summary>Converts a property dictionary using the registered dictionary converter.</summary>
	object Convert(IDictionary<string, object?> value, Type type);

	/// <summary>Converts a scalar value using the registered scalar converter.</summary>
	object Convert(object value, Type type);

	/// <summary>Converts a property dictionary using the registered dictionary converter.</summary>
	T Convert<T>(IDictionary<string, object?> value);

	/// <summary>Converts a scalar value using the registered scalar converter.</summary>
	T Convert<T>(object value);
}
