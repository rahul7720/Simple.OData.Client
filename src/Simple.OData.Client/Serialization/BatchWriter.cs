using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.OData;
using Simple.OData.Client.Commands;

namespace Simple.OData.Client.Serialization;

/// <summary>Accumulates operations into an OData <c>$batch</c> payload (multipart or JSON).</summary>
internal sealed class BatchWriter(Session session, ConcurrentDictionary<object, IDictionary<string, object?>> batchEntries) : IDisposable
{
	private readonly Dictionary<IDictionary<string, object?>, string> _contentIdMap = new(ReferenceEqualityComparer.Instance);
	private ODataRequestMessage? _requestMessage;
	private ODataMessageWriter? _messageWriter;
	private ODataBatchWriter? _batchWriter;
	private bool _pendingChangeSet;

	public ConcurrentDictionary<object, IDictionary<string, object?>> BatchEntries => batchEntries;

	public int LastOperationId { get; private set; }

	public bool HasOperations { get; private set; }

	public async Task StartBatchAsync()
	{
		_requestMessage = new ODataRequestMessage { Url = session.BaseUri, Method = RestVerbs.Post };
		var settings = new ODataMessageWriterSettings { BaseUri = session.BaseUri, Version = ODataLibSettings.Version(session), EnableMessageStreamDisposal = false };
		if (IsJson)
		{
			// JSON batch is an OData 4.01 format
			settings.Version = ODataVersion.V401;
			settings.SetContentType(ODataFormat.Json);
		}

		_messageWriter = new ODataMessageWriter(_requestMessage, settings);
		_batchWriter = await _messageWriter.CreateODataBatchWriterAsync().ConfigureAwait(false);
		await _batchWriter.WriteStartBatchAsync().ConfigureAwait(false);
		HasOperations = true;
	}

	public async Task<HttpRequestMessage> EndBatchAsync()
	{
		if (_batchWriter is null || _requestMessage is null)
		{
			throw new InvalidOperationException("The batch has not been started.");
		}

		if (_pendingChangeSet)
		{
			await _batchWriter.WriteEndChangesetAsync().ConfigureAwait(false);
			_pendingChangeSet = false;
		}

		await _batchWriter.WriteEndBatchAsync().ConfigureAwait(false);
		_messageWriter?.Dispose();
		_messageWriter = null;
		var stream = await _requestMessage.GetStreamAsync().ConfigureAwait(false);
		stream.Position = 0;
		var content = new StreamContent(stream);
		content.Headers.ContentType = MediaTypeHeaderValue.Parse(_requestMessage.GetHeader(HttpLiteral.ContentType) ?? HttpLiteral.MultipartMixed);
		content.Headers.ContentLength = stream.Length;
		var request = new HttpRequestMessage(HttpMethod.Post, Utils.CreateAbsoluteUri(session.BaseUri, ODataLiteral.Batch)) { Content = content };
		if (IsJson)
		{
			request.Headers.TryAddWithoutValidation(HttpLiteral.ODataVersion, "4.01");
		}

		return request;
	}

	/// <summary>
	/// Runs the batch actions (each of which writes its operation through a batch-mode <see cref="RequestWriter"/>),
	/// records for every action the index of the operation response it produced (or -1), and returns the
	/// <c>$batch</c> request, or <see langword="null"/> when no action produced an operation.
	/// </summary>
	public async Task<ODataRequest?> CreateBatchRequestAsync(IReadOnlyList<Func<Task>> actions, List<int> responseIndexes, IDictionary<string, string>? headers)
	{
		ArgumentNullException.ThrowIfNull(actions);
		ArgumentNullException.ThrowIfNull(responseIndexes);
		var lastOperationId = 0;
		foreach (var action in actions)
		{
			await action().ConfigureAwait(false);
			var responseIndex = -1;
			if (LastOperationId > lastOperationId)
			{
				lastOperationId = LastOperationId;
				responseIndex = lastOperationId - 1;
			}

			responseIndexes.Add(responseIndex);
		}

		if (!HasOperations)
		{
			return null;
		}

		var message = await EndBatchAsync().ConfigureAwait(false);
		if (headers is not null)
		{
			foreach (var (name, value) in headers)
			{
				message.Headers.TryAddWithoutValidation(name, value);
			}
		}

		return new ODataRequest(RestVerbs.Post, session, ODataLiteral.Batch, message);
	}

	private bool IsJson => session.Settings.BatchFormat == BatchFormat.Json;

	public string NextContentId() => (++LastOperationId).ToString(CultureInfo.InvariantCulture);

	/// <summary>Looks up the Content-ID of a previously written entry, by its data or by the object it was created from.</summary>
	public string? GetContentId(IDictionary<string, object?>? entryData, object? linkData)
	{
		if (entryData is not null && _contentIdMap.TryGetValue(entryData, out var contentId))
		{
			return contentId;
		}

		if (linkData is not null && batchEntries.TryGetValue(linkData, out var mappedEntry) && _contentIdMap.TryGetValue(mappedEntry, out contentId))
		{
			return contentId;
		}

		return null;
	}

	public void MapContentId(IDictionary<string, object?>? entryData, string contentId)
	{
		if (entryData is not null)
		{
			_contentIdMap.TryAdd(entryData, contentId);
		}
	}

	public async Task<ODataBatchOperationRequestMessage> CreateOperationMessageAsync(Uri uri, string method, string? collection, IDictionary<string, object?>? entryData, bool resultRequired)
	{
		if (_batchWriter is null)
		{
			await StartBatchAsync().ConfigureAwait(false);
		}

		if (method != RestVerbs.Get && !_pendingChangeSet)
		{
			await _batchWriter!.WriteStartChangesetAsync().ConfigureAwait(false);
			_pendingChangeSet = true;
		}
		else if (method == RestVerbs.Get && _pendingChangeSet)
		{
			await _batchWriter!.WriteEndChangesetAsync().ConfigureAwait(false);
			_pendingChangeSet = false;
		}

		var contentId = NextContentId();
		if (method != RestVerbs.Get && method != RestVerbs.Delete)
		{
			MapContentId(entryData, contentId);
		}

		var message = await _batchWriter!.CreateOperationRequestMessageAsync(method, uri, contentId, (Microsoft.OData.BatchPayloadUriOption)session.Settings.BatchPayloadUriOption).ConfigureAwait(false);
		var isMutation = method is RestVerbs.Post or RestVerbs.Put or RestVerbs.Patch;
		if (isMutation)
		{
			message.SetHeader(HttpLiteral.ContentId, contentId);
			message.SetHeader(HttpLiteral.Prefer, resultRequired ? HttpLiteral.ReturnRepresentation : HttpLiteral.ReturnMinimal);
		}

		if (collection is not null && method is RestVerbs.Put or RestVerbs.Patch or RestVerbs.Delete && session.Metadata.EntityCollectionRequiresOptimisticConcurrencyCheck(collection))
		{
			message.SetHeader(HttpLiteral.IfMatch, EntityTagHeaderValue.Any.Tag);
		}

		return message;
	}

	public void Dispose()
	{
		_messageWriter?.Dispose();
		_messageWriter = null;
	}
}
