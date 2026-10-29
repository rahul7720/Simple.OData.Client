using System.Linq.Expressions;
using System.Text;

namespace Simple.OData.Client;

internal static class Utils
{
	private static readonly char[] CollectionNameTerminators = ['?', '(', '/'];

	public static string StreamToString(Stream stream, bool disposeStream = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (!disposeStream && stream.CanSeek)
		{
			stream.Seek(0, SeekOrigin.Begin);
		}

		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: !disposeStream);
		return reader.ReadToEnd();
	}

	public static async Task<string> StreamToStringAsync(Stream stream, bool disposeStream, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (!disposeStream && stream.CanSeek)
		{
			stream.Seek(0, SeekOrigin.Begin);
		}

		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: !disposeStream);
		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}

	public static byte[] StreamToByteArray(Stream stream, bool disposeStream = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		if (!disposeStream && stream.CanSeek)
		{
			stream.Seek(0, SeekOrigin.Begin);
		}

		byte[] result;
		if (stream is MemoryStream memoryStream)
		{
			result = memoryStream.ToArray();
		}
		else
		{
			using var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			result = buffer.ToArray();
		}

		if (disposeStream)
		{
			stream.Dispose();
		}

		return result;
	}

	public static Stream StringToStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

	public static Stream ByteArrayToStream(byte[] bytes) => new MemoryStream(bytes);

	public static Exception NotSupportedExpression(Expression expression) =>
		new NotSupportedException($"Not supported expression of type {expression.GetType()} ({expression.NodeType}): {expression}");

	/// <summary>
	/// Combines a service root URI with a relative command path, keeping the root's path and carrying its query
	/// string over to the result. An absolute <paramref name="relativePath"/> is returned as is.
	/// </summary>
	public static Uri CreateAbsoluteUri(Uri baseUri, string? relativePath)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		relativePath ??= string.Empty;

		if (Uri.TryCreate(relativePath, UriKind.Absolute, out var absolute) && (absolute.Scheme is "http" or "https"))
		{
			return absolute;
		}

		var basePath = baseUri.AbsoluteUri;
		var baseQuery = baseUri.Query;
		if (!string.IsNullOrEmpty(baseQuery))
		{
			basePath = basePath[..^baseQuery.Length];
			baseQuery = baseQuery[1..];
		}

		if (!basePath.EndsWith('/'))
		{
			basePath += "/";
		}

		var uri = new Uri(basePath + relativePath);
		if (string.IsNullOrEmpty(baseQuery))
		{
			return uri;
		}

		var absoluteUri = uri.AbsoluteUri;
		var host = absoluteUri[..(absoluteUri.Length - uri.AbsolutePath.Length - uri.Query.Length)];
		var query = string.IsNullOrEmpty(uri.Query) ? "?" + baseQuery : uri.Query + "&" + baseQuery;
		return new Uri(host + uri.AbsolutePath + query);
	}

	/// <summary>
	/// Extracts the first path segment of a command (the entity set name), ignoring keys, navigation and query options.
	/// </summary>
	public static string ExtractCollectionName(string commandText)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (Uri.TryCreate(commandText, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https"))
		{
			var path = uri.LocalPath;
			var lastSlash = path.LastIndexOf('/');
			return lastSlash < 0 ? path : path[(lastSlash + 1)..];
		}

		var end = commandText.IndexOfAny(CollectionNameTerminators);
		return end < 0 ? commandText : commandText[..end];
	}

	public static bool IsSystemType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		var fullName = type.FullName ?? type.Name;
		return fullName.StartsWith("System.", StringComparison.Ordinal) || fullName.StartsWith("Microsoft.", StringComparison.Ordinal);
	}

	/// <summary>
	/// Returns the element whose name matches <paramref name="value"/>, preferring an exact match when the resolver
	/// is lenient.
	/// </summary>
	public static T? BestMatch<T>(this IEnumerable<T> collection, Func<T, string> fieldFunc, string value, INameMatchResolver matchResolver)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(fieldFunc);
		ArgumentNullException.ThrowIfNull(matchResolver);

		if (ReferenceEquals(matchResolver, ODataNameMatchResolver.Strict))
		{
			foreach (var item in collection)
			{
				if (matchResolver.IsMatch(fieldFunc(item), value))
				{
					return item;
				}
			}

			return null;
		}

		T? lenientMatch = null;
		foreach (var item in collection)
		{
			var name = fieldFunc(item);
			if (!matchResolver.IsMatch(name, value))
			{
				continue;
			}

			if (ODataNameMatchResolver.Strict.IsMatch(name, value))
			{
				return item;
			}

			lenientMatch ??= item;
		}

		return lenientMatch;
	}

	public static T? BestMatch<T>(this IEnumerable<T> collection, Func<T, bool> condition, Func<T, string> fieldFunc, string value, INameMatchResolver matchResolver)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(condition);
		return collection.Where(condition).BestMatch(fieldFunc, value, matchResolver);
	}

	/// <summary>
	/// Succeeds when <paramref name="namedKeyValues"/> contains exactly the key properties in <paramref name="keyNames"/>
	/// (matched through the resolver) and returns them in declared key order with their declared names.
	/// </summary>
	public static bool NamedKeyValuesMatchKeyNames(
		IDictionary<string, object?>? namedKeyValues,
		INameMatchResolver resolver,
		IReadOnlyList<string>? keyNames,
		out List<KeyValuePair<string, object?>>? matchingNamedKeyValues)
	{
		matchingNamedKeyValues = null;
		if (namedKeyValues is null || keyNames is null || keyNames.Count != namedKeyValues.Count)
		{
			return false;
		}

		var result = new List<KeyValuePair<string, object?>>(keyNames.Count);
		foreach (var keyName in keyNames)
		{
			var found = false;
			foreach (var pair in namedKeyValues)
			{
				if (resolver.IsMatch(pair.Key, keyName))
				{
					result.Add(new KeyValuePair<string, object?>(keyName, pair.Value));
					found = true;
					break;
				}
			}

			if (!found)
			{
				return false;
			}
		}

		matchingNamedKeyValues = result;
		return true;
	}

	/// <summary>
	/// Succeeds when at least one entry of <paramref name="namedValues"/> is a key property and returns those entries
	/// with their declared names.
	/// </summary>
	public static bool NamedKeyValuesContainKeyNames(
		IDictionary<string, object?>? namedValues,
		INameMatchResolver resolver,
		IReadOnlyList<string>? keyNames,
		out List<KeyValuePair<string, object?>>? matchingNamedKeyValues)
	{
		matchingNamedKeyValues = null;
		if (namedValues is null || keyNames is null)
		{
			return false;
		}

		List<KeyValuePair<string, object?>>? result = null;
		foreach (var pair in namedValues)
		{
			foreach (var keyName in keyNames)
			{
				if (resolver.IsMatch(keyName, pair.Key))
				{
					(result ??= []).Add(new KeyValuePair<string, object?>(keyName, pair.Value));
					break;
				}
			}
		}

		matchingNamedKeyValues = result;
		return result is not null;
	}
}
