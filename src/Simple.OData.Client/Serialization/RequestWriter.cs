using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.Spatial;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Metadata;

namespace Simple.OData.Client.Serialization;

/// <summary>Builds <see cref="ODataRequest"/>s, writing JSON payloads with ODataLib.</summary>
internal sealed class RequestWriter(Session session, Lazy<BatchWriter>? batchWriter)
{
	private static readonly FrozenDictionary<EdmPrimitiveTypeKind, Type[]> PrimitiveClrTypes = new Dictionary<EdmPrimitiveTypeKind, Type[]>
	{
		[EdmPrimitiveTypeKind.String] = [typeof(string)],
		[EdmPrimitiveTypeKind.Boolean] = [typeof(bool)],
		[EdmPrimitiveTypeKind.Byte] = [typeof(byte)],
		[EdmPrimitiveTypeKind.SByte] = [typeof(sbyte)],
		[EdmPrimitiveTypeKind.Int16] = [typeof(short)],
		[EdmPrimitiveTypeKind.Int32] = [typeof(int)],
		[EdmPrimitiveTypeKind.Int64] = [typeof(long)],
		[EdmPrimitiveTypeKind.Decimal] = [typeof(decimal)],
		[EdmPrimitiveTypeKind.Double] = [typeof(double)],
		[EdmPrimitiveTypeKind.Single] = [typeof(float)],
		[EdmPrimitiveTypeKind.Guid] = [typeof(Guid)],
		[EdmPrimitiveTypeKind.DateTimeOffset] = [typeof(DateTimeOffset)],
		[EdmPrimitiveTypeKind.Date] = [typeof(DateOnly)],
		[EdmPrimitiveTypeKind.TimeOfDay] = [typeof(TimeOnly)],
		[EdmPrimitiveTypeKind.Duration] = [typeof(TimeSpan)],
		[EdmPrimitiveTypeKind.Binary] = [typeof(byte[])],
	}.ToFrozenDictionary();

	private readonly CommandUriBuilder _uriBuilder = new(session);

	private IEdmModel Model => session.Model;

	private MetadataResolver Metadata => session.Resolver;

	private ITypeCache TypeCache => session.TypeCache;

	private INameMatchResolver NameResolver => session.Settings.NameMatchResolver;

	private bool IsBatch => batchWriter is not null;

	private bool IsV401 => session.Settings.ProtocolVersion == ODataProtocolVersion.V401;

	public async Task<ODataRequest> CreateGetRequestAsync(string commandText, bool scalarResult, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		if (IsBatch)
		{
			await CreateBatchOperationMessageAsync(RestVerbs.Get, null, null, commandText, true).ConfigureAwait(false);
		}

		return new ODataRequest(RestVerbs.Get, session, commandText, headers) { ReturnsScalarResult = scalarResult, ResultRequired = true };
	}

	public async Task<ODataRequest> CreatePutRequestAsync(string commandText, Stream stream, string? contentType, bool optimisticConcurrency, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(stream);
		var message = IsBatch
			? await CreateBatchOperationMessageAsync(RestVerbs.Put, null, null, commandText, false).ConfigureAwait(false)
			: new ODataRequestMessage();
		if (IsBatch && contentType is not null)
		{
			message.SetHeader(HttpLiteral.ContentType, contentType);
		}

		var target = await message.GetStreamAsync(CancellationToken.None).ConfigureAwait(false);
		if (stream.CanSeek)
		{
			stream.Position = 0;
		}

		await stream.CopyToAsync(target).ConfigureAwait(false);
		if (IsBatch)
		{
			// the batch writer accepts the next operation only once this operation's stream is closed
			await target.DisposeAsync().ConfigureAwait(false);
		}

		var content = (message as ODataRequestMessage)?.TakeContent();
		return WithPreferences(new ODataRequest(RestVerbs.Put, session, commandText, null, content, contentType, headers) { CheckOptimisticConcurrency = optimisticConcurrency });
	}

	public async Task<ODataRequest> CreateInsertRequestAsync(string collection, string commandText, IDictionary<string, object?> entryData, bool resultRequired, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(entryData);
		commandText = StripTypeSegment(commandText);
		var (content, contentType) = await WriteEntryContentAsync(RestVerbs.Post, collection, commandText, entryData, resultRequired).ConfigureAwait(false);
		return WithPreferences(new ODataRequest(RestVerbs.Post, session, commandText, entryData, content, contentType, headers) { ResultRequired = resultRequired });
	}

	public async Task<ODataRequest> CreateUpdateRequestAsync(string collection, string entryIdent, IReadOnlyDictionary<string, object?> entryKey, IDictionary<string, object?> entryData, bool resultRequired, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(entryIdent);
		ArgumentNullException.ThrowIfNull(entryKey);
		ArgumentNullException.ThrowIfNull(entryData);
		var entityType = Metadata.GetEntityType(collection);
		var usePatch = session.Settings.PreferredUpdateMethod == ODataUpdateMethod.Patch || !HasStructuralProperties(entityType, entryData);
		if (HasUpdatedKeyProperties(entityType, entryKey, entryData))
		{
			usePatch = false;
		}

		var method = usePatch ? RestVerbs.Patch : RestVerbs.Put;
		var (content, contentType) = await WriteEntryContentAsync(method, collection, entryIdent, entryData, resultRequired).ConfigureAwait(false);
		return WithPreferences(new ODataRequest(method, session, entryIdent, entryData, content, contentType, headers)
		{
			ResultRequired = resultRequired,
			CheckOptimisticConcurrency = Metadata.EntityCollectionRequiresOptimisticConcurrencyCheck(collection),
		});
	}

	public async Task<ODataRequest> CreateDeleteRequestAsync(string collection, string entryIdent, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(entryIdent);
		if (IsBatch)
		{
			await CreateBatchOperationMessageAsync(RestVerbs.Delete, collection, null, entryIdent, false).ConfigureAwait(false);
		}

		return new ODataRequest(RestVerbs.Delete, session, entryIdent, headers)
		{
			CheckOptimisticConcurrency = Metadata.EntityCollectionRequiresOptimisticConcurrencyCheck(collection),
		};
	}

	public async Task<ODataRequest> CreateLinkRequestAsync(string collection, string linkName, string entryIdent, string linkIdent, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(linkName);
		ArgumentNullException.ThrowIfNull(entryIdent);
		ArgumentNullException.ThrowIfNull(linkIdent);
		var navigationName = Metadata.GetNavigationPropertyExactName(collection, linkName);
		var method = Metadata.IsNavigationPropertyCollection(collection, navigationName) ? RestVerbs.Post : RestVerbs.Put;
		var commandText = FormatLinkPath(entryIdent, navigationName, null);

		var message = IsBatch
			? await CreateBatchOperationMessageAsync(method, null, null, commandText, false).ConfigureAwait(false)
			: new ODataRequestMessage();
		using (var messageWriter = new ODataMessageWriter(message, WriterSettings(), Model))
		{
			var link = new ODataEntityReferenceLink { Url = Utils.CreateAbsoluteUri(session.BaseUri, linkIdent) };
			await messageWriter.WriteEntityReferenceLinkAsync(link).ConfigureAwait(false);
		}

		var content = (message as ODataRequestMessage)?.TakeContent();
		return WithPreferences(new ODataRequest(method, session, commandText, null, content, message.GetHeader(HttpLiteral.ContentType), headers) { IsLink = true });
	}

	public async Task<ODataRequest> CreateUnlinkRequestAsync(string collection, string linkName, string entryIdent, string? linkIdent, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentNullException.ThrowIfNull(linkName);
		ArgumentNullException.ThrowIfNull(entryIdent);
		var navigationName = Metadata.GetNavigationPropertyExactName(collection, linkName);
		var commandText = FormatLinkPath(entryIdent, navigationName, linkIdent);
		if (IsBatch)
		{
			await CreateBatchOperationMessageAsync(RestVerbs.Delete, null, null, commandText, false).ConfigureAwait(false);
		}

		return new ODataRequest(RestVerbs.Delete, session, commandText, headers) { IsLink = true };
	}

	public async Task<ODataRequest> CreateFunctionRequestAsync(string commandText, string functionName, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(functionName);
		if (IsBatch)
		{
			await CreateBatchOperationMessageAsync(RestVerbs.Get, null, null, commandText, true).ConfigureAwait(false);
		}

		return new ODataRequest(RestVerbs.Get, session, commandText, headers) { ResultRequired = true };
	}

	public async Task<ODataRequest> CreateActionRequestAsync(string commandText, string actionName, string? boundTypeName, IDictionary<string, object?>? parameters, bool resultRequired, IDictionary<string, string>? headers = null)
	{
		ArgumentNullException.ThrowIfNull(commandText);
		ArgumentNullException.ThrowIfNull(actionName);
		var message = IsBatch
			? await CreateBatchOperationMessageAsync(RestVerbs.Post, null, null, commandText, resultRequired).ConfigureAwait(false)
			: new ODataRequestMessage();

		Stream? content = null;
		string? contentType = null;
		if (parameters is { Count: > 0 })
		{
			var action = FindAction(actionName, boundTypeName);
			using (var messageWriter = new ODataMessageWriter(message, WriterSettings(), Model))
			{
				var parameterWriter = await messageWriter.CreateODataParameterWriterAsync(action).ConfigureAwait(false);
				await parameterWriter.WriteStartAsync().ConfigureAwait(false);
				foreach (var (name, value) in parameters)
				{
					var parameter = action.Parameters.BestMatch(x => x.Name, name, NameResolver)
						?? throw new UnresolvableObjectException(name, $"Parameter [{name}] not found for action [{actionName}]");
					await WriteOperationParameterAsync(parameterWriter, parameter, value).ConfigureAwait(false);
				}

				await parameterWriter.WriteEndAsync().ConfigureAwait(false);
			}

			content = (message as ODataRequestMessage)?.TakeContent();
			contentType = message.GetHeader(HttpLiteral.ContentType);
		}

		return WithPreferences(new ODataRequest(RestVerbs.Post, session, commandText, parameters, content, contentType, headers) { ResultRequired = resultRequired });
	}

	// ---- entries ----------------------------------------------------------------------------------------------

	private async Task<(Stream? Content, string? ContentType)> WriteEntryContentAsync(string method, string collection, string commandText, IDictionary<string, object?> entryData, bool resultRequired)
	{
		var message = IsBatch
			? await CreateBatchOperationMessageAsync(method, collection, entryData, commandText, resultRequired).ConfigureAwait(false)
			: new ODataRequestMessage();

		var entityType = Metadata.GetEntityType(collection);
		var node = CreateEntryNode(entityType.FullName(), entityType, entryData);
		using (var messageWriter = new ODataMessageWriter(message, WriterSettings(), Model))
		{
			var writer = await messageWriter.CreateODataResourceWriterAsync().ConfigureAwait(false);
			await WriteEntryAsync(writer, node, method).ConfigureAwait(false);
		}

		return ((message as ODataRequestMessage)?.TakeContent(), message.GetHeader(HttpLiteral.ContentType));
	}

	private async Task WriteEntryAsync(ODataWriter writer, EntryNode node, string method)
	{
		await writer.WriteStartAsync(node.Resource).ConfigureAwait(false);
		foreach (var (name, child) in node.ComplexProperties)
		{
			await writer.WriteStartAsync(new ODataNestedResourceInfo { Name = name, IsCollection = false }).ConfigureAwait(false);
			if (child is null)
			{
				await writer.WriteStartAsync((ODataResource?)null).ConfigureAwait(false);
				await writer.WriteEndAsync().ConfigureAwait(false);
			}
			else
			{
				await WriteEntryAsync(writer, child, method).ConfigureAwait(false);
			}

			await writer.WriteEndAsync().ConfigureAwait(false);
		}

		foreach (var (name, items) in node.ComplexCollections)
		{
			await writer.WriteStartAsync(new ODataNestedResourceInfo { Name = name, IsCollection = true }).ConfigureAwait(false);
			await writer.WriteStartAsync(new ODataResourceSet()).ConfigureAwait(false);
			foreach (var item in items)
			{
				await WriteEntryAsync(writer, item, method).ConfigureAwait(false);
			}

			await writer.WriteEndAsync().ConfigureAwait(false);
			await writer.WriteEndAsync().ConfigureAwait(false);
		}

		foreach (var (navigationProperty, values) in node.Links)
		{
			await WriteNavigationValuesAsync(writer, navigationProperty, values, method).ConfigureAwait(false);
		}

		await writer.WriteEndAsync().ConfigureAwait(false);
	}

	private async Task WriteNavigationValuesAsync(ODataWriter writer, IEdmNavigationProperty navigationProperty, List<object> values, string method)
	{
		var isCollection = navigationProperty.Type.IsCollection();
		var targetType = navigationProperty.ToEntityType();
		var keyType = KeyOwner(targetType);
		var items = values.Select(value => DescribeLinkedEntity(value, keyType)).ToList();

		await writer.WriteStartAsync(new ODataNestedResourceInfo { Name = navigationProperty.Name, IsCollection = isCollection }).ConfigureAwait(false);
		if (items.TrueForAll(x => x.ReferenceUrl is not null))
		{
			foreach (var item in items)
			{
				await writer.WriteEntityReferenceLinkAsync(new ODataEntityReferenceLink { Url = item.ReferenceUrl }).ConfigureAwait(false);
			}
		}
		else if (isCollection)
		{
			var useDelta = method == RestVerbs.Patch && IsV401;
			if (useDelta)
			{
				await writer.WriteStartAsync(new ODataDeltaResourceSet()).ConfigureAwait(false);
			}
			else
			{
				await writer.WriteStartAsync(new ODataResourceSet()).ConfigureAwait(false);
			}

			foreach (var item in items)
			{
				await WriteEntryAsync(writer, CreateLinkedEntryNode(item, targetType), method).ConfigureAwait(false);
			}

			await writer.WriteEndAsync().ConfigureAwait(false);
		}
		else
		{
			await WriteEntryAsync(writer, CreateLinkedEntryNode(items[0], targetType), method).ConfigureAwait(false);
		}

		await writer.WriteEndAsync().ConfigureAwait(false);
	}

	private EntryNode CreateLinkedEntryNode(LinkedEntity item, IEdmEntityType declaredType)
	{
		var (typeName, structuredType) = ResolveTypeOverride(item.Data, declaredType);
		var node = CreateEntryNode(typeName, structuredType, item.Data);
		if (item.ReferenceUrl is not null)
		{
			node.Resource.Id = item.ReferenceUrl;
		}

		return node;
	}

	/// <summary>Decides whether a navigation value identifies an existing entity (reference) or carries a new one.</summary>
	private LinkedEntity DescribeLinkedEntity(object value, IEdmEntityType keyType)
	{
		var data = TypeCache.ToDictionary(value);
		var contentId = batchWriter?.Value.GetContentId(data, value);
		if (contentId is not null)
		{
			// a reference to an earlier operation of the same batch: "$<Content-ID>", always relative
			return new LinkedEntity(data, new Uri("$" + contentId, UriKind.Relative));
		}

		if (TryGetAnnotation(data, ReservedNames.ODataId, ReservedNames.ODataIdShort) is string id)
		{
			return new LinkedEntity(data, Utils.CreateAbsoluteUri(session.BaseUri, id));
		}

		var keyProperties = keyType.DeclaredKey?.ToList() ?? [];
		var key = new Dictionary<string, object?>(keyProperties.Count);
		foreach (var keyProperty in keyProperties)
		{
			var match = data.Keys.BestMatch(x => x, keyProperty.Name, NameResolver);
			if (match is null || data[match] is null)
			{
				return new LinkedEntity(data, null);
			}

			key[keyProperty.Name] = data[match];
		}

		if (key.Count == 0)
		{
			return new LinkedEntity(data, null);
		}

		var collectionName = Metadata.GetLinkedCollectionName(value.GetType().Name, keyType.Name, out var isSingleton);
		var url = isSingleton ? collectionName : collectionName + _uriBuilder.ConvertKeyValuesToUriLiteral(key, true);
		return new LinkedEntity(data, Utils.CreateAbsoluteUri(session.BaseUri, url));
	}

	private EntryNode CreateEntryNode(string? typeName, IEdmStructuredType? structuredType, IDictionary<string, object?> data)
	{
		var node = new EntryNode(new ODataResource { TypeName = typeName });
		var properties = new List<ODataProperty>();
		foreach (var (name, value) in data)
		{
			if (IsTypeAnnotation(name))
			{
				continue;
			}

			var property = structuredType is null ? null : structuredType.Properties().BestMatch(x => x.Name, name, NameResolver);
			if (property is null)
			{
				if (structuredType is null || structuredType.IsOpen)
				{
					properties.Add(new ODataProperty { Name = name, Value = ConvertValue(null, value) });
					continue;
				}

				if (Metadata.IgnoreUnmappedProperties)
				{
					continue;
				}

				throw new UnresolvableObjectException(name, $"No property or association found for [{name}].");
			}

			if (property is IEdmNavigationProperty navigationProperty)
			{
				AddLinks(node, navigationProperty, value);
				continue;
			}

			var type = property.Type;
			if (type.IsComplex())
			{
				if (value is not null && TypeCache.Converter.HasObjectConverter(value.GetType()))
				{
					properties.Add(new ODataProperty { Name = property.Name, Value = TypeCache.Converter.Convert(value, value.GetType()) });
				}
				else
				{
					node.ComplexProperties.Add((property.Name, value is null ? null : CreateComplexNode(type.AsComplex(), value)));
				}
			}
			else if (type.IsCollection() && type.AsCollection().ElementType() is { } elementType && elementType.IsComplex())
			{
				if (value is null)
				{
					properties.Add(new ODataProperty { Name = property.Name, Value = null });
				}
				else
				{
					var items = ((IEnumerable)value).Cast<object?>().Where(x => x is not null).Select(x => CreateComplexNode(elementType.AsComplex(), x!)).ToList();
					node.ComplexCollections.Add((property.Name, items));
				}
			}
			else
			{
				properties.Add(new ODataProperty { Name = property.Name, Value = ConvertValue(type, value) });
			}
		}

		node.Resource.Properties = properties;
		return node;
	}

	private EntryNode CreateComplexNode(IEdmComplexTypeReference declaredType, object value)
	{
		var (typeName, structuredType) = ResolveTypeOverride(TypeCache.ToDictionary(value), declaredType.ComplexDefinition());
		return CreateEntryNode(typeName, structuredType, TypeCache.ToDictionary(value));
	}

	private static void AddLinks(EntryNode node, IEdmNavigationProperty navigationProperty, object? value)
	{
		if (value is null)
		{
			return;
		}

		var values = node.Links.FirstOrDefault(x => x.Property == navigationProperty).Values;
		if (values is null)
		{
			values = [];
			node.Links.Add((navigationProperty, values));
		}

		if (navigationProperty.Type.IsCollection() && value is IEnumerable items and not (string or IDictionary))
		{
			values.AddRange(items.Cast<object?>().Where(x => x is not null)!);
		}
		else
		{
			values.Add(value);
		}
	}

	private (string TypeName, IEdmStructuredType Type) ResolveTypeOverride(IDictionary<string, object?> data, IEdmStructuredType declaredType)
	{
		if (TryGetAnnotation(data, ReservedNames.ODataType, ReservedNames.ODataTypeShort) is string annotated)
		{
			var typeName = annotated.TrimStart('#');
			if (Model.FindDeclaredType(typeName) is IEdmStructuredType declared)
			{
				return (declared.FullTypeName(), declared);
			}

			if (Metadata.Index.TryFindSchemaType(typeName, out var schemaType) && schemaType is IEdmStructuredType resolved)
			{
				return (resolved.FullTypeName(), resolved);
			}

			return (typeName, declaredType);
		}

		return (declaredType.FullTypeName(), declaredType);
	}

	// ---- operation parameters ---------------------------------------------------------------------------------

	private IEdmAction FindAction(string actionName, string? boundTypeName)
	{
		var candidates = Metadata.Index.FindActions(actionName).ToList();
		if (candidates.Count == 0)
		{
			return Metadata.GetAction(actionName);
		}

		if (boundTypeName is null)
		{
			return candidates.Find(x => !x.IsBound) ?? candidates[0];
		}

		var boundType = Model.FindDeclaredType(boundTypeName) ?? (Metadata.Index.TryFindSchemaType(boundTypeName, out var schemaType) ? schemaType : null);
		return candidates.Find(x => x.IsBound && BindingMatches(x, boundType)) ?? candidates[0];
	}

	private static bool BindingMatches(IEdmAction action, IEdmType? boundType)
	{
		if (boundType is null)
		{
			return true;
		}

		var bindingType = action.Parameters.FirstOrDefault()?.Type.Definition;
		if (bindingType is IEdmCollectionType collectionType)
		{
			bindingType = collectionType.ElementType.Definition;
		}

		return bindingType == boundType || (boundType is IEdmStructuredType derived && bindingType is IEdmStructuredType baseType && derived.IsOrInheritsFrom(baseType));
	}

	private async Task WriteOperationParameterAsync(ODataParameterWriter parameterWriter, IEdmOperationParameter parameter, object? value)
	{
		var type = parameter.Type;
		switch (type.Definition)
		{
			case IEdmStructuredType structuredType when value is not null:
			{
				var data = TypeCache.ToDictionary(value);
				var (typeName, resolvedType) = ResolveTypeOverride(data, structuredType);
				var writer = await parameterWriter.CreateResourceWriterAsync(parameter.Name).ConfigureAwait(false);
				await WriteEntryAsync(writer, CreateEntryNode(typeName, resolvedType, data), RestVerbs.Post).ConfigureAwait(false);
				break;
			}

			case IEdmCollectionType { ElementType.Definition: IEdmStructuredType elementType } when value is not null:
			{
				var writer = await parameterWriter.CreateResourceSetWriterAsync(parameter.Name).ConfigureAwait(false);
				await writer.WriteStartAsync(new ODataResourceSet()).ConfigureAwait(false);
				foreach (var item in ((IEnumerable)value).Cast<object?>().Where(x => x is not null))
				{
					var data = TypeCache.ToDictionary(item!);
					var (typeName, resolvedType) = ResolveTypeOverride(data, elementType);
					await WriteEntryAsync(writer, CreateEntryNode(typeName, resolvedType, data), RestVerbs.Post).ConfigureAwait(false);
				}

				await writer.WriteEndAsync().ConfigureAwait(false);
				break;
			}

			case IEdmCollectionType collectionType when value is not null:
			{
				var writer = await parameterWriter.CreateCollectionWriterAsync(parameter.Name).ConfigureAwait(false);
				await writer.WriteStartAsync(new ODataCollectionStart()).ConfigureAwait(false);
				foreach (var item in (IEnumerable)value)
				{
					await writer.WriteItemAsync(ConvertValue(collectionType.ElementType, item)).ConfigureAwait(false);
				}

				await writer.WriteEndAsync().ConfigureAwait(false);
				break;
			}

			default:
				await parameterWriter.WriteValueAsync(parameter.Name, ConvertValue(type, value)).ConfigureAwait(false);
				break;
		}
	}

	// ---- values -----------------------------------------------------------------------------------------------

	private object? ConvertValue(IEdmTypeReference? type, object? value)
	{
		if (value is null)
		{
			return null;
		}

		if (type is null)
		{
			return ConvertDynamicValue(value);
		}

		switch (type.Definition)
		{
			case IEdmPrimitiveType primitiveType:
				return ConvertPrimitive(primitiveType.PrimitiveKind, value);
			case IEdmTypeDefinition typeDefinition:
				return ConvertPrimitive(typeDefinition.UnderlyingType.PrimitiveKind, value);
			case IEdmEnumType enumType:
				return value is ODataEnumValue enumValue ? enumValue : new ODataEnumValue(EnumMemberName(value), enumType.FullTypeName());
			case IEdmCollectionType collectionType:
				return new ODataCollectionValue
				{
					TypeName = type.FullName(),
					Items = ((IEnumerable)value).Cast<object?>().Select(x => ConvertValue(collectionType.ElementType, x)).ToList(),
				};
			case IEdmUntypedType:
				return new ODataUntypedValue { RawValue = JsonSerializer.Serialize(value) };
			default:
				return TypeCache.Converter.HasObjectConverter(value.GetType())
					? TypeCache.Converter.Convert(value, value.GetType())
					: throw new NotSupportedException($"Conversion is not supported from type {value.GetType()} to OData type {type.FullName()}.");
		}
	}

	private object ConvertDynamicValue(object value) => value switch
	{
		ODataValue or string or bool or byte or sbyte or short or int or long or decimal or double or float or Guid or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or byte[] or ISpatial => value,
		Enum enumValue => new ODataEnumValue(EnumMemberName(enumValue)),
		DateTime dateTime => new DateTimeOffset(dateTime),
		IDictionary<string, object?> or IEnumerable => new ODataUntypedValue { RawValue = JsonSerializer.Serialize(value) },
		_ => TypeCache.Converter.HasObjectConverter(value.GetType()) ? TypeCache.Converter.Convert(value, value.GetType()) ?? value : value,
	};

	private object ConvertPrimitive(EdmPrimitiveTypeKind kind, object value)
	{
		if (value is ISpatial || !PrimitiveClrTypes.TryGetValue(kind, out var candidates))
		{
			return value;
		}

		foreach (var candidate in candidates)
		{
			if (candidate.IsInstanceOfType(value))
			{
				return value;
			}
		}

		foreach (var candidate in candidates)
		{
			if (TypeCache.TryConvert(value, candidate, out var converted) && converted is not null)
			{
				return converted;
			}
		}

		throw new NotSupportedException($"Conversion is not supported from type {value.GetType()} to OData type Edm.{kind}.");
	}

	private static string EnumMemberName(object value) => value switch
	{
		Enum enumValue => enumValue.ToString().Replace(" ", string.Empty, StringComparison.Ordinal),
		string text => text,
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? string.Empty,
	};

	// ---- helpers ----------------------------------------------------------------------------------------------

	private async Task<IODataRequestMessage> CreateBatchOperationMessageAsync(string method, string? collection, IDictionary<string, object?>? entryData, string commandText, bool resultRequired) =>
		await batchWriter!.Value.CreateOperationMessageAsync(Utils.CreateAbsoluteUri(session.BaseUri, commandText), method, collection, entryData, resultRequired).ConfigureAwait(false);

	/// <summary>In batch mode ODataLib must dispose each operation's stream before the batch writer accepts the next one.</summary>
	private ODataMessageWriterSettings WriterSettings()
	{
		var settings = ODataLibSettings.CreateWriterSettings(session);
		settings.EnableMessageStreamDisposal = IsBatch;
		return settings;
	}

	private static ODataRequest WithPreferences(ODataRequest request)
	{
		// Prefer has no effect on GET or DELETE
		if (request.Method is not (RestVerbs.Get or RestVerbs.Delete))
		{
			request.AddPreference(request.ResultRequired ? HttpLiteral.ReturnRepresentation : HttpLiteral.ReturnMinimal);
		}

		return request;
	}

	private static string FormatLinkPath(string entryIdent, string navigationPropertyName, string? linkIdent)
	{
		if (linkIdent is null)
		{
			return $"{entryIdent}/{navigationPropertyName}/{ODataLiteral.Ref}";
		}

		// OData 4.01 addresses a collection member's reference with a key segment on the navigation property
		// (Categories(1)/Products(2)/$ref), which is also the only form ASP.NET Core OData routes by convention.
		var keyStart = linkIdent.IndexOfAny(['(', '/']);
		var keyPredicate = keyStart < 0 ? linkIdent : linkIdent[keyStart..];
		return $"{entryIdent}/{navigationPropertyName}{keyPredicate}/{ODataLiteral.Ref}";
	}

	private bool HasStructuralProperties(IEdmEntityType entityType, IDictionary<string, object?> entryData) =>
		entryData.Keys.Any(name => Metadata.Index.TryFindStructuralProperty(entityType, name, out _) || (entityType.IsOpen && !Metadata.Index.TryFindNavigationProperty(entityType, name, out _)));

	private bool HasUpdatedKeyProperties(IEdmEntityType entityType, IReadOnlyDictionary<string, object?> entryKey, IDictionary<string, object?> entryData)
	{
		foreach (var keyProperty in Metadata.Index.DeclaredKeyNames(entityType))
		{
			var keyName = entryKey.Keys.BestMatch(x => x, keyProperty, NameResolver);
			var dataName = entryData.Keys.BestMatch(x => x, keyProperty, NameResolver);
			if (keyName is not null && dataName is not null && !Equals(entryKey[keyName], entryData[dataName]))
			{
				return true;
			}
		}

		return false;
	}

	private static IEdmEntityType KeyOwner(IEdmEntityType entityType)
	{
		var current = entityType;
		while (current.DeclaredKey is null && current.BaseEntityType() is { } baseType)
		{
			current = baseType;
		}

		return current;
	}

	private static string StripTypeSegment(string commandText)
	{
		var separator = commandText.LastIndexOf('/');
		if (separator <= 0)
		{
			return commandText;
		}

		var last = commandText.AsSpan(separator + 1);
		return last.Contains('.') && !last.Contains('(') && last[0] != '$' ? commandText[..separator] : commandText;
	}

	private static bool IsTypeAnnotation(string name) => name is ReservedNames.ODataType or ReservedNames.ODataTypeShort or ReservedNames.ODataId or ReservedNames.ODataIdShort;

	private static object? TryGetAnnotation(IDictionary<string, object?> data, string name, string shortName) =>
		data.TryGetValue(name, out var value) ? value : data.TryGetValue(shortName, out value) ? value : null;

	private sealed class EntryNode(ODataResource resource)
	{
		public ODataResource Resource { get; } = resource;

		public List<(string Name, EntryNode? Node)> ComplexProperties { get; } = [];

		public List<(string Name, List<EntryNode> Items)> ComplexCollections { get; } = [];

		public List<(IEdmNavigationProperty Property, List<object> Values)> Links { get; } = [];
	}

	private sealed record LinkedEntity(IDictionary<string, object?> Data, Uri? ReferenceUrl);
}
