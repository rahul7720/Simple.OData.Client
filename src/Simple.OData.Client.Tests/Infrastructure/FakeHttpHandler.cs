using System.Net;
using System.Net.Http.Headers;

namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records requests and replays queued responses.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
	public List<HttpRequestMessage> Requests { get; } = [];

	public List<string?> RequestBodies { get; } = [];

	public string? LastRequestBody => RequestBodies.Count == 0 ? null : RequestBodies[^1];

	public HttpRequestMessage LastRequest => Requests[^1];

	public Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> Responses { get; } = new();

	/// <summary>
	/// Used when the queue is empty; when null an empty queue throws.
	/// </summary>
	public Func<HttpRequestMessage, HttpResponseMessage>? Fallback { get; set; }

	public FakeHttpHandler Enqueue(
		HttpStatusCode status,
		string? body = null,
		string contentType = "application/json;odata.metadata=minimal",
		IDictionary<string, string>? headers = null)
	{
		return Enqueue(_ => CreateResponse(status, body, contentType, headers));
	}

	public FakeHttpHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory)
	{
		Responses.Enqueue((request, _) => Task.FromResult(factory(request)));
		return this;
	}

	public FakeHttpHandler Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
	{
		Responses.Enqueue(factory);
		return this;
	}

	public FakeHttpHandler EnqueueMetadata(string resourceName)
	{
		var document = Resources.Read(resourceName);
		var contentType = resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "application/xml";
		return Enqueue(HttpStatusCode.OK, document, contentType, new Dictionary<string, string> { ["OData-Version"] = "4.0" });
	}

	public static HttpResponseMessage CreateResponse(
		HttpStatusCode status,
		string? body = null,
		string contentType = "application/json;odata.metadata=minimal",
		IDictionary<string, string>? headers = null)
	{
		var response = new HttpResponseMessage(status);
		if (body is not null)
		{
			response.Content = new StringContent(body);
			response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
		}

		if (headers is not null)
		{
			foreach (var header in headers)
			{
				if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value) && response.Content is not null)
				{
					response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}
			}
		}

		return response;
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		lock (Requests)
		{
			Requests.Add(request);
		}

		var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
		lock (RequestBodies)
		{
			RequestBodies.Add(body);
		}

		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? factory;
		lock (Responses)
		{
			factory = Responses.Count > 0 ? Responses.Dequeue() : null;
		}

		HttpResponseMessage response;
		if (factory is not null)
		{
			response = await factory(request, cancellationToken);
		}
		else if (Fallback is not null)
		{
			response = Fallback(request);
		}
		else
		{
			throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}");
		}

		response.RequestMessage = request;
		return response;
	}
}
