using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>An HTTP client that calls the test server itself, used to materialize deferred (<c>respond-async</c>) results.</summary>
public sealed class SelfClient
{
	public HttpClient Client { get; set; } = null!;
}

/// <summary>A pending asynchronous request: the path to execute and how often the monitor was polled.</summary>
public sealed class MonitorJob(string path)
{
	public string Path { get; } = path;

	public int Polls { get; set; }
}

/// <summary>Stores the jobs behind <c>/odata/$monitor/{id}</c>.</summary>
public sealed class AsyncMonitorStore
{
	private readonly ConcurrentDictionary<string, MonitorJob> _jobs = new(StringComparer.Ordinal);

	public string Create(string path)
	{
		var id = Guid.NewGuid().ToString("N");
		_jobs[id] = new MonitorJob(path);
		return id;
	}

	public bool TryGet(string id, out MonitorJob job) => _jobs.TryGetValue(id, out job!);

	public static string LocationFor(string id) => $"http://localhost/odata/$monitor/{id}";
}

/// <summary>The status monitor resource: answers 202 on the first poll, then 200 with the wrapped <c>application/http</c> result.</summary>
public static class AsyncMonitorEndpoint
{
	public static void Map(IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/odata/$monitor/{id}", async (string id, HttpContext context, AsyncMonitorStore store, SelfClient self) =>
		{
			if (!store.TryGet(id, out var job))
			{
				return Results.NotFound();
			}

			if (job.Polls++ == 0)
			{
				context.Response.Headers.Location = AsyncMonitorStore.LocationFor(id);
				context.Response.Headers.RetryAfter = "0";
				return Results.StatusCode(StatusCodes.Status202Accepted);
			}

			using var request = new HttpRequestMessage(HttpMethod.Get, job.Path);
			request.Headers.TryAddWithoutValidation("Accept", "application/json;odata.metadata=minimal");
			request.Headers.TryAddWithoutValidation("OData-Version", "4.0");
			using var inner = await self.Client.SendAsync(request).ConfigureAwait(false);
			var body = await inner.Content.ReadAsStringAsync().ConfigureAwait(false);
			var status = ((int)inner.StatusCode).ToString(CultureInfo.InvariantCulture);
			var wrapped = $"HTTP/1.1 {status} {inner.ReasonPhrase}\r\nContent-Type: {inner.Content.Headers.ContentType}\r\nOData-Version: 4.0\r\n\r\n{body}";
			return Results.Text(wrapped, "application/http");
		});
	}
}
