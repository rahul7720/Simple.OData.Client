namespace Simple.OData.Client;

/// <summary>A built request that can be inspected, sent, or paired with a response obtained elsewhere.</summary>
internal sealed class ClientWithRequest<T>(ODataRequest request, ODataClient client, EntityCollection? entityCollection) : IClientWithRequest<T>
	where T : class
{
	public ODataRequest GetRequest() => request;

	public IClientWithResponse<T> FromResponse(HttpResponseMessage responseMessage) => new ClientWithResponse<T>(client, request, responseMessage, entityCollection);

	public async Task<IClientWithResponse<T>> RunAsync(CancellationToken cancellationToken = default)
	{
		var response = await client.Runner.ExecuteRequestAsync(request, cancellationToken).ConfigureAwait(false);
		return new ClientWithResponse<T>(client, request, response, entityCollection);
	}
}
