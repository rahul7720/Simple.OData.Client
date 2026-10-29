using System.Collections;
using System.Text;
using Microsoft.OData;

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
		return escapeDataString ? Uri.EscapeDataString(text) : text;
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
