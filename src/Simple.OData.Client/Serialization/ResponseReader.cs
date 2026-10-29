using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Metadata;
using ODataLibDeltaLink = Microsoft.OData.ODataDeltaLink;

namespace Simple.OData.Client.Serialization;

/// <summary>Reads OData JSON responses (feeds, entries, values, collections, deltas, batches and errors) with ODataLib.</summary>
internal sealed class ResponseReader(Session session)
{
	private IEdmModel Model => session.Model;

	private MetadataResolver Metadata => session.Resolver;

	private ITypeCache TypeCache => session.TypeCache;

	private bool IncludeAnnotations => session.Settings.IncludeAnnotationsInResults;

	public Task<ODataResponse> ReadAsync(HttpResponseMessage response, EntityCollection? expectedCollection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(response);
		var message = new ODataResponseMessage(response);
		ApplyExpectation(message, expectedCollection);
		return ReadAsync(message, cancellationToken);
	}

	public async Task<ODataResponse> ReadAsync(IODataResponseMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		var headers = ToHeaders(message.Headers);
		var statusCode = message.StatusCode;
		if (statusCode == (int)HttpStatusCode.NoContent)
		{
			return ODataResponse.FromStatusCode(TypeCache, statusCode, headers);
		}

		var buffered = message as ODataResponseMessage;
		if (buffered is not null && !await buffered.HasContentAsync(cancellationToken).ConfigureAwait(false))
		{
			return ODataResponse.FromStatusCode(TypeCache, statusCode, headers);
		}

		var contentType = message.GetHeader(HttpLiteral.ContentType);
		if (string.IsNullOrEmpty(contentType))
		{
			return ODataResponse.FromValue(TypeCache, await ReadTextAsync(message, cancellationToken).ConfigureAwait(false), statusCode, headers);
		}

		if (buffered is { ExpectedNavigationSource: not null } && IsMetadataNone(contentType))
		{
			// without a context URL ODataLib rejects a response payload, but accepts the same JSON as a request payload
			var shape = PeekShape(buffered.Content.Span);
			if (shape != PayloadShape.Unknown)
			{
				using var requestReader = new ODataMessageReader(buffered.AsRequest(), ODataLibSettings.CreateReaderSettings(session), Model);
				var reader = shape == PayloadShape.Feed
					? await requestReader.CreateODataResourceSetReaderAsync(buffered.ExpectedNavigationSource as IEdmEntitySetBase, buffered.ExpectedType).ConfigureAwait(false)
					: await requestReader.CreateODataResourceReaderAsync(buffered.ExpectedNavigationSource, buffered.ExpectedType).ConfigureAwait(false);
				return FromRoot(await ReadResourceTreeAsync(reader).ConfigureAwait(false), statusCode, headers);
			}
		}

		using var messageReader = new ODataMessageReader(message, CreateReaderSettings(message), Model);
		var kinds = (await messageReader.DetectPayloadKindAsync().ConfigureAwait(false)).Select(x => x.PayloadKind).ToHashSet();
		if (kinds.Contains(ODataPayloadKind.Error))
		{
			var error = ToDetails(await messageReader.ReadErrorAsync().ConfigureAwait(false));
			return ODataResponse.FromError(TypeCache, statusCode, headers, error, null);
		}

		if (kinds.Contains(ODataPayloadKind.Batch))
		{
			return await ReadBatchAsync(messageReader, statusCode, headers, cancellationToken).ConfigureAwait(false);
		}

		if (kinds.Contains(ODataPayloadKind.Value) || kinds.Contains(ODataPayloadKind.BinaryValue))
		{
			IEdmTypeReference typeReference = kinds.Contains(ODataPayloadKind.BinaryValue) && !kinds.Contains(ODataPayloadKind.Value)
				? EdmCoreModel.Instance.GetBinary(true)
				: EdmCoreModel.Instance.GetString(true);
			var value = await messageReader.ReadValueAsync(typeReference).ConfigureAwait(false);
			return ODataResponse.FromValue(TypeCache, value, statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.ResourceSet))
		{
			var expected = buffered;
			var reader = expected?.ExpectedNavigationSource is IEdmEntitySetBase set
				? await messageReader.CreateODataResourceSetReaderAsync(set, expected.ExpectedType).ConfigureAwait(false)
				: await messageReader.CreateODataResourceSetReaderAsync().ConfigureAwait(false);
			return FromRoot(await ReadResourceTreeAsync(reader).ConfigureAwait(false), statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.Collection))
		{
			return ODataResponse.FromCollection(TypeCache, await ReadCollectionAsync(messageReader).ConfigureAwait(false), statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.Property) && !kinds.Contains(ODataPayloadKind.Resource))
		{
			var property = await messageReader.ReadPropertyAsync().ConfigureAwait(false);
			return ODataResponse.FromProperty(TypeCache, property.Name ?? ReservedNames.Result, ConvertValue(property.Value), statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.Delta) && !kinds.Contains(ODataPayloadKind.Resource))
		{
			var reader = await messageReader.CreateODataDeltaResourceSetReaderAsync().ConfigureAwait(false);
			return FromRoot(await ReadResourceTreeAsync(reader).ConfigureAwait(false), statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.EntityReferenceLinks))
		{
			var links = await messageReader.ReadEntityReferenceLinksAsync().ConfigureAwait(false);
			return ODataResponse.FromCollection(TypeCache, links.Links.Select(x => (object?)x.Url), statusCode, headers);
		}

		if (kinds.Contains(ODataPayloadKind.EntityReferenceLink))
		{
			var link = await messageReader.ReadEntityReferenceLinkAsync().ConfigureAwait(false);
			return ODataResponse.FromValue(TypeCache, link.Url, statusCode, headers);
		}

		var resourceReader = buffered?.ExpectedNavigationSource is not null
			? await messageReader.CreateODataResourceReaderAsync(buffered.ExpectedNavigationSource, buffered.ExpectedType).ConfigureAwait(false)
			: await messageReader.CreateODataResourceReaderAsync().ConfigureAwait(false);
		return FromRoot(await ReadResourceTreeAsync(resourceReader).ConfigureAwait(false), statusCode, headers);
	}

	public async Task<ODataDelta> ReadDeltaAsync(HttpResponseMessage response, EntityCollection? expectedCollection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(response);
		var message = new ODataResponseMessage(response);
		ApplyExpectation(message, expectedCollection);
		if (!await message.HasContentAsync(cancellationToken).ConfigureAwait(false))
		{
			return new ODataDelta { Changed = [], Deleted = [], AddedLinks = [], DeletedLinks = [] };
		}

		using var messageReader = new ODataMessageReader(message, ODataLibSettings.CreateReaderSettings(session), Model);
		var reader = message.ExpectedNavigationSource is IEdmEntitySetBase set
			? await messageReader.CreateODataDeltaResourceSetReaderAsync(set, message.ExpectedType).ConfigureAwait(false)
			: await messageReader.CreateODataDeltaResourceSetReaderAsync().ConfigureAwait(false);
		var root = await ReadResourceTreeAsync(reader).ConfigureAwait(false);
		var feed = root?.Feed ?? new AnnotatedFeed();
		return new ODataDelta
		{
			Changed = feed.Entries.Select(x => x.GetData(IncludeAnnotations)).ToList(),
			Deleted = [.. feed.DeletedEntries],
			AddedLinks = [.. feed.AddedLinks],
			DeletedLinks = [.. feed.DeletedLinks],
			DeltaLink = feed.Annotations?.DeltaLink,
			NextPageLink = feed.Annotations?.NextPageLink,
		};
	}

	public async Task<ODataErrorDetails?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(response);
		var message = new ODataResponseMessage(response);
		if (message.ContentType is not { } contentType || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		if (!await message.HasContentAsync(cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		return await TryReadErrorAsync(message).ConfigureAwait(false);
	}

	private async Task<ODataErrorDetails?> TryReadErrorAsync(IODataResponseMessage message)
	{
		try
		{
			using var messageReader = new ODataMessageReader(message, CreateReaderSettings(message), Model);
			var kinds = await messageReader.DetectPayloadKindAsync().ConfigureAwait(false);
			if (!kinds.Any(x => x.PayloadKind == ODataPayloadKind.Error))
			{
				return null;
			}

			return ToDetails(await messageReader.ReadErrorAsync().ConfigureAwait(false));
		}
		catch (ODataException)
		{
			return null;
		}
	}

	// ---- batch ------------------------------------------------------------------------------------------------

	private async Task<ODataResponse> ReadBatchAsync(ODataMessageReader messageReader, int statusCode, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
	{
		var batchReader = await messageReader.CreateODataBatchReaderAsync().ConfigureAwait(false);
		var responses = new List<ODataResponse>();
		while (await batchReader.ReadAsync().ConfigureAwait(false))
		{
			if (batchReader.State != ODataBatchReaderState.Operation)
			{
				continue;
			}

			var operation = await batchReader.CreateOperationResponseMessageAsync().ConfigureAwait(false);
			var operationHeaders = ToHeaders(operation.Headers);
			if (operation.StatusCode == (int)HttpStatusCode.NoContent)
			{
				responses.Add(ODataResponse.FromStatusCode(TypeCache, operation.StatusCode, operationHeaders));
			}
			else if (operation.StatusCode >= (int)HttpStatusCode.BadRequest)
			{
				var error = await TryReadErrorAsync(operation).ConfigureAwait(false);
				var exception = WebRequestException.CreateFromStatusCode((HttpStatusCode)operation.StatusCode, session.Settings.WebRequestExceptionMessageSource, null, error);
				responses.Add(ODataResponse.FromError(TypeCache, operation.StatusCode, operationHeaders, error, exception));
			}
			else
			{
				responses.Add(await ReadAsync(operation, cancellationToken).ConfigureAwait(false));
			}
		}

		return ODataResponse.FromBatch(TypeCache, responses, statusCode, headers);
	}

	// ---- collections ------------------------------------------------------------------------------------------

	private async Task<List<object?>> ReadCollectionAsync(ODataMessageReader messageReader)
	{
		var reader = await messageReader.CreateODataCollectionReaderAsync().ConfigureAwait(false);
		var items = new List<object?>();
		while (await reader.ReadAsync().ConfigureAwait(false))
		{
			if (reader.State == ODataCollectionReaderState.Value)
			{
				items.Add(ConvertValue(reader.Item));
			}
		}

		return items;
	}

	// ---- resources --------------------------------------------------------------------------------------------

	private async Task<ResponseNode?> ReadResourceTreeAsync(ODataReader reader)
	{
		ResponseNode? root = null;
		var stack = new Stack<ResponseNode>();
		while (await reader.ReadAsync().ConfigureAwait(false))
		{
			switch (reader.State)
			{
				case ODataReaderState.ResourceSetStart:
				case ODataReaderState.DeltaResourceSetStart:
					stack.Push(new ResponseNode { Feed = new AnnotatedFeed() });
					break;

				case ODataReaderState.ResourceSetEnd:
				case ODataReaderState.DeltaResourceSetEnd:
				{
					var feedNode = stack.Pop();
					feedNode.Feed!.SetAnnotations(CreateAnnotations((ODataResourceSetBase)reader.Item));
					if (stack.Count > 0)
					{
						stack.Peek().Feed = feedNode.Feed;
					}
					else
					{
						root = feedNode;
					}

					break;
				}

				case ODataReaderState.ResourceStart:
					stack.Push(new ResponseNode { Entry = new AnnotatedEntry(new Dictionary<string, object?>(StringComparer.Ordinal)) });
					break;

				case ODataReaderState.ResourceEnd:
				{
					var entryNode = stack.Pop();
					if (reader.Item is ODataResource resource)
					{
						Populate(entryNode.Entry!, resource);
					}
					else
					{
						entryNode.IsNull = true;
					}

					if (stack.Count == 0)
					{
						root = entryNode;
					}
					else if (stack.Peek() is { Feed: { } feed })
					{
						feed.Entries.Add(entryNode.Entry!);
					}
					else
					{
						var parent = stack.Peek();
						parent.Entry = entryNode.Entry;
						parent.IsNull = entryNode.IsNull;
					}

					break;
				}

				case ODataReaderState.DeletedResourceStart:
					stack.Push(new ResponseNode { Entry = new AnnotatedEntry(new Dictionary<string, object?>(StringComparer.Ordinal)), IsDeleted = true });
					break;

				case ODataReaderState.DeletedResourceEnd:
				{
					var deletedNode = stack.Pop();
					if (reader.Item is ODataDeletedResource deleted && stack.Count > 0 && stack.Peek().Feed is { } feed)
					{
						Populate(deletedNode.Entry!, deleted);
						var data = deletedNode.Entry!.Data.Count > 0 ? deletedNode.Entry.Data : null;
						feed.DeletedEntries.Add(new ODataDeletedEntry(deleted.Id?.AbsoluteUri, deleted.Reason?.ToString().ToLowerInvariant(), data));
					}

					break;
				}

				case ODataReaderState.DeltaLink when reader.Item is ODataLibDeltaLink added && stack.Count > 0 && stack.Peek().Feed is { } feed:
					feed.AddedLinks.Add(new ODataDeltaLink(added.Source, added.Relationship, added.Target));
					break;

				case ODataReaderState.DeltaDeletedLink when reader.Item is ODataDeltaDeletedLink removed && stack.Count > 0 && stack.Peek().Feed is { } feed:
					feed.DeletedLinks.Add(new ODataDeltaLink(removed.Source, removed.Relationship, removed.Target));
					break;

				case ODataReaderState.NestedResourceInfoStart:
					stack.Push(new ResponseNode { LinkName = ((ODataNestedResourceInfo)reader.Item).Name });
					break;

				case ODataReaderState.NestedResourceInfoEnd:
					EndNestedResourceInfo(stack);
					break;

				case ODataReaderState.EntityReferenceLink when reader.Item is ODataEntityReferenceLink link && stack.Count > 0:
					(stack.Peek().Primitives ??= []).Add(link.Url);
					break;

				case ODataReaderState.Primitive when stack.Count > 0:
					(stack.Peek().Primitives ??= []).Add(ConvertValue(reader.Item));
					break;

				case ODataReaderState.NestedProperty when reader.Item is ODataPropertyInfo propertyInfo && stack.Count > 0:
					stack.Peek().PendingPropertyName = propertyInfo.Name;
					break;

				case ODataReaderState.Stream when stack.Count > 0:
				{
					using var stream = await reader.CreateReadStreamAsync().ConfigureAwait(false);
					using var buffer = new MemoryStream();
					await stream.CopyToAsync(buffer).ConfigureAwait(false);
					var node = stack.Peek();
					if (node.Entry is not null && node.PendingPropertyName is { } propertyName)
					{
						node.Entry.Data[propertyName] = buffer.ToArray();
					}

					node.PendingPropertyName = null;
					break;
				}

				default:
					break;
			}
		}

		return root;
	}

	private void EndNestedResourceInfo(Stack<ResponseNode> stack)
	{
		var linkNode = stack.Pop();
		var parent = stack.Peek();
		if (parent.Entry is null || linkNode.LinkName is null)
		{
			return;
		}

		object? value;
		if (linkNode.Feed is { } feed)
		{
			value = feed.Entries.Select(x => x.GetData(IncludeAnnotations)).ToList();
			parent.Entry.SetLinkAnnotations(feed.Annotations);
		}
		else if (linkNode.Entry is { } entry)
		{
			value = linkNode.IsNull ? null : entry.GetData(IncludeAnnotations);
		}
		else
		{
			value = linkNode.Primitives;
		}

		parent.Entry.Data[linkNode.LinkName] = value;
	}

	private void Populate(AnnotatedEntry entry, ODataResourceBase resource)
	{
		foreach (var propertyInfo in resource.Properties)
		{
			if (propertyInfo is ODataProperty property)
			{
				entry.Data[property.Name] = ConvertValue(property.Value);
			}
		}

		entry.SetAnnotations(CreateAnnotations(resource));
	}

	private ODataResponse FromRoot(ResponseNode? root, int statusCode, IReadOnlyDictionary<string, string> headers)
	{
		if (root?.Feed is { } feed)
		{
			return ODataResponse.FromFeed(TypeCache, feed, statusCode, headers);
		}

		if (root?.Entry is { } entry && !root.IsNull)
		{
			return ODataResponse.FromEntry(TypeCache, entry, statusCode, headers);
		}

		return ODataResponse.FromFeed(TypeCache, new AnnotatedFeed(), statusCode, headers);
	}

	// ---- annotations ------------------------------------------------------------------------------------------

	private static ODataFeedAnnotations CreateAnnotations(ODataResourceSetBase feed) => new()
	{
		Id = feed.Id?.AbsoluteUri,
		Count = feed.Count,
		DeltaLink = feed.DeltaLink,
		NextPageLink = feed.NextPageLink,
		InstanceAnnotations = feed.InstanceAnnotations.Count > 0 ? feed.InstanceAnnotations.ToList<object>() : null,
	};

	private static ODataEntryAnnotations CreateAnnotations(ODataResourceBase resource)
	{
		string? id = null;
		Uri? readLink = null;
		Uri? editLink = null;
		string? etag = null;
		try
		{
			// computed links are unavailable for transient entities and types without a key
			id = resource.Id?.AbsoluteUri;
			readLink = resource.ReadLink;
			editLink = resource.EditLink;
			etag = resource.ETag;
		}
		catch (ODataException)
		{
			// leave the links unset
		}

		return new ODataEntryAnnotations
		{
			Id = id,
			TypeName = resource.TypeName,
			ReadLink = readLink,
			EditLink = editLink,
			ETag = etag,
			MediaResource = CreateAnnotations(resource.MediaResource),
			InstanceAnnotations = resource.InstanceAnnotations.Count > 0 ? resource.InstanceAnnotations.ToList<object>() : null,
		};
	}

	private static ODataMediaAnnotations? CreateAnnotations(ODataStreamReferenceValue? value) =>
		value is null ? null : new ODataMediaAnnotations { ContentType = value.ContentType, ReadLink = value.ReadLink, EditLink = value.EditLink, ETag = value.ETag };

	// ---- values -----------------------------------------------------------------------------------------------

	private object? ConvertValue(object? value) => value switch
	{
		null or ODataNullValue => null,
		ODataResource resource => ToDictionary(resource),
		ODataCollectionValue collection => collection.Items.Select(ConvertValue).ToList(),
		ODataEnumValue enumValue => enumValue.Value,
		ODataPrimitiveValue primitive => primitive.Value,
		ODataUntypedValue untyped => ConvertUntyped(untyped.RawValue),
		ODataStreamReferenceValue stream => CreateAnnotations(stream),
		_ => value,
	};

	private Dictionary<string, object?> ToDictionary(ODataResource resource)
	{
		var result = new Dictionary<string, object?>(StringComparer.Ordinal);
		foreach (var propertyInfo in resource.Properties)
		{
			if (propertyInfo is ODataProperty property)
			{
				result[property.Name] = ConvertValue(property.Value);
			}
		}

		return result;
	}

	private object? ConvertUntyped(string? rawValue)
	{
		if (string.IsNullOrEmpty(rawValue))
		{
			return null;
		}

		if (session.Settings.ReadUntypedAsString)
		{
			return rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"' ? JsonSerializer.Deserialize<string>(rawValue) : rawValue;
		}

		try
		{
			using var document = JsonDocument.Parse(rawValue);
			return ToClrValue(document.RootElement);
		}
		catch (JsonException)
		{
			return rawValue;
		}
	}

	private static object? ToClrValue(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.Null or JsonValueKind.Undefined => null,
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.String => element.GetString(),
		JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.TryGetInt64(out var l) ? l : element.TryGetDecimal(out var m) ? m : element.GetDouble(),
		JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToList(),
		JsonValueKind.Object => element.EnumerateObject().ToDictionary(x => x.Name, x => ToClrValue(x.Value), StringComparer.Ordinal),
		_ => element.GetRawText(),
	};

	// ---- errors -----------------------------------------------------------------------------------------------

	private static ODataErrorDetails ToDetails(ODataError error) => new()
	{
		Code = error.Code,
		Message = error.Message,
		Target = error.Target,
		Details = error.Details?.Select(x => new ODataErrorDetails { Code = x.Code, Message = x.Message, Target = x.Target }).ToList() ?? [],
		InnerError = FormatInnerError(error.InnerError),
	};

	private static string? FormatInnerError(ODataInnerError? innerError)
	{
		if (innerError is null)
		{
			return null;
		}

		var parts = new List<string>();
		foreach (var (name, value) in innerError.Properties)
		{
			var text = value switch
			{
				ODataPrimitiveValue primitive => Convert.ToString(primitive.Value, CultureInfo.InvariantCulture),
				ODataNullValue or null => null,
				_ => value.ToString(),
			};

			if (!string.IsNullOrEmpty(text))
			{
				parts.Add($"{name}: {text}");
			}
		}

		if (FormatInnerError(innerError.InnerError) is { } nested)
		{
			parts.Add($"innererror: {{{nested}}}");
		}

		return parts.Count == 0 ? null : string.Join("; ", parts);
	}

	// ---- helpers ----------------------------------------------------------------------------------------------

	private void ApplyExpectation(ODataResponseMessage message, EntityCollection? collection)
	{
		if (collection is null)
		{
			return;
		}

		message.ExpectedNavigationSource = Metadata.FindNavigationSource(collection.BaseEntityCollection?.Name ?? collection.Name);
		message.ExpectedType = Metadata.FindEntityTypeForCollection(collection.Name);
	}

	/// <summary>Batch operation streams must be disposed before the batch reader advances, so let ODataLib do it.</summary>
	private ODataMessageReaderSettings CreateReaderSettings(IODataResponseMessage message)
	{
		var settings = ODataLibSettings.CreateReaderSettings(session);
		settings.EnableMessageStreamDisposal = message is ODataBatchOperationResponseMessage;
		return settings;
	}

	private static bool IsMetadataNone(string contentType) => contentType.Contains("odata.metadata=none", StringComparison.OrdinalIgnoreCase);

	private static async Task<string> ReadTextAsync(IODataResponseMessage message, CancellationToken cancellationToken)
	{
		var stream = await message.GetStreamAsync(cancellationToken).ConfigureAwait(false);
		return await Utils.StreamToStringAsync(stream, false, cancellationToken).ConfigureAwait(false);
	}

	private static Dictionary<string, string> ToHeaders(IEnumerable<KeyValuePair<string, string>> headers)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (name, value) in headers)
		{
			result.TryAdd(name, value);
		}

		return result;
	}

	private static PayloadShape PeekShape(ReadOnlySpan<byte> json)
	{
		try
		{
			var reader = new Utf8JsonReader(json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
			if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
			{
				return PayloadShape.Unknown;
			}

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var isValue = reader.ValueTextEquals("value"u8);
				if (!reader.Read())
				{
					return PayloadShape.Unknown;
				}

				if (isValue)
				{
					return reader.TokenType == JsonTokenType.StartArray ? PayloadShape.Feed : PayloadShape.Entry;
				}

				reader.TrySkip();
			}

			return PayloadShape.Entry;
		}
		catch (JsonException)
		{
			return PayloadShape.Unknown;
		}
	}

	private enum PayloadShape
	{
		Unknown,
		Feed,
		Entry,
	}
}
