using System.Reflection;

namespace Simple.OData.Client;

/// <summary>
/// Caches CLR type information (mapped names, properties, derived types) and performs value and object conversion.
/// </summary>
public interface ITypeCache
{
	/// <summary>The custom converters used by this cache.</summary>
	ITypeConverter Converter { get; }

	/// <summary>The resolver used to match property names.</summary>
	INameMatchResolver NameMatchResolver { get; }

	/// <summary>Registers <typeparamref name="T"/> (and its derived types in the same assembly) as an open type whose unmapped properties are stored in <paramref name="dynamicContainerName"/>.</summary>
	void Register<T>(string dynamicContainerName = "DynamicProperties");

	/// <summary>Registers <paramref name="type"/> (and its derived types in the same assembly) as an open type whose unmapped properties are stored in <paramref name="dynamicContainerName"/>.</summary>
	void Register(Type type, string dynamicContainerName = "DynamicProperties");

	/// <summary>Returns the dynamic properties container name registered for <paramref name="type"/>, or <see langword="null"/>.</summary>
	string? DynamicContainerName(Type type);

	/// <summary>Returns the property of type <see cref="ODataEntryAnnotations"/>, when the type declares one.</summary>
	PropertyInfo? GetAnnotationsProperty(Type type);

	/// <summary>Returns the non-abstract types deriving from <paramref name="type"/> in its assembly.</summary>
	IReadOnlyList<Type> GetDerivedTypes(Type type);

	/// <summary>Returns the properties that take part in OData mapping (excluding <c>[NotMapped]</c>/<c>[JsonIgnore]</c> properties).</summary>
	IReadOnlyList<PropertyInfo> GetMappedProperties(Type type);

	/// <summary>Returns all instance properties, including inherited ones.</summary>
	IReadOnlyList<PropertyInfo> GetAllProperties(Type type);

	/// <summary>Returns the instance properties declared directly on <paramref name="type"/>.</summary>
	IReadOnlyList<PropertyInfo> GetDeclaredProperties(Type type);

	/// <summary>Finds a mapped property by its OData (mapped) name using the <see cref="NameMatchResolver"/>.</summary>
	PropertyInfo? GetMappedProperty(Type type, string propertyName);

	/// <summary>Finds a property by its CLR name, walking base types.</summary>
	PropertyInfo? GetNamedProperty(Type type, string propertyName);

	/// <summary>Finds a property declared directly on <paramref name="type"/> by its CLR name.</summary>
	PropertyInfo? GetDeclaredProperty(Type type, string propertyName);

	/// <summary>Returns the OData name of the type (from <c>[DataContract]</c>/<c>[Table]</c> or the CLR name).</summary>
	string GetMappedName(Type type);

	/// <summary>Returns the OData name of the property with the given CLR name.</summary>
	string GetMappedName(Type type, string propertyName);

	/// <summary>Returns the OData name of the property.</summary>
	string GetMappedName(Type type, PropertyInfo propertyInfo);

	/// <summary>Returns whether <paramref name="type"/> is a compiler-generated anonymous type.</summary>
	bool IsAnonymousType(Type type);

	/// <summary>Returns whether <paramref name="type"/> is an enum.</summary>
	bool IsEnumType(Type type);

	/// <summary>Returns whether <paramref name="type"/> is a value type.</summary>
	bool IsValue(Type type);

	/// <summary>Returns whether a value of <paramref name="otherType"/> can be assigned to <paramref name="type"/>.</summary>
	bool IsTypeAssignableFrom(Type type, Type otherType);

	/// <summary>Attempts to convert <paramref name="value"/> to <paramref name="targetType"/>.</summary>
	bool TryConvert(object? value, Type targetType, out object? result);

	/// <summary>Converts <paramref name="value"/> to <paramref name="targetType"/>, throwing <see cref="FormatException"/> when impossible.</summary>
	object? Convert(object? value, Type targetType);

	/// <summary>Converts <paramref name="value"/> to <typeparamref name="T"/>, throwing <see cref="FormatException"/> when impossible.</summary>
	T? Convert<T>(object? value);

	/// <summary>Extracts the mapped properties of <paramref name="source"/> into a dictionary keyed by OData names.</summary>
	IDictionary<string, object?> ToDictionary(object source);

	/// <summary>Materializes a property dictionary into an instance of <paramref name="type"/>.</summary>
	object? ToObject(IDictionary<string, object?>? source, Type type, bool dynamicObject = false);

	/// <summary>Materializes a property dictionary into an instance of <typeparamref name="T"/>.</summary>
	T? ToObject<T>(IDictionary<string, object?>? source, bool dynamicObject = false) where T : class;
}
