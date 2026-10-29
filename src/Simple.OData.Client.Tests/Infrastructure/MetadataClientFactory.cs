namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>
/// Creates settings and sessions bound to an embedded metadata document and a <see cref="FakeHttpHandler"/>.
/// </summary>
internal static class MetadataClientFactory
{
	public static Uri UniqueBaseUri(string? suffix = null) => new($"http://localhost/{Guid.NewGuid():N}/{suffix}");

	public static ODataClientSettings CreateSettings(string metadataResource, Action<ODataClientSettings>? configure = null, FakeHttpHandler? handler = null)
	{
		var settings = new ODataClientSettings(new HttpClient(handler ?? new FakeHttpHandler(), disposeHandler: false))
		{
			BaseUri = UniqueBaseUri(metadataResource + "/"),
			MetadataDocument = Resources.Read(metadataResource),
		};
		configure?.Invoke(settings);
		return settings;
	}

	public static Session CreateSession(string metadataResource, Action<ODataClientSettings>? configure = null, FakeHttpHandler? handler = null) =>
		new(CreateSettings(metadataResource, configure, handler));

	public static ODataClient CreateClient(string metadataResource, Action<ODataClientSettings>? configure = null, FakeHttpHandler? handler = null) =>
		new(CreateSettings(metadataResource, configure, handler));
}
