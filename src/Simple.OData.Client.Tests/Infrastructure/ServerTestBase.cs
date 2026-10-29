using Simple.OData.Client.Tests.TestServer;
using Simple.OData.Client.Tests.TestServer.Infrastructure;

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
		Server.ClearRequests();
	}

	protected NorthwindTestServer Server { get; }

	/// <summary>The requests the server received during this test, batch sub-requests included.</summary>
	protected IReadOnlyList<RecordedRequest> Requests => Server.Requests;

	/// <summary>The recorded request with the given method whose path ends with <paramref name="pathSuffix"/>.</summary>
	protected RecordedRequest Request(string method, string pathSuffix) =>
		Requests.LastOrDefault(x => !x.IsBatch && x.Method == method && x.Path.EndsWith(pathSuffix, StringComparison.Ordinal))
		?? throw new InvalidOperationException($"No {method} request ending with '{pathSuffix}' was recorded. Recorded: {string.Join(", ", Requests.Select(x => x.Method + " " + x.Path))}");

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
