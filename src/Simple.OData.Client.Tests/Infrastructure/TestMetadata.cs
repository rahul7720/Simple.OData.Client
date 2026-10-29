using Microsoft.OData.Edm;
using Simple.OData.Client.Metadata;

namespace Simple.OData.Client.Tests.Infrastructure;

internal static class TestMetadata
{
	public static IEdmModel Model(string resourceName) => MetadataLoader.Parse(Resources.Read(resourceName));

	public static IMetadata Resolver(string resourceName, INameMatchResolver? resolver = null, bool ignoreUnmappedProperties = false, bool unqualifiedNameCall = false) =>
		new MetadataResolver(Model(resourceName), resolver ?? ODataNameMatchResolver.NotStrict, ignoreUnmappedProperties, unqualifiedNameCall);

	public static IMetadata ResolverFromCsdl(string csdl, INameMatchResolver? resolver = null) =>
		new MetadataResolver(MetadataLoader.Parse(csdl), resolver ?? ODataNameMatchResolver.NotStrict, false, false);
}
