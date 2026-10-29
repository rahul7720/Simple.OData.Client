using Simple.OData.Client.Tests.TestServer;

namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>Base class of the end-to-end tests: resets the server data before each test and creates clients bound to the test server.</summary>
[Collection("Server")]
public abstract class ServerTestBase
{
	protected ServerTestBase(TestServerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		Server = fixture.Server;
		Server.ResetData();
	}

	protected NorthwindTestServer Server { get; }

	protected static Uri BaseUri => NorthwindTestServer.BaseAddress;

	/// <summary>Creates a client that sends requests straight into the test server.</summary>
	protected ODataClient CreateClient(Action<ODataClientSettings>? configure = null)
	{
		var settings = new ODataClientSettings(Server.CreateClient())
		{
			IgnoreResourceNotFoundException = true,
		};
		configure?.Invoke(settings);
		return new ODataClient(settings);
	}

	/// <summary>Creates a plain <see cref="HttpClient"/> whose base address is the service root.</summary>
	protected HttpClient CreateHttpClient() => Server.CreateClient();
}
