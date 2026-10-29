# Advanced Requests

## Headers and preferences per request

```csharp
await client.For<Product>()
    .WithHeader("X-Tenant", "contoso")
    .WithHeaders(new Dictionary<string, string> { ["X-Trace"] = traceId })
    .WithMaxPageSize(50)              // Prefer: odata.maxpagesize=50
    .TrackChanges()                   // Prefer: odata.track-changes
    .WithIfMatch(etag)                // If-Match
    .FindEntriesAsync();
```

A `Prefer` value you set through `WithHeader` is merged with the preferences the client composes, and headers travel with every page of an `IAsyncEnumerable` or next-link request. Settings-level preferences (`MaxPageSize`, `OmitNullValues`, `RespondAsync`, `IncludeAnnotations`) apply to every request.

## Two-phase requests

Build the request, inspect or modify it, send it yourself or through the client, then read the response with the typed readers.

```csharp
var request = await client.For<Product>().Filter(p => p.UnitPrice > 10).BuildRequestFor().FindEntriesAsync();

HttpRequestMessage message = request.GetRequest().RequestMessage;   // look at the URL, headers and body
using var response = await request.RunAsync();                       // send through the client's transport
var products = await response.ReadAsCollectionAsync();

// or send with your own HttpClient and hand the response back
using var http = await myHttpClient.SendAsync(request.GetRequest().RequestMessage);
using var fromElsewhere = request.FromResponse(http);
var same = await fromElsewhere.ReadAsCollectionAsync();
```

`BuildRequestFor()` exposes `FindEntriesAsync`, `FindEntryAsync`, `FindScalarAsync`, `InsertEntryAsync`, `UpdateEntryAsync` and `DeleteEntryAsync`. The response side offers `ReadAsCollectionAsync`, `ReadAsSingleAsync`, `ReadAsScalarAsync<T>` and `GetResponseStreamAsync`. A built request can be run more than once.

## Asynchronous requests (`respond-async`)

```csharp
var settings = new ODataClientSettings(serviceRoot) { RespondAsync = true, AsyncPollingInterval = TimeSpan.FromSeconds(2) };
var client = new ODataClient(settings);
var slow = await client.For<Product>().Function("Slow").ExecuteAsEnumerableAsync();
```

When the service answers `202 Accepted` with a `Location`, the client polls the status monitor until the final response arrives, honouring `Retry-After` in both seconds and HTTP-date forms. Delays use `ODataClientSettings.TimeProvider`, so tests can run them instantly.

## Metadata

```csharp
IEdmModel model = await client.GetMetadataAsync();
string csdl = await client.GetMetadataDocumentAsync();
```

- Supply metadata with `MetadataDocument` (CSDL text) or `MetadataModel` (an `IEdmModel`) to skip the download.
- Request CSDL JSON with `MetadataFormat = MetadataFormat.Json`.
- The built-in vocabularies (Core, Capabilities, Community alternate keys) are resolved locally. External `edmx:Reference` schemas are not downloaded; build the model yourself and pass `MetadataModel` for such services.
- Downloaded models are cached per service root for the process; call `ODataClient.ClearMetadataCache()` to forget them.

## Annotations

```csharp
var settings = new ODataClientSettings(serviceRoot) { IncludeAnnotationsInResults = true, MetadataLevel = ODataMetadataLevel.Full };
var client = new ODataClient(settings);

var entry = await client.For("Products").Key(1).FindEntryAsync();
var annotations = (ODataEntryAnnotations)entry!["__annotations"]!;
Console.WriteLine($"{annotations.Id} {annotations.ETag} {annotations.TypeName}");
```

`ODataEntryAnnotations` carries `Id`, `TypeName`, `ReadLink`, `EditLink`, `ETag`, `AssociationLinks`, `MediaResource` and `InstanceAnnotations`. Request specific instance annotations from the service with `IncludeAnnotations = "*"` or a pattern.

## Metadata level

`MetadataLevel` controls `Accept: application/json;odata.metadata=minimal|full|none`. All three are supported for reading; `None` payloads are read against the collection the command addressed.

## Tracing

```csharp
settings.OnTrace = (format, args) => logger.LogDebug(format, args);
settings.TraceFilter = ODataTrace.RequestContent | ODataTrace.ResponseContent;
```

## Concurrency and timeouts

`MaxConcurrentRequests` gates requests in flight, `RequestTimeout` bounds each request, and every method accepts a `CancellationToken`.
