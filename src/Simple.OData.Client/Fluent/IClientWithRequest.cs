namespace Simple.OData.Client;

/// <summary>A built request that can be inspected, sent, or paired with a response obtained elsewhere.</summary>
public interface IClientWithRequest<T>
	where T : class
{
	/// <summary>The built request.</summary>
	ODataRequest GetRequest();

	/// <summary>Pairs the request with a response obtained through another transport.</summary>
	IClientWithResponse<T> FromResponse(HttpResponseMessage responseMessage);

	/// <summary>Sends the request.</summary>
	Task<IClientWithResponse<T>> RunAsync(CancellationToken cancellationToken = default);
}
