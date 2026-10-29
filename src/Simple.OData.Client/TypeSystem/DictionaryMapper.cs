using System.Collections;
using System.Reflection;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// Materializes property dictionaries into CLR objects and extracts CLR objects into property dictionaries.
/// </summary>
internal static class DictionaryMapper
{
	/// <summary>Hook installed by the dynamic API to create <c>DynamicODataEntry</c> instances.</summary>
	internal static Func<IDictionary<string, object?>, TypeCache, ODataEntry>? DynamicEntryFactory { get; set; }

	public static object? ToObject(TypeCache typeCache, IDictionary<string, object?>? source, Type type, bool dynamicObject)
	{
		ArgumentNullException.ThrowIfNull(type);
		if (source is null)
		{
			return null;
		}

		if (type.IsInstanceOfType(source))
		{
			return source;
		}

		if (type == typeof(ODataEntry))
		{
			return CreateODataEntry(typeCache, source, dynamicObject);
		}

		if (typeof(IDictionary<string, object?>).IsAssignableFrom(type))
		{
			return new Dictionary<string, object?>(source, StringComparer.Ordinal);
		}

		if (type == typeof(string) || type.IsValueType)
		{
			throw new InvalidOperationException($"Unable to convert structural data to {type.Name}.");
		}

		if (source.TryGetValue(ReservedNames.Annotations, out var annotationsValue) && annotationsValue is ODataEntryAnnotations annotations)
		{
			type = ResolveTypeFromAnnotation(typeCache, annotations, type);
		}

		if (typeCache.Converter.HasDictionaryConverter(type))
		{
			return typeCache.Converter.Convert(source, type);
		}

		var descriptor = typeCache.Descriptor(type);
		if (descriptor.IsAnonymous)
		{
			return CreateAnonymous(typeCache, descriptor, source);
		}

		var instance = descriptor.Activator?.Invoke()
			?? throw new MissingMethodException($"Type {type.FullName} has no public parameterless constructor.");

		IDictionary<string, object?>? dynamicProperties = null;
		var containerName = typeCache.DynamicContainerName(type);
		if (!string.IsNullOrEmpty(containerName))
		{
			dynamicProperties = CreateDynamicPropertiesContainer(typeCache, descriptor, instance, containerName);
		}

		var resolver = typeCache.NameMatchResolver;
		foreach (var item in source)
		{
			if (item.Key == ReservedNames.Annotations)
			{
				if (descriptor.AnnotationsProperty is { CanWrite: true } annotationsProperty)
				{
					annotationsProperty.SetValue(instance, item.Value);
				}

				continue;
			}

			var property = descriptor.FindByMappedName(item.Key, resolver);
			if (property is not null)
			{
				if (property.CanWrite && item.Value is not null)
				{
					property.SetValue(instance, ConvertValue(typeCache, property.PropertyType, item.Value));
				}
			}
			else
			{
				dynamicProperties?.TryAdd(item.Key, item.Value);
			}
		}

		return instance;
	}

	public static IDictionary<string, object?> ToDictionary(TypeCache typeCache, object source)
	{
		ArgumentNullException.ThrowIfNull(source);
		switch (source)
		{
			case IDictionary<string, object?> dictionary:
				return dictionary;
			case ODataEntry entry:
				return entry.AsDictionary();
		}

		var descriptor = typeCache.Descriptor(source.GetType());
		var result = new Dictionary<string, object?>(descriptor.Properties.Count, StringComparer.Ordinal);
		foreach (var property in descriptor.Properties)
		{
			if (property.PropertyType == typeof(ODataEntryAnnotations))
			{
				continue;
			}

			result[property.MappedName] = property.GetValue(source);
		}

		return result;
	}

	internal static object? ConvertValue(TypeCache typeCache, Type targetType, object value)
	{
		if (targetType == typeof(ODataEntryAnnotations) || targetType.IsInstanceOfType(value))
		{
			return value;
		}

		if (value is IDictionary<string, object?> nested)
		{
			if (typeCache.Converter.HasDictionaryConverter(targetType))
			{
				return typeCache.Converter.Convert(nested, targetType);
			}

			if (IsCompoundType(targetType))
			{
				return ToObject(typeCache, nested, targetType, dynamicObject: false);
			}
		}

		if (value is IEnumerable enumerable and not string && targetType.IsEnumerableType(out var elementType))
		{
			return ConvertCollection(typeCache, targetType, elementType, enumerable);
		}

		if (value is ODataEntry entryValue && IsCompoundType(targetType))
		{
			return ToObject(typeCache, entryValue.AsDictionary(), targetType, dynamicObject: false);
		}

		if (IsCompoundType(targetType) && !typeCache.Converter.HasObjectConverter(targetType) && value is not IConvertible && value.GetType() is { IsClass: true } valueType && !Utils.IsSystemType(valueType))
		{
			return ToObject(typeCache, ToDictionary(typeCache, value), targetType, dynamicObject: false);
		}

		return typeCache.TryConvert(value, targetType, out var converted) ? converted : value;
	}

	private static bool IsCompoundType(Type type) =>
		!type.IsValueType && !type.IsArray && type != typeof(string) && !type.IsEnum && type != typeof(byte[])
		&& !typeof(IEnumerable).IsAssignableFrom(type) && !Utils.IsSystemType(type);

	private static object? ConvertCollection(TypeCache typeCache, Type targetType, Type elementType, IEnumerable source)
	{
		var items = new List<object?>();
		foreach (var item in source)
		{
			items.Add(item is null ? null : ConvertValue(typeCache, elementType, item));
		}

		var array = Array.CreateInstance(elementType, items.Count);
		for (var i = 0; i < items.Count; i++)
		{
			array.SetValue(items[i], i);
		}

		if (targetType.IsArray || targetType.IsAssignableFrom(array.GetType()))
		{
			return array;
		}

		var concreteType = targetType;
		if (targetType.IsInterface)
		{
			var definition = targetType.GetGenericTypeDefinition();
			if (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
			{
				concreteType = typeof(HashSet<>).MakeGenericType(elementType);
			}
			else
			{
				concreteType = typeof(List<>).MakeGenericType(elementType);
			}
		}

		var listType = typeof(IList<>).MakeGenericType(elementType);
		var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
		var activator = ExpressionActivator.GetActivator(concreteType, listType)
			?? ExpressionActivator.GetActivator(concreteType, enumerableType)
			?? throw new MissingMethodException($"Type {targetType.FullName} has no constructor accepting IEnumerable<{elementType.Name}>.");

		return activator(array);
	}

	private static object CreateAnonymous(TypeCache typeCache, ClrTypeDescriptor descriptor, IDictionary<string, object?> source)
	{
		var constructor = descriptor.Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
		var parameters = constructor.GetParameters();
		var arguments = new object?[parameters.Length];
		for (var i = 0; i < parameters.Length; i++)
		{
			var parameter = parameters[i];
			var property = descriptor.FindByClrName(parameter.Name!, typeCache.NameMatchResolver);
			var mappedName = property?.MappedName ?? parameter.Name!;
			object? value = null;
			if (!source.TryGetValue(mappedName, out value) && !source.TryGetValue(parameter.Name!, out value))
			{
				foreach (var pair in source)
				{
					if (typeCache.NameMatchResolver.IsMatch(pair.Key, mappedName))
					{
						value = pair.Value;
						break;
					}
				}
			}

			arguments[i] = value is null
				? (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null ? Activator.CreateInstance(parameter.ParameterType) : null)
				: ConvertValue(typeCache, parameter.ParameterType, value);
		}

		return constructor.Invoke(arguments);
	}

	private static Type ResolveTypeFromAnnotation(TypeCache typeCache, ODataEntryAnnotations annotations, Type type)
	{
		var odataType = annotations.TypeName;
		if (string.IsNullOrEmpty(odataType))
		{
			return type;
		}

		var resolver = ODataNameMatchResolver.NotStrict;
		if (resolver.IsMatch(odataType, typeCache.GetMappedName(type)))
		{
			return type;
		}

		foreach (var derived in typeCache.GetDerivedTypes(type))
		{
			if (resolver.IsMatch(odataType, typeCache.GetMappedName(derived)))
			{
				return derived;
			}
		}

		return type;
	}

	private static IDictionary<string, object?> CreateDynamicPropertiesContainer(TypeCache typeCache, ClrTypeDescriptor descriptor, object instance, string containerName)
	{
		var property = descriptor.FindByClrName(containerName, typeCache.NameMatchResolver)
			?? throw new ArgumentException($"Type {descriptor.Type} does not have property {containerName}.");
		if (!typeof(IDictionary<string, object?>).IsAssignableFrom(property.PropertyType) || !property.CanWrite)
		{
			throw new InvalidOperationException($"Property {containerName} must be a writable IDictionary<string, object>.");
		}

		if (property.GetValue(instance) is IDictionary<string, object?> existing)
		{
			return existing;
		}

		var container = new Dictionary<string, object?>(StringComparer.Ordinal);
		property.SetValue(instance, container);
		return container;
	}

	private static ODataEntry CreateODataEntry(TypeCache typeCache, IDictionary<string, object?> source, bool dynamicObject) =>
		dynamicObject && DynamicEntryFactory is { } factory ? factory(source, typeCache) : new ODataEntry(source);
}
