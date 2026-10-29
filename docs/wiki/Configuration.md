# Configuration

Everything is configured through `ODataClientSettings`. Create it from a `Uri`, from an `HttpClient`, or with the parameterless constructor and set `BaseUri` or `HttpClient` yourself.

```csharp
var settings = new ODataClientSettings(new Uri("https://example.com/odata/"))
{
    ProtocolVersion = ODataProtocolVersion.V401,
    BatchFormat = BatchFormat.Json,
    MaxPageSize = 500,
    BeforeRequest = request => request.Headers.Authorization = new("Bearer", token),
};
var client = new ODataClient(settings);
```

Settings are read when a request is built, so they can be changed between calls on a settings object you keep.

## Transport

| Setting | Default | Effect |
|---|---|---|
| `HttpClient` | created | Use your own `HttpClient`; its `BaseAddress` is the service root. |
| `Credentials` | none | Credentials for the default handler. |
| `RequestTimeout` | 100 s | Per-request timeout. |
| `OnCreateMessageHandler` | | Supply the `HttpMessageHandler` used by the default client. |
| `ConfigureHandler` | | Tweak the default `SocketsHttpHandler` (proxy, certificates, pooling). |
| `RequestExecutor` | | Replace HTTP entirely with a `Func<HttpRequestMessage, Task<HttpResponseMessage>>`. Useful in tests. |
| `BeforeRequest`, `BeforeRequestAsync` | | Run before every request, including the `$metadata` request. Add authentication headers here. |
| `AfterResponse`, `AfterResponseAsync` | | Run after every response. |
| `MaxConcurrentRequests` | unlimited | Gate the number of requests in flight. |

## Metadata

| Setting | Default | Effect |
|---|---|---|
| `MetadataDocument` | download | Supply the CSDL text yourself; cached by content hash. |
| `MetadataModel` | | Supply a ready `IEdmModel`. Use this for services whose metadata references external schemas. |
| `MetadataFormat` | `Xml` | `Json` requests CSDL JSON (`$format=application/json`). |
| `SchemaVersion` | | Sent as `$schemaversion`. |
| `NameMatchResolver` | `Alphanumeric` | How CLR names are matched to model names: `Strict`, `Alphanumeric`, `AlphanumericCaseInsensitive`, `NotStrict`. |
| `TypeCache` | per settings | Property mapping and conversion. `TypeCache.Register<T>("Collection")` maps a type to a collection; `TypeCache.RegisterTypeConverter<T>(...)` converts values. |

`ODataClient.ClearMetadataCache()` forgets every downloaded model.

## Protocol and URL conventions

| Setting | Default | Effect |
|---|---|---|
| `ProtocolVersion` | `V4` | `V401` sends `OData-Version: 4.01` and uses 4.01 payload shortcuts (nested delta updates, `@id`). |
| `MetadataLevel` | `Minimal` | `Full` exposes `@odata.type` and `@odata.id` through entry annotations; `None` for minimal payloads. |
| `KeyAsSegment` | `false` | `Products/1` instead of `Products(1)`. |
| `UnqualifiedNameCall` | `false` | Call bound operations without the namespace. |
| `EnumPrefixFree` | `false` | Write enum literals without the type prefix. |
| `PreferredUpdateMethod` | `Patch` | `Put` sends the whole entity. |
| `BulkOperationMode` | `ClientSide` | `Each` sends one `PATCH`/`DELETE` to `Set/$each?$filter=` instead of iterating. See [[Modifying Data]]. |
| `IgnoreNavigationPropertiesOnUpdate` | `false` | Never touch navigation links during an update. |
| `IgnoreUnmappedProperties` | `false` | Tolerate POCO properties that the model does not declare. |
| `Validations` | duplicates and type conflicts | ODataLib reader validation. Set `ValidationKinds.All` to reject undeclared properties again. |
| `ReadUntypedAsString` | `true` | Untyped (`Edm.Untyped`) values arrive as raw JSON text; `false` parses them into dictionaries, lists and primitives. |

## Preferences

These become `Prefer` header values on every request.

| Setting | Header value |
|---|---|
| `MaxPageSize` | `odata.maxpagesize=N` |
| `TrackChanges` | `odata.track-changes` |
| `OmitNullValues` | `omit-values=nulls` |
| `IncludeAnnotations` | `odata.include-annotations="..."` |
| `RespondAsync`, `AsyncPollingInterval` | `respond-async`; the client polls the status monitor, honouring `Retry-After`. |
| `ContinueOnError` | `odata.continue-on-error` on batches. |

`IncludeAnnotationsInResults` adds an `__annotations` entry (an `ODataEntryAnnotations`) to every untyped result and fills `ODataFeedAnnotations` on feeds.

## Batches

| Setting | Default | Effect |
|---|---|---|
| `BatchFormat` | `Multipart` | `Json` sends an OData 4.01 JSON batch. |
| `BatchPayloadUriOption` | `AbsoluteUri` | How operation URLs are written inside a multipart batch. |
| `ContinueOnError` | `false` | Keep processing after a failed operation. |

## Errors and diagnostics

| Setting | Default | Effect |
|---|---|---|
| `IgnoreResourceNotFoundException` | `false` | A 404 becomes an empty result instead of a `WebRequestException` (direct requests only, not inside batches). |
| `WebRequestExceptionMessageSource` | `ReasonPhrase` | Build the exception message from the response body, the reason phrase, or both. |
| `OnTrace`, `TraceFilter` | | Receive request and response text (`ODataTrace.RequestContent`, `ResponseContent`). |
| `TimeProvider` | `TimeProvider.System` | Clock used for polling delays; replace it in tests. |
