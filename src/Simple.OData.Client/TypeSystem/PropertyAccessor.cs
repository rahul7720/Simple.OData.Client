using System.Linq.Expressions;
using System.Reflection;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// A property with compiled get/set delegates and its OData (mapped) name.
/// </summary>
internal sealed class PropertyAccessor
{
	private readonly Func<object, object?> _getter;
	private readonly Action<object, object?>? _setter;

	public PropertyAccessor(PropertyInfo property, string mappedName)
	{
		ArgumentNullException.ThrowIfNull(property);
		ArgumentException.ThrowIfNullOrEmpty(mappedName);

		Property = property;
		MappedName = mappedName;
		_getter = BuildGetter(property);
		_setter = property.SetMethod is null || property.DeclaringType is null ? null : BuildSetter(property);
	}

	public PropertyInfo Property { get; }

	public string Name => Property.Name;

	public string MappedName { get; }

	public Type PropertyType => Property.PropertyType;

	public bool CanWrite => _setter is not null;

	public object? GetValue(object instance) => _getter(instance);

	public void SetValue(object instance, object? value)
	{
		if (_setter is null)
		{
			throw new InvalidOperationException($"Property {Property.DeclaringType?.Name}.{Property.Name} has no setter.");
		}

		_setter(instance, value);
	}

	private static Func<object, object?> BuildGetter(PropertyInfo property)
	{
		if (property.GetMethod is null)
		{
			return _ => throw new InvalidOperationException($"Property {property.DeclaringType?.Name}.{property.Name} has no getter.");
		}

		var instance = Expression.Parameter(typeof(object), "instance");
		var typed = Expression.Convert(instance, property.DeclaringType!);
		var access = Expression.Property(typed, property);
		var body = Expression.Convert(access, typeof(object));
		return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
	}

	private static Action<object, object?> BuildSetter(PropertyInfo property)
	{
		var instance = Expression.Parameter(typeof(object), "instance");
		var value = Expression.Parameter(typeof(object), "value");
		var typed = Expression.Convert(instance, property.DeclaringType!);
		var typedValue = Expression.Convert(value, property.PropertyType);
		var assign = Expression.Assign(Expression.Property(typed, property), typedValue);
		return Expression.Lambda<Action<object, object?>>(assign, instance, value).Compile();
	}
}
