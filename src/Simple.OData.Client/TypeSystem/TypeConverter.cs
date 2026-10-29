using System.Collections.Concurrent;

namespace Simple.OData.Client;

/// <summary>
/// The default <see cref="ITypeConverter"/>: a thread-safe registry of custom conversions.
/// </summary>
public sealed class TypeConverter : ITypeConverter
{
	private readonly ConcurrentDictionary<Type, Func<IDictionary<string, object?>, object>> _dictionaryConverters = new();
	private readonly ConcurrentDictionary<Type, Func<object, object>> _objectConverters = new();

	/// <inheritdoc />
	public void RegisterTypeConverter<T>(Func<IDictionary<string, object?>, T> converter)
	{
		ArgumentNullException.ThrowIfNull(converter);
		RegisterTypeConverter(typeof(T), d => converter(d)!);
	}

	/// <inheritdoc />
	public void RegisterTypeConverter<T>(Func<object, T> converter)
	{
		ArgumentNullException.ThrowIfNull(converter);
		RegisterTypeConverter(typeof(T), o => converter(o)!);
	}

	/// <inheritdoc />
	public void RegisterTypeConverter(Type type, Func<IDictionary<string, object?>, object> converter)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(converter);
		_dictionaryConverters[type] = converter;
	}

	/// <inheritdoc />
	public void RegisterTypeConverter(Type type, Func<object, object> converter)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(converter);
		_objectConverters[type] = converter;
	}

	/// <inheritdoc />
	public bool HasDictionaryConverter(Type type) => _dictionaryConverters.ContainsKey(type);

	/// <inheritdoc />
	public bool HasObjectConverter(Type type) => _objectConverters.ContainsKey(type);

	/// <inheritdoc />
	public object Convert(IDictionary<string, object?> value, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return _dictionaryConverters.TryGetValue(type, out var converter)
			? converter(value)
			: throw new InvalidOperationException($"No custom converter found for type {type}");
	}

	/// <inheritdoc />
	public object Convert(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return _objectConverters.TryGetValue(type, out var converter)
			? converter(value)
			: throw new InvalidOperationException($"No custom converter found for type {type}");
	}

	/// <inheritdoc />
	public T Convert<T>(IDictionary<string, object?> value) => (T)Convert(value, typeof(T));

	/// <inheritdoc />
	public T Convert<T>(object value) => (T)Convert(value, typeof(T));
}
