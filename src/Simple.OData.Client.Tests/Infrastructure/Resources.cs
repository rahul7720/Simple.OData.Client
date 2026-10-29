namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>
/// Reads embedded test resources (CSDL documents and canned payloads) by file name.
/// </summary>
internal static class Resources
{
	public static string Read(string name)
	{
		using var stream = Open(name);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	public static Stream Open(string name)
	{
		return typeof(Resources).Assembly.GetManifestResourceStream(name)
			?? throw new FileNotFoundException($"Embedded resource '{name}' was not found.", name);
	}
}
