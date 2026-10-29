using System.Collections;
using System.Text;
using System.Text.Json;
using Microsoft.OData;
using Microsoft.Spatial;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Formats CLR values as OData V4 URI literals (<c>'text'</c>, <c>2013-01-01T00:00:00Z</c>, <c>NS.Color'Red'</c>, <c>[1,2]</c>, …).
/// </summary>
internal static class UriLiteralFormatter
{
	public static string Format(ISession session, object? value, bool escapeDataString)
	{
		ArgumentNullException.ThrowIfNull(session);
		var text = FormatCore(session, value);
		return escapeDataString ? UriText.Escape(text) : text;
	}

	private static string FormatCore(ISession session, object? value)
	{
		switch (value)
		{
			case ODataExpression expression:
				return expression.AsString(session);
			case Type type:
				return FormatCore(session, QualifiedTypeName(session, type));
			case DateTime dateTime:
				value = new DateTimeOffset(dateTime);
				break;
			case Enum enumValue:
				value = PrepareEnum(session, enumValue);
				break;
			case IEnumerable items and not string and not byte[] when value is not ODataCollectionValue:
				return FormatCollection(session, items);
		}

		var version = session.Settings.ProtocolVersion == ODataProtocolVersion.V401 ? ODataVersion.V401 : ODataVersion.V4;
		var model = session.IsInitialized ? session.Model : null;
		return ODataUriUtils.ConvertToUriLiteral(value, version, model);
	}

	/// <summary>Whether <paramref name="value"/> is a complex or entity value that a URL can only carry as a JSON parameter alias.</summary>
	public static bool IsStructuredValue(object? value) => value switch
	{
		null or string or byte[] or Enum or Type or Uri or ODataExpression or ODataValue or ISpatial => false,
		IDictionary<string, object?> or ODataEntry => true,
		IEnumerable => false,
		_ => value.GetType().IsClass,
	};

	/// <summary>Formats a complex or entity value as the JSON object literal used by parameter aliases.</summary>
	public static string FormatJson(ISession session, object value)
	{
		ArgumentNullException.ThrowIfNull(session);
		ArgumentNullException.ThrowIfNull(value);
		return JsonSerializer.Serialize(ToJsonValue(session, value));
	}

	private static object? ToJsonValue(ISession session, object? value) => value switch
	{
		null => null,
		ODataEntry entry => ToJsonObject(session, entry.AsDictionary(), null),
		IDictionary<string, object?> dictionary => ToJsonObject(session, dictionary, null),
		string or bool or byte or sbyte or short or int or long or decimal or double or float or Guid or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or byte[] => value,
		DateTime dateTime => new DateTimeOffset(dateTime),
		Enum enumValue => enumValue.ToString(),
		ODataEnumValue enumValue => enumValue.Value,
		IEnumerable items => items.Cast<object?>().Select(x => ToJsonValue(session, x)).ToList(),
		_ when value.GetType().IsClass => ToJsonObject(session, session.TypeCache.ToDictionary(value), TryGetQualifiedTypeName(session, value.GetType())),
		_ => value,
	};

	private static Dictionary<string, object?> ToJsonObject(ISession session, IDictionary<string, object?> source, string? typeName)
	{
		var result = new Dictionary<string, object?>(StringComparer.Ordinal);
		if (typeName is not null)
		{
			result["@odata.type"] = "#" + typeName;
		}

		foreach (var (name, value) in source)
		{
			result[name] = ToJsonValue(session, value);
		}

		return result;
	}

	private static string FormatCollection(ISession session, IEnumerable items)
	{
		var builder = new StringBuilder();
		builder.Append('[');
		var first = true;
		foreach (var item in items)
		{
			if (!first)
			{
				builder.Append(',');
			}

			builder.Append(FormatCore(session, item));
			first = false;
		}

		builder.Append(']');
		return builder.ToString();
	}

	private static object PrepareEnum(ISession session, Enum value)
	{
		var text = value.ToString();
		if (session.Settings.EnumPrefixFree)
		{
			return text;
		}

		var typeName = session.IsInitialized ? TryGetQualifiedTypeName(session, value.GetType()) : null;
		return typeName is null ? text : new ODataEnumValue(text, typeName);
	}

	private static string QualifiedTypeName(ISession session, Type type) =>
		TryGetQualifiedTypeName(session, type) ?? type.FullName ?? type.Name;

	private static string? TryGetQualifiedTypeName(ISession session, Type type)
	{
		try
		{
			return session.Metadata.GetQualifiedTypeName(session.TypeCache.GetMappedName(type));
		}
		catch (UnresolvableObjectException)
		{
			return null;
		}
	}
}
