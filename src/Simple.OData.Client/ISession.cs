using Microsoft.OData.Edm;

namespace Simple.OData.Client;

/// <summary>
/// The state shared by all requests of one client: settings, the resolved metadata and the type cache.
/// </summary>
public interface ISession
{
	/// <summary>The client settings.</summary>
	ODataClientSettings Settings { get; }

	/// <summary>Whether the metadata has been resolved.</summary>
	bool IsInitialized { get; }

	/// <summary>Name-tolerant access to the service metadata. Available after initialization.</summary>
	IMetadata Metadata { get; }

	/// <summary>The Entity Data Model. Available after initialization.</summary>
	IEdmModel Model { get; }

	/// <summary>CLR type information and converters.</summary>
	ITypeCache TypeCache { get; }
}
