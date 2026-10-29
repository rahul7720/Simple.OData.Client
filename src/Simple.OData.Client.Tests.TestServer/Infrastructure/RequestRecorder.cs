using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>A request the server received: the top-level <c>$batch</c> request (<see cref="IsBatch"/>) or any individual (sub-)request.</summary>
public sealed record RecordedRequest(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers, bool IsBatch)
{
	public bool HasHeader(string name, string value) => Headers.TryGetValue(name, out var actual) && actual == value;
}

/// <summary>Records every request that reaches the host so tests can assert on headers and request shapes.</summary>
public sealed class RequestRecorder
{
	private readonly ConcurrentQueue<RecordedRequest> _requests = new();

	public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

	public void Clear() => _requests.Clear();

	/// <summary>Registers the recording middleware: batch envelopes before OData batching, everything else (including batch sub-requests) after it.</summary>
	public static void Use(IApplicationBuilder app, Func<IApplicationBuilder, IApplicationBuilder> useBatching)
	{
		ArgumentNullException.ThrowIfNull(app);
		ArgumentNullException.ThrowIfNull(useBatching);
		var recorder = app.ApplicationServices.GetService(typeof(RequestRecorder)) as RequestRecorder ?? throw new InvalidOperationException("RequestRecorder is not registered.");
		app.Use(async (context, next) =>
		{
			if (context.Request.Path.Value?.EndsWith("/$batch", StringComparison.Ordinal) == true)
			{
				recorder.Record(context, isBatch: true);
			}

			await next(context).ConfigureAwait(false);
		});
		useBatching(app);
		app.Use(async (context, next) =>
		{
			recorder.Record(context, isBatch: false);
			await next(context).ConfigureAwait(false);
		});
	}

	private void Record(HttpContext context, bool isBatch)
	{
		var headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
		_requests.Enqueue(new RecordedRequest(context.Request.Method, context.Request.Path.Value ?? string.Empty, context.Request.QueryString.Value ?? string.Empty, headers, isBatch));
	}
}
