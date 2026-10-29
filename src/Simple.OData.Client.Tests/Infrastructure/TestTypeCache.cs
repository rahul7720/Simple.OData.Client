namespace Simple.OData.Client.Tests.Infrastructure;

internal static class TestTypeCache
{
	public static TypeCache Create(INameMatchResolver? resolver = null) =>
		new(new TypeConverter(), resolver ?? ODataNameMatchResolver.NotStrict);
}
