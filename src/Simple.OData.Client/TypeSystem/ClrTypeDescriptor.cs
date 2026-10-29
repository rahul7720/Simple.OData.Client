using System.Collections.Frozen;
using System.Reflection;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// Immutable, precomputed information about a CLR type used for mapping to and from OData payloads.
/// </summary>
internal sealed class ClrTypeDescriptor
{
	private readonly Lazy<IReadOnlyList<Type>> _derivedTypes;
	private readonly Lazy<Func<object>?> _activator;

	public ClrTypeDescriptor(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		Type = type;
		MappedName = MemberMapping.GetMappedName(type);
		IsAnonymous = type.IsAnonymousType();

		var allProperties = type.GetAllInstanceProperties();
		AllProperties = allProperties;
		DeclaredProperties = type.GetDeclaredInstanceProperties().ToArray();

		var mapped = new List<PropertyAccessor>(allProperties.Count);
		foreach (var property in allProperties)
		{
			if (!MemberMapping.IsNotMapped(property))
			{
				mapped.Add(new PropertyAccessor(property, MemberMapping.GetMappedName(property)));
			}
		}

		Properties = mapped;
		ByMappedName = mapped.ToFrozenDictionary(x => x.MappedName, StringComparer.Ordinal);
		ByClrName = mapped.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

		var byHomogenized = new Dictionary<string, PropertyAccessor>(StringComparer.Ordinal);
		foreach (var accessor in mapped)
		{
			byHomogenized.TryAdd(accessor.MappedName.Homogenize(), accessor);
		}

		ByHomogenizedMappedName = byHomogenized.ToFrozenDictionary(StringComparer.Ordinal);
		AnnotationsProperty = mapped.FirstOrDefault(x => x.PropertyType == typeof(ODataEntryAnnotations));

		_derivedTypes = new Lazy<IReadOnlyList<Type>>(type.GetDerivedTypesInAssembly, LazyThreadSafetyMode.ExecutionAndPublication);
		_activator = new Lazy<Func<object>?>(() => ExpressionActivator.GetDefaultActivator(type), LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public Type Type { get; }

	public string MappedName { get; }

	public bool IsAnonymous { get; }

	public bool IsEnum => Type.IsEnum;

	public bool IsValueType => Type.IsValueType;

	public bool IsGenericType => Type.IsGenericType;

	/// <summary>All instance properties including inherited and <c>[NotMapped]</c> ones.</summary>
	public IReadOnlyList<PropertyInfo> AllProperties { get; }

	/// <summary>Instance properties declared directly on the type.</summary>
	public IReadOnlyList<PropertyInfo> DeclaredProperties { get; }

	/// <summary>Mapped properties (participating in OData payloads).</summary>
	public IReadOnlyList<PropertyAccessor> Properties { get; }

	public FrozenDictionary<string, PropertyAccessor> ByMappedName { get; }

	public FrozenDictionary<string, PropertyAccessor> ByHomogenizedMappedName { get; }

	public FrozenDictionary<string, PropertyAccessor> ByClrName { get; }

	public PropertyAccessor? AnnotationsProperty { get; }

	public IReadOnlyList<Type> DerivedTypes => _derivedTypes.Value;

	public Func<object>? Activator => _activator.Value;

	/// <summary>Finds a mapped property by OData name: exact, then homogenized, then through the resolver.</summary>
	public PropertyAccessor? FindByMappedName(string name, INameMatchResolver resolver)
	{
		if (ByMappedName.TryGetValue(name, out var accessor))
		{
			return accessor;
		}

		if (ReferenceEquals(resolver, ODataNameMatchResolver.Strict))
		{
			return null;
		}

		if (ByHomogenizedMappedName.TryGetValue(name.Homogenize(), out accessor))
		{
			return accessor;
		}

		foreach (var candidate in Properties)
		{
			if (resolver.IsMatch(candidate.MappedName, name))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>Finds a mapped property by CLR name: exact, then through the resolver.</summary>
	public PropertyAccessor? FindByClrName(string name, INameMatchResolver resolver)
	{
		if (ByClrName.TryGetValue(name, out var accessor))
		{
			return accessor;
		}

		if (ReferenceEquals(resolver, ODataNameMatchResolver.Strict))
		{
			return null;
		}

		foreach (var candidate in Properties)
		{
			if (resolver.IsMatch(candidate.Name, name))
			{
				return candidate;
			}
		}

		return null;
	}
}
