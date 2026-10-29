namespace Simple.OData.Client;

/// <summary>Percent-encodes URL components the way OData services expect them.</summary>
internal static class UriText
{
	/// <summary>
	/// Escapes <paramref name="text"/> for use in a URL path or query, keeping apostrophes literal: they are legal sub-delimiters,
	/// OData string literals are written with them, and ASP.NET Core OData does not decode <c>%27</c> in batch sub-request paths.
	/// </summary>
	public static string Escape(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return Uri.EscapeDataString(text).Replace("%27", "'", StringComparison.Ordinal);
	}
}
