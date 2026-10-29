using System.Globalization;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client.Http;

/// <summary>Composes the <c>Prefer</c> header from request-level and settings-level preferences.</summary>
internal static class PreferHeader
{
	/// <summary>Returns the header value, or <see langword="null"/> when no preference applies. Request preferences win over settings.</summary>
	public static string? Compose(ODataClientSettings settings, ODataRequest request)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(request);
		var preferences = new List<string>(request.Preferences);

		if (settings.MaxPageSize is { } maxPageSize)
		{
			Add(preferences, $"{HttpLiteral.MaxPageSize}={maxPageSize.ToString(CultureInfo.InvariantCulture)}");
		}

		if (settings.TrackChanges)
		{
			Add(preferences, HttpLiteral.TrackChanges);
		}

		if (settings.OmitNullValues)
		{
			Add(preferences, HttpLiteral.OmitNullValues);
		}

		if (!string.IsNullOrEmpty(settings.IncludeAnnotations))
		{
			Add(preferences, $"{HttpLiteral.IncludeAnnotations}=\"{settings.IncludeAnnotations}\"");
		}

		if (settings.ContinueOnError && request.CommandText == ODataLiteral.Batch)
		{
			Add(preferences, HttpLiteral.ContinueOnError);
		}

		if (settings.RespondAsync)
		{
			Add(preferences, HttpLiteral.RespondAsync);
		}

		return preferences.Count == 0 ? null : string.Join(", ", preferences);
	}

	private static void Add(List<string> preferences, string preference)
	{
		var name = Name(preference);
		foreach (var existing in preferences)
		{
			if (Name(existing).Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		preferences.Add(preference);
	}

	private static ReadOnlySpan<char> Name(string preference)
	{
		var separator = preference.IndexOf('=');
		return separator < 0 ? preference : preference.AsSpan(0, separator);
	}
}
