namespace Simple.OData.Client;

public sealed partial class ODataBatch
{
	private readonly ODataClient? _client;

	/// <summary>Creates a batch that shares the session (metadata and connection) of <paramref name="client"/>.</summary>
	public ODataBatch(IODataClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		_client = client as ODataClient ?? throw new ArgumentException("The client must be an ODataClient.", nameof(client));
		Settings = _client.Settings;
	}

	/// <summary>Sends every added operation in one <c>$batch</c> request and assigns their results.</summary>
	/// <remarks>Operation failures surface as <see cref="WebRequestException"/> after successful operations have been assigned, unless <see cref="ODataClientSettings.ContinueOnError"/> is set.</remarks>
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		using var batchClient = _client is null ? new ODataClient(Settings, _entryMap) : new ODataClient(_client, _entryMap);
		await batchClient.ExecuteBatchAsync(_actions, _headers.Count == 0 ? null : _headers, cancellationToken).ConfigureAwait(false);
	}
}
