namespace Simple.OData.Client;

/// <summary>
/// The wire format of <c>$batch</c> requests.
/// </summary>
public enum BatchFormat
{
	/// <summary>Multipart MIME batch (<c>multipart/mixed</c>), supported by every OData V4 service.</summary>
	Multipart,

	/// <summary>JSON batch (<c>application/json</c>), introduced with OData 4.01.</summary>
	Json,
}
