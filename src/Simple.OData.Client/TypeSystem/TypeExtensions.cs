using System.Reflection;
using System.Runtime.CompilerServices;

namespace Simple.OData.Client.TypeSystem;

internal static class TypeExtensions
{
	private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	/// <summary>Instance properties declared directly on the type, excluding indexers and explicit interface implementations.</summary>
	public static IEnumerable<PropertyInfo> GetDeclaredInstanceProperties(this Type type) =>
		type.GetProperties(InstanceMembers)
			.Where(x => x.GetIndexParameters().Length == 0 && !x.Name.Contains('.'));

	/// <summary>Instance properties of the type and its base types; a derived declaration hides a base one with the same name.</summary>
	public static List<PropertyInfo> GetAllInstanceProperties(this Type type)
	{
		var result = new List<PropertyInfo>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
		{
			foreach (var property in current.GetDeclaredInstanceProperties())
			{
				if (seen.Add(property.Name))
				{
					result.Add(property);
				}
			}
		}

		return result;
	}

	public static PropertyInfo? FindInstanceProperty(this Type type, string name)
	{
		for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
		{
			var property = current.GetProperty(name, InstanceMembers);
			if (property is not null)
			{
				return property;
			}
		}

		return null;
	}

	public static bool IsAnonymousType(this Type type) =>
		type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
		&& type.IsGenericType
		&& type.Name.Contains("AnonymousType", StringComparison.Ordinal)
		&& (type.Name.StartsWith("<>", StringComparison.Ordinal) || type.Name.StartsWith("VB$", StringComparison.Ordinal))
		&& (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;

	public static IReadOnlyList<Type> GetDerivedTypesInAssembly(this Type type)
	{
		Type[] candidates;
		try
		{
			candidates = type.Assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			candidates = ex.Types.Where(x => x is not null).ToArray()!;
		}

		return candidates.Where(x => x.IsSubclassOf(type) && !x.IsAbstract).ToArray();
	}

	public static bool IsEnumerableType(this Type type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? elementType)
	{
		elementType = null;
		if (type == typeof(string))
		{
			return false;
		}

		if (type.IsArray)
		{
			elementType = type.GetElementType();
			return elementType is not null;
		}

		if (!type.IsGenericType)
		{
			return false;
		}

		var definition = type.GetGenericTypeDefinition();
		if (definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>))
		{
			return false;
		}

		var arguments = type.GetGenericArguments();
		if (arguments.Length != 1 || !typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
		{
			return false;
		}

		elementType = arguments[0];
		return true;
	}
}
