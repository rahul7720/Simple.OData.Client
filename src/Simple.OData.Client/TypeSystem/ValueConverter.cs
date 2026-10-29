using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// Converts scalar values between the CLR types produced by the OData reader and the types declared on user models.
/// </summary>
internal static class ValueConverter
{
	private static readonly ConcurrentDictionary<(Type From, Type To), MethodInfo?> ConversionOperators = new();

	public static bool TryConvert(ITypeConverter converters, object? value, Type targetType, out object? result)
	{
		try
		{
			result = ConvertCore(converters, value, targetType);
			return true;
		}
		catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException or NotSupportedException or InvalidOperationException or TargetInvocationException)
		{
			result = null;
			return false;
		}
	}

	private static object? ConvertCore(ITypeConverter converters, object? value, Type targetType)
	{
		if (value is null)
		{
			return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
				? Activator.CreateInstance(targetType)
				: null;
		}

		if (targetType.IsInstanceOfType(value))
		{
			return value;
		}

		if (Nullable.GetUnderlyingType(targetType) is { } underlying)
		{
			return ConvertCore(converters, value, underlying);
		}

		if (value is Stream stream)
		{
			if (targetType == typeof(byte[]))
			{
				return Utils.StreamToByteArray(stream);
			}

			if (targetType == typeof(string))
			{
				return Utils.StreamToString(stream);
			}
		}

		if (targetType == typeof(string))
		{
			return value is byte[] bytes ? Convert.ToBase64String(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		if (targetType == typeof(byte[]) && value is string base64)
		{
			return Convert.FromBase64String(base64);
		}

		if (targetType.IsEnum)
		{
			return ConvertToEnum(value, targetType);
		}

		if (TryConvertTemporal(value, targetType, out var temporal))
		{
			return temporal;
		}

		if (targetType == typeof(Guid) && value is string guidText)
		{
			return Guid.Parse(guidText, CultureInfo.InvariantCulture);
		}

		if (converters.HasObjectConverter(targetType))
		{
			return converters.Convert(value, targetType);
		}

		var conversionOperator = FindConversionOperator(value.GetType(), targetType);
		if (conversionOperator is not null)
		{
			return conversionOperator.Invoke(null, [value]);
		}

		var descriptor = TypeDescriptor.GetConverter(targetType);
		if (descriptor.CanConvertFrom(value.GetType()))
		{
			return descriptor.ConvertFrom(null, CultureInfo.InvariantCulture, value);
		}

		if (descriptor.CanConvertTo(targetType))
		{
			return descriptor.ConvertTo(null, CultureInfo.InvariantCulture, value, targetType);
		}

		return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
	}

	private static object ConvertToEnum(object value, Type enumType)
	{
		switch (value)
		{
			case string text:
				return Enum.Parse(enumType, text, ignoreCase: true);
			case Microsoft.OData.ODataEnumValue enumValue:
				return Enum.Parse(enumType, enumValue.Value, ignoreCase: true);
			default:
				var underlyingValue = Convert.ChangeType(value, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
				return Enum.ToObject(enumType, underlyingValue!);
		}
	}

	private static bool TryConvertTemporal(object value, Type targetType, out object? result)
	{
		result = null;
		if (targetType == typeof(DateTime))
		{
			result = value switch
			{
				DateTimeOffset dto => dto.DateTime,
				DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
				string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				_ => null,
			};
		}
		else if (targetType == typeof(DateTimeOffset))
		{
			result = value switch
			{
				DateTime dt => new DateTimeOffset(dt),
				DateOnly dateOnly => new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue)),
				string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				_ => null,
			};
		}
		else if (targetType == typeof(DateOnly))
		{
			result = value switch
			{
				DateTime dt => DateOnly.FromDateTime(dt),
				DateTimeOffset dto => DateOnly.FromDateTime(dto.DateTime),
				string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
				_ => null,
			};
		}
		else if (targetType == typeof(TimeSpan))
		{
			result = value switch
			{
				TimeOnly timeOnly => timeOnly.ToTimeSpan(),
				string text => text.StartsWith('P') || text.StartsWith("-P", StringComparison.Ordinal) ? XmlConvert.ToTimeSpan(text) : TimeSpan.Parse(text, CultureInfo.InvariantCulture),
				_ => null,
			};
		}
		else if (targetType == typeof(TimeOnly))
		{
			result = value switch
			{
				TimeSpan span => TimeOnly.FromTimeSpan(span),
				string text => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
				_ => null,
			};
		}

		return result is not null;
	}

	private static MethodInfo? FindConversionOperator(Type from, Type to) =>
		ConversionOperators.GetOrAdd((from, to), static key =>
		{
			static bool Matches(MethodInfo m, Type from, Type to) =>
				m.ReturnType == to && m.GetParameters() is [{ ParameterType: var p }] && p.IsAssignableFrom(from);

			var candidates = key.From.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.Concat(key.To.GetMethods(BindingFlags.Public | BindingFlags.Static))
				.Where(m => m.Name is "op_Implicit" or "op_Explicit");

			return candidates.FirstOrDefault(m => Matches(m, key.From, key.To));
		});
}
