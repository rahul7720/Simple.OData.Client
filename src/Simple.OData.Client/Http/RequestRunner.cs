using System.Net;
using System.Net.Http.Headers;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Serialization;

namespace Simple.OData.Client.Http;

/// <summary>Sends <see cref="ODataRequest"/>s: protocol headers, preferences, ETags, hooks, concurrency gate, async monitoring and error translation.</summary>
internal sealed class RequestRunner(Session session)
{
	private readonly SemaphoreSlim? _gate = session.Settings.MaxConcurrentRequests > 0 ? new(session.Settings.MaxConcurrentRequests, session.Settings.MaxConcurrentRequests) : null;
	private readonly AsyncMonitor _monitor = new(session);

	private ODataClientSettings Settings => session.Settings;

	public async Task<HttpResponseMessage> ExecuteRequestAsync(ODataRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (_gate is not null)
		{
			await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			var message = request.PrepareForSend();
			PrepareHeaders(request, message);
			await RunBeforeRequestAsync(message, cancellationToken).ConfigureAwait(false);
			await TraceRequestAsync(message, cancellationToken).ConfigureAwait(false);

			var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
			await TraceResponseAsync(response, cancellationToken).ConfigureAwait(false);
			await RunAfterResponseAsync(response, cancellationToken).ConfigureAwait(false);

			if (response.StatusCode == HttpStatusCode.Accepted && response.Headers.Location is not null)
			{
				response = await _monitor.WaitForCompletionAsync(response, SendAsync, cancellationToken).ConfigureAwait(false);
			}

			if (!response.IsSuccessStatusCode)
			{
				throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);
			}

			return response;
		}
		finally
		{
			_gate?.Release();
		}
	}

	private Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken) =>
		Settings.RequestExecutor is { } executor
			? executor(message)
			: session.HttpConnection.HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

	private void PrepareHeaders(ODataRequest request, HttpRequestMessage message)
	{
		var headers = message.Headers;
		var version = Settings.ProtocolVersion == ODataProtocolVersion.V401 ? "4.01" : "4.0";
		if (!headers.Contains(HttpLiteral.ODataVersion))
		{
			headers.TryAddWithoutValidation(HttpLiteral.ODataVersion, version);
		}

		if (!headers.Contains(HttpLiteral.ODataMaxVersion))
		{
			headers.TryAddWithoutValidation(HttpLiteral.ODataMaxVersion, "4.01");
		}

		if (request.Accept is { } accept && headers.Accept.Count == 0)
		{
			foreach (var mediaType in accept)
			{
				headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(mediaType == HttpLiteral.ApplicationJson ? JsonAccept() : mediaType));
			}
		}

		var existing = headers.TryGetValues(HttpLiteral.Prefer, out var values) ? values.ToArray() : null;
		var prefer = PreferHeader.Compose(Settings, request, existing);
		headers.Remove(HttpLiteral.Prefer);
		if (prefer is not null)
		{
			headers.TryAddWithoutValidation(HttpLiteral.Prefer, prefer);
		}

		if (request.IfMatch is null && request.CheckOptimisticConcurrency && request.Method is RestVerbs.Put or RestVerbs.Patch or RestVerbs.Delete && headers.IfMatch.Count == 0)
		{
			headers.IfMatch.Add(EntityTagHeaderValue.Any);
		}
	}

	private string JsonAccept()
	{
		var metadata = Settings.MetadataLevel switch
		{
			ODataMetadataLevel.Full => "full",
			ODataMetadataLevel.None => "none",
			_ => "minimal",
		};
		var accept = $"{HttpLiteral.ApplicationJson};odata.metadata={metadata}";
		return Settings.Ieee754Compatible ? accept + ";IEEE754Compatible=true" : accept;
	}

	private async Task RunBeforeRequestAsync(HttpRequestMessage message, CancellationToken cancellationToken)
	{
		Settings.BeforeRequest?.Invoke(message);
		if (Settings.BeforeRequestAsync is { } before)
		{
			await before(message, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task RunAfterResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		Settings.AfterResponse?.Invoke(response);
		if (Settings.AfterResponseAsync is { } after)
		{
			await after(response, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task TraceRequestAsync(HttpRequestMessage message, CancellationToken cancellationToken)
	{
		if (Settings.OnTrace is null)
		{
			return;
		}

		session.Trace("{0} request: {1}", message.Method, message.RequestUri?.AbsoluteUri);
		if (message.Content is not null && session.IsTracing(ODataTrace.RequestContent))
		{
			var content = await message.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			session.Trace("Request content:{0}{1}", Environment.NewLine, content);
		}
	}

	private async Task TraceResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (Settings.OnTrace is null)
		{
			return;
		}

		session.Trace("Request completed: {0}", response.StatusCode);
		if (response.Content is not null && session.IsTracing(ODataTrace.ResponseContent))
		{
			await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
			var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			session.Trace("Response content:{0}{1}", Environment.NewLine, content);
		}
	}

	private async Task<WebRequestException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		ODataErrorDetails? error = null;
		if (response.Content is not null)
		{
			await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
			if (session.IsInitialized)
			{
				error = await new ResponseReader(session).TryReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
			}
		}

		return await WebRequestException.CreateFromResponseMessageAsync(response, Settings.WebRequestExceptionMessageSource, error, cancellationToken).ConfigureAwait(false);
	}
}
