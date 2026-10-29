using System.Net;
using System.Net.Http.Headers;
using Microsoft.OData;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client.Http;

/// <summary>Follows a <c>202 Accepted</c> status monitor until the asynchronous operation completes.</summary>
internal sealed class AsyncMonitor(Session session)
{
	public async Task<HttpResponseMessage> WaitForCompletionAsync(
		HttpResponseMessage accepted,
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(accepted);
		ArgumentNullException.ThrowIfNull(send);
		var current = accepted;
		while (current.StatusCode == HttpStatusCode.Accepted && current.Headers.Location is { } location)
		{
			var delay = RetryAfter(current) ?? session.Settings.AsyncPollingInterval;
			var requestMessage = current.RequestMessage;
			current.Dispose();
			session.Trace("Operation pending; polling {0} in {1}", location, delay);
			await Task.Delay(delay, session.Settings.TimeProvider, cancellationToken).ConfigureAwait(false);

			var poll = new HttpRequestMessage(HttpMethod.Get, location);
			if (requestMessage is not null)
			{
				CopyHeaders(requestMessage.Headers, poll.Headers);
			}

			current = await send(poll, cancellationToken).ConfigureAwait(false);
		}

		if (current.StatusCode == HttpStatusCode.OK && string.Equals(current.Content?.Headers.ContentType?.MediaType, HttpLiteral.ApplicationHttp, StringComparison.OrdinalIgnoreCase))
		{
			return await UnwrapAsync(current, cancellationToken).ConfigureAwait(false);
		}

		return current;
	}

	private static TimeSpan? RetryAfter(HttpResponseMessage response)
	{
		var retryAfter = response.Headers.RetryAfter;
		if (retryAfter?.Delta is { } delta)
		{
			return delta;
		}

		if (retryAfter?.Date is { } date)
		{
			var wait = date - DateTimeOffset.UtcNow;
			return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
		}

		return null;
	}

	/// <summary>Reads the <c>application/http</c> envelope of a completed asynchronous response into a plain response.</summary>
	private async Task<HttpResponseMessage> UnwrapAsync(HttpResponseMessage envelope, CancellationToken cancellationToken)
	{
		using var message = new ODataResponseMessage(envelope);
		var settings = ODataLibSettings.CreateReaderSettings(session);
		using var messageReader = new ODataMessageReader(message, settings, session.Model);
		var asyncReader = await messageReader.CreateODataAsynchronousReaderAsync().ConfigureAwait(false);
		var inner = await asyncReader.CreateResponseMessageAsync().ConfigureAwait(false);

		var body = new MemoryStream();
		await (await inner.GetStreamAsync(cancellationToken).ConfigureAwait(false)).CopyToAsync(body, cancellationToken).ConfigureAwait(false);
		body.Position = 0;

		var result = new HttpResponseMessage((HttpStatusCode)inner.StatusCode)
		{
			RequestMessage = envelope.RequestMessage,
			Content = new StreamContent(body),
		};
		foreach (var (name, value) in inner.Headers)
		{
			if (name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
			{
				result.Content.Headers.TryAddWithoutValidation(name, value);
			}
			else
			{
				result.Headers.TryAddWithoutValidation(name, value);
			}
		}

		envelope.Dispose();
		return result;
	}

	private static void CopyHeaders(HttpRequestHeaders source, HttpRequestHeaders target)
	{
		foreach (var header in source)
		{
			if (header.Key.StartsWith("OData-", StringComparison.OrdinalIgnoreCase) || header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || header.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase))
			{
				target.TryAddWithoutValidation(header.Key, header.Value);
			}
		}
	}
}
