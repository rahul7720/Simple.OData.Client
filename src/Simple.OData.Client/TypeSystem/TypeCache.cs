using System.Collections.Concurrent;
using System.Reflection;
using Simple.OData.Client.TypeSystem;

namespace Simple.OData.Client;

/// <summary>
/// The default <see cref="ITypeCache"/>. One instance is shared by all requests made through the same settings.
/// </summary>
public sealed class TypeCache : ITypeCache
{
	private readonly ConcurrentDictionary<Type, ClrTypeDescriptor> _descriptors = new();
	private readonly ConcurrentDictionary<Type, string> _dynamicContainers = new();

	/// <summary>Creates a cache with the given converters and property name resolver.</summary>
	public TypeCache(ITypeConverter converter, INameMatchResolver nameMatchResolver)
	{
		ArgumentNullException.ThrowIfNull(converter);
		ArgumentNullException.ThrowIfNull(nameMatchResolver);
		Converter = converter;
		NameMatchResolver = nameMatchResolver;
	}

	/// <summary>Creates a cache with a fresh <see cref="TypeConverter"/> and the lenient name resolver.</summary>
	public TypeCache() : this(new TypeConverter(), ODataNameMatchResolver.NotStrict)
	{
	}

	/// <inheritdoc />
	public ITypeConverter Converter { get; }

	/// <inheritdoc />
	public INameMatchResolver NameMatchResolver { get; }

	internal ClrTypeDescriptor Descriptor(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return _descriptors.GetOrAdd(type, static t => new ClrTypeDescriptor(t));
	}

	/// <inheritdoc />
	public void Register<T>(string dynamicContainerName = "DynamicProperties") => Register(typeof(T), dynamicContainerName);

	/// <inheritdoc />
	public void Register(Type type, string dynamicContainerName = "DynamicProperties")
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentException.ThrowIfNullOrEmpty(dynamicContainerName);
		_dynamicContainers[type] = dynamicContainerName;
		foreach (var derived in Descriptor(type).DerivedTypes)
		{
			_dynamicContainers[derived] = dynamicContainerName;
		}
	}

	/// <inheritdoc />
	public string? DynamicContainerName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return _dynamicContainers.TryGetValue(type, out var name) ? name : null;
	}

	/// <inheritdoc />
	public PropertyInfo? GetAnnotationsProperty(Type type) => Descriptor(type).AnnotationsProperty?.Property;

	/// <inheritdoc />
	public IReadOnlyList<Type> GetDerivedTypes(Type type) => Descriptor(type).DerivedTypes;

	/// <inheritdoc />
	public IReadOnlyList<PropertyInfo> GetMappedProperties(Type type) => Descriptor(type).Properties.Select(x => x.Property).ToArray();

	/// <inheritdoc />
	public IReadOnlyList<PropertyInfo> GetAllProperties(Type type) => Descriptor(type).AllProperties;

	/// <inheritdoc />
	public IReadOnlyList<PropertyInfo> GetDeclaredProperties(Type type) => Descriptor(type).DeclaredProperties;

	/// <inheritdoc />
	public PropertyInfo? GetMappedProperty(Type type, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Descriptor(type).FindByMappedName(propertyName, NameMatchResolver)?.Property;
	}

	/// <inheritdoc />
	public PropertyInfo? GetNamedProperty(Type type, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(propertyName);
		return type.FindInstanceProperty(propertyName);
	}

	/// <inheritdoc />
	public PropertyInfo? GetDeclaredProperty(Type type, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		foreach (var property in Descriptor(type).DeclaredProperties)
		{
			if (property.Name == propertyName)
			{
				return property;
			}
		}

		return null;
	}

	/// <inheritdoc />
	public string GetMappedName(Type type) => Descriptor(type).MappedName;

	/// <inheritdoc />
	public string GetMappedName(Type type, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Descriptor(type).FindByClrName(propertyName, NameMatchResolver)?.MappedName ?? propertyName;
	}

	/// <inheritdoc />
	public string GetMappedName(Type type, PropertyInfo propertyInfo)
	{
		ArgumentNullException.ThrowIfNull(propertyInfo);
		return Descriptor(type).ByClrName.TryGetValue(propertyInfo.Name, out var accessor) ? accessor.MappedName : MemberMapping.GetMappedName(propertyInfo);
	}

	/// <inheritdoc />
	public bool IsAnonymousType(Type type) => Descriptor(type).IsAnonymous;

	/// <inheritdoc />
	public bool IsEnumType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return type.IsEnum;
	}

	/// <inheritdoc />
	public bool IsValue(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return type.IsValueType;
	}

	/// <inheritdoc />
	public bool IsTypeAssignableFrom(Type type, Type otherType)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(otherType);
		return type.IsAssignableFrom(otherType);
	}

	/// <inheritdoc />
	public bool TryConvert(object? value, Type targetType, out object? result)
	{
		ArgumentNullException.ThrowIfNull(targetType);
		return ValueConverter.TryConvert(Converter, value, targetType, out result);
	}

	/// <inheritdoc />
	public object? Convert(object? value, Type targetType)
	{
		ArgumentNullException.ThrowIfNull(targetType);
		if (value is null && (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null))
		{
			return null;
		}

		return TryConvert(value, targetType, out var result)
			? result
			: throw new FormatException($"Unable to convert value from type {value?.GetType()} to type {targetType}");
	}

	/// <inheritdoc />
	public T? Convert<T>(object? value) => (T?)Convert(value, typeof(T));

	/// <inheritdoc />
	public IDictionary<string, object?> ToDictionary(object source) => DictionaryMapper.ToDictionary(this, source);

	/// <inheritdoc />
	public object? ToObject(IDictionary<string, object?>? source, Type type, bool dynamicObject = false) => DictionaryMapper.ToObject(this, source, type, dynamicObject);

	/// <inheritdoc />
	public T? ToObject<T>(IDictionary<string, object?>? source, bool dynamicObject = false) where T : class => (T?)DictionaryMapper.ToObject(this, source, typeof(T), dynamicObject);
}
