using System.Collections.Frozen;
using System.Reflection;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// Reads mapping attributes by name so that any of the common attribute families
/// (<c>System.ComponentModel.DataAnnotations</c>, <c>System.Runtime.Serialization</c>, <c>System.Text.Json</c>, Json.NET)
/// work without a reference to them.
/// </summary>
internal static class MemberMapping
{
	private static readonly FrozenSet<string> NotMappedAttributeNames = FrozenSet.ToFrozenSet(["NotMappedAttribute", "JsonIgnoreAttribute"], StringComparer.Ordinal);

	private static readonly FrozenSet<string> PropertyNameAttributeNames = FrozenSet.ToFrozenSet(
		["DataAttribute", "DataMemberAttribute", "ColumnAttribute", "JsonPropertyAttribute", "JsonPropertyNameAttribute"],
		StringComparer.Ordinal);

	private static readonly FrozenSet<string> TypeNameAttributeNames = FrozenSet.ToFrozenSet(["DataContractAttribute", "TableAttribute"], StringComparer.Ordinal);

	private static readonly string[] NameProperties = ["Name", "PropertyName"];

	public static bool IsNotMapped(MemberInfo member)
	{
		foreach (var attribute in member.GetCustomAttributes(inherit: true))
		{
			if (NotMappedAttributeNames.Contains(attribute.GetType().Name))
			{
				return true;
			}
		}

		return false;
	}

	public static string GetMappedName(MemberInfo member)
	{
		var attributes = member.GetCustomAttributes(inherit: true).ToArray();
		var mapping = attributes.FirstOrDefault(x => PropertyNameAttributeNames.Contains(x.GetType().Name))
			?? attributes.FirstOrDefault(x => x.GetType().GetProperty("PropertyName") is not null);

		return ReadName(mapping) ?? member.Name;
	}

	public static string GetMappedName(Type type)
	{
		var mapping = type.GetCustomAttributes(inherit: false).FirstOrDefault(x => TypeNameAttributeNames.Contains(x.GetType().Name));
		return ReadName(mapping) ?? type.Name;
	}

	private static string? ReadName(object? attribute)
	{
		if (attribute is null)
		{
			return null;
		}

		foreach (var propertyName in NameProperties)
		{
			var property = attribute.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
			if (property?.GetValue(attribute) is string { Length: > 0 } name)
			{
				return name;
			}
		}

		return null;
	}
}
