namespace Simple.OData.Client;

/// <summary>
/// Selects the information written through <see cref="ODataClientSettings.OnTrace"/>.
/// </summary>
[Flags]
public enum ODataTrace
{
	/// <summary>Only request lines and status codes.</summary>
	None = 0,

	/// <summary>Also trace request bodies.</summary>
	RequestContent = 0x1,

	/// <summary>Also trace response bodies.</summary>
	ResponseContent = 0x2,

	/// <summary>Trace everything.</summary>
	All = 0xFF,
}
