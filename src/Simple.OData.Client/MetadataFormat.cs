namespace Simple.OData.Client;

/// <summary>
/// The serialization format of a service metadata document.
/// </summary>
public enum MetadataFormat
{
	/// <summary>CSDL XML (<c>application/xml</c>), supported by every OData V4 service.</summary>
	Xml,

	/// <summary>CSDL JSON (<c>application/json</c>), introduced with OData 4.01.</summary>
	Json,
}
