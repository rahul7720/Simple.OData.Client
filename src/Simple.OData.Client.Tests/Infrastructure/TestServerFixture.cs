using Simple.OData.Client.Tests.TestServer;

namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>Hosts one in-process Northwind OData service for every test class in the "Server" collection.</summary>
public sealed class TestServerFixture : IAsyncLifetime
{
	public NorthwindTestServer Server { get; private set; } = null!;

	public async ValueTask InitializeAsync() => Server = await NorthwindTestServer.StartAsync();

	public async ValueTask DisposeAsync()
	{
		if (Server is not null)
		{
			await Server.DisposeAsync();
		}
	}
}

[CollectionDefinition("Server")]
public sealed class ServerCollection : ICollectionFixture<TestServerFixture>
{
}
