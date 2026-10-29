using System.Collections.Concurrent;

namespace Simple.OData.Client;

internal static class StringExtensions
{
	private static readonly ConcurrentDictionary<string, string> HomogenizeCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Lower-cases a string and removes all whitespace and punctuation so that names such as
	/// <c>Order_Details</c> and <c>orderdetails</c> compare equal.
	/// </summary>
	public static string Homogenize(this string source)
	{
		ArgumentNullException.ThrowIfNull(source);
		return HomogenizeCache.GetOrAdd(source, static s => HomogenizeCore(s));
	}

	private static string HomogenizeCore(string source)
	{
		var lower = source.ToLowerInvariant();
		Span<char> buffer = lower.Length <= 256 ? stackalloc char[lower.Length] : new char[lower.Length];
		var length = 0;
		foreach (var c in lower)
		{
			if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
			{
				buffer[length++] = c;
			}
		}

		return length == lower.Length ? lower : new string(buffer[..length]);
	}

	public static string EnsureStartsWith(this string source, string value)
	{
		ArgumentNullException.ThrowIfNull(source);
		return source.StartsWith(value, StringComparison.Ordinal) ? source : value + source;
	}

	public static bool IsAllUpperCase(this string source)
	{
		foreach (var c in source)
		{
			if (char.IsLower(c))
			{
				return false;
			}
		}

		return true;
	}
}
