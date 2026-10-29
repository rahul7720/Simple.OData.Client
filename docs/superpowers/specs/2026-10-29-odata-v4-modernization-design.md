# Simple.OData.Client 7.0 — .NET 10 / OData V4.01 modernization design

Date: 2026-10-29  
Branch: `xver1`  
Status: approved for implementation under the assumptions listed in §3 (the session ran autonomously; the user asked for the full modernization and could not answer questions mid-task).

## 1. Goals

1. Upgrade the library to **.NET 10** (`net10.0` only) and **C# 14**, using the latest language features where they improve clarity or performance.
2. Support **only OData V4.x** (4.0 and 4.01), built on **Microsoft.OData.Core 9.0.0** (the current ODataLib, which itself targets .NET 10 only).
3. Implement the **OData 4.01 feature set** that a client can meaningfully expose (see §6 feature matrix).
4. **Optimize** the library: remove reflection and LINQ from hot paths, index metadata once per model, reuse HTTP connections and ODataLib settings, make lambda-variable naming thread safe, and reduce allocations in URL building and entity materialization.
5. Keep the library's identity: a metadata-driven client that needs **no generated proxy classes**, usable with typed POCOs, dictionaries, and `dynamic`, through the familiar fluent API (`client.For<Product>().Filter(...).Expand(...).FindEntriesAsync()`).
6. Ship a **real, end-to-end test suite** against an in-process OData V4 server instead of 1,266 recorded V3/Atom fixtures.

## 2. Non-goals and removed scope

No backward compatibility is required. The following are removed outright:

| Removed | Reason |
|---|---|
| `Simple.OData.Client.V3.Adapter`, `Microsoft.Data.OData` dependency, Atom/XML payloads, `MERGE` verb, `DataServiceVersion` headers | V1–V3 are out of scope |
| `Simple.OData.Client.Core` + `Simple.OData.Client.V4.Adapter` split and the `Simple.OData.Client` metapackage | Collapsed into one package `Simple.OData.Client` |
| `ODataAdapterFactory`, `IODataAdapter`, `IODataModelAdapter`, `AdapterVersion`, the `ODataProtocolVersion` string constants (replaced by an enum of the same name), reflection-based adapter assembly loading | No multi-protocol dispatch |
| `ODataPayloadFormat`, `RenewHttpConnection`, `ODataUpdateMethod.Merge` | V3-only or anti-pattern |
| `netstandard2.0`, `net7.0`, `net8.0`, `net48` targets | .NET 10 only |
| `Simple.OData.Samples.ApiV3/V4`, `Simple.OData.Tests.Samples.*`, `Simple.OData.Tests.Shared.*` (OWIN/WCF Data Services, net48) | Replaced by the ASP.NET Core test server |
| `MockData/*.txt` recorded V3 Atom request/response pairs | Replaced by end-to-end tests |
| xunit 2 + MSTest + FluentAssertions 6 + Moq | Replaced by xunit.v3, AwesomeAssertions |
| `.vsspell` files | Stale tooling |

Also out of scope: trimming/AOT compatibility (the dynamic API and reflection mapping make this impractical; the assembly is marked non-trimmable), and OData CSDL *writing*.

## 3. Decisions and assumptions (for user review)

These were decided without the ability to ask. Each is cheap to change later.

1. **Single NuGet package** `Simple.OData.Client` (version 7.0.0). The V3/V4 split packages are discontinued.
2. **ODataLib 9.0.0** (not 8.x) because it is the current major and targets .NET 10. Consequence: the test server must use `Microsoft.AspNetCore.OData 10.0.0-preview.2` (the only AspNetCore OData line compatible with ODataLib 9). This preview is a **test-only** dependency; the shipped library has no preview dependencies.
3. **Protocol version** defaults to OData **4.0** on the wire (`OData-Version: 4.0`) with `OData-MaxVersion: 4.01`, so existing 4.0 servers keep working; 4.01-only features switch the request to 4.01 where the protocol requires it (JSON batch, `$each`, deep update). `ODataClientSettings.ProtocolVersion` can force 4.01 globally.
4. **Public API shape is preserved but modernized**: overload pairs `(…)` / `(…, CancellationToken)` collapse into one method with `CancellationToken cancellationToken = default`; collection results become `IReadOnlyList<T>`; dictionaries are `IDictionary<string, object?>`; nullable reference types are annotated throughout. Everything a fluent chain could do before, it can still do.
5. **Dynamic API** (`ODataDynamic.Expression`, `dynamic` results) is kept — it is a differentiator of this library.
6. **Data aggregation (`$apply`)** moves from a `WithExtensions()` add-on into the core fluent API: `IBoundClient<T>.Apply(...)`.
7. **Test assertions** use AwesomeAssertions (MIT fork of FluentAssertions; FluentAssertions 8 changed to a commercial license).
8. **CI** moves to `ubuntu-latest` with `dotnet-version: 10.0.x`; the release workflow packs on `releases/*` branches as before.
9. **Live-service tests** (services.odata.org) are kept but opt-in (`ODATA_LIVE_TESTS=1`) because the public services are flaky.
10. Strong-name signing with the existing `Simple.OData.Client.snk` is kept (harmless, keeps the key continuity).

## 4. Architecture

### 4.1 Repository layout

```
src/
  Directory.Build.props            shared build settings (net10.0, C# 14, nullable, analyzers, signing)
  Directory.Packages.props         central package versions
  global.json                      SDK 10.0.x, rollForward latestFeature
  Simple.OData.Client.slnx         solution (new XML solution format)
  Simple.OData.Client/             the library (single assembly, package id Simple.OData.Client)
  Simple.OData.Client.Tests/       unit tests + in-process server tests (xunit.v3)
  Simple.OData.Client.Tests.TestServer/   ASP.NET Core OData V4 Northwind-style service (library, hosted by TestServer)
  Simple.OData.Client.Tests.Integration/  opt-in tests against public services.odata.org V4 endpoints
  Simple.OData.Client.Benchmarks/  BenchmarkDotNet
docs/superpowers/specs|plans/      this document and the implementation plan
.github/workflows/                 CI on .NET 10
```

### 4.2 Library internals (namespaces are internal unless noted)

```
Simple.OData.Client                 public API surface (see §5)
Simple.OData.Client.Metadata        EdmMetadataIndex, MetadataResolver (IMetadata impl), MetadataLoader (XML + CSDL JSON)
Simple.OData.Client.Commands        FluentCommand, CommandDetails, ResolvedCommand, UriBuilder (command → URL)
Simple.OData.Client.Expressions     ODataExpression tree, LINQ translator, function/operator maps, expand/select/orderby extractors
Simple.OData.Client.Serialization   RequestWriter, ResponseReader, BatchWriter, ODataRequestMessage/ODataResponseMessage adapters
Simple.OData.Client.Http            RequestRunner, HttpConnection, preference/ETag header handling, async-monitor handling
Simple.OData.Client.TypeSystem      TypeCache, TypeDescriptorCache (compiled accessors), TypeConverter, Dictionary↔object mapping
Simple.OData.Client.Aggregation     $apply builders (typed + dynamic)
Simple.OData.Client.Dynamic         DynamicODataEntry, DynamicODataExpression, ODataDynamic
```

### 4.3 Request flow

```
fluent call → FluentCommand (mutable builder)
           → ResolvedCommand (names resolved against EdmMetadataIndex, keys/filter-as-key interpreted)
           → UriBuilder.Format(ResolvedCommand) → relative URL string
           → RequestWriter → ODataRequest (HttpRequestMessage + payload stream written by ODataLib)
           → RequestRunner (preferences, ETags, BeforeRequest/AfterResponse hooks, concurrency gate, respond-async monitor)
           → ResponseReader (ODataLib ODataMessageReader → ODataResponse: feed/entry/value/collection/batch/delta/error)
           → materialization: dictionaries → T via TypeDescriptorCache (compiled setters) or DynamicODataEntry
```

Batch: `ODataBatch` collects `Func<IODataClient, Task>` actions; replaying them against a batch-mode client routes each request into `BatchWriter` (multipart or JSON batch), the batch response is split, and the actions are replayed a second time against a response-bound client to hand each caller its result (same design as today, which is what makes `$batch` transparent to callers).

### 4.4 Metadata

`EdmMetadataIndex` is built once per `IEdmModel` and cached per service URL (`ConcurrentDictionary<string, EdmMetadataIndex>` replacing `EdmMetadataCache`). It precomputes with `FrozenDictionary`:

- entity sets, singletons, entity/complex/enum types, functions, actions, function/action imports by **exact name** and by **homogenized name** (lower-case alphanumerics), plus singular/plural variants for the default `BestMatchResolver`;
- per structured type: structural and navigation properties by exact and homogenized name, declared key, alternate keys (`OData.Community.Keys.V1.AlternateKeys` and `Core.AlternateKeys`), optimistic-concurrency flag, open-type flag, derived types, `HasStream`;
- per navigation property: target type, partner, `ContainsTarget`, is-collection.

`IMetadata` (public, as today) is implemented by `MetadataResolver` over the index. Custom `INameMatchResolver` implementations are honoured by falling back to a linear scan (results memoized per key), so the fast path is used for the three built-in resolvers.

### 4.5 Type system

`TypeCache` (public `ITypeCache`, slimmed) wraps `TypeDescriptorCache`: per CLR type a frozen map of OData property name → `PropertyAccessor` (compiled getter/setter delegates, mapped name from `[Column]`, `[DataMember]`, `[JsonPropertyName]`, `[JsonProperty]`, `[NotMapped]`/`[JsonIgnore]` honoured), key properties, dynamic-properties container, annotations property (`ODataEntryAnnotations`), derived types. Materialization (`IDictionary → T`) and extraction (`T → IDictionary`) go through these accessors; `PropertyInfo.SetValue` is gone from hot paths. Anonymous types are constructed via their single constructor (cached). Enum, `DateOnly`/`TimeOnly`, `DateTime`↔`DateTimeOffset`, `TimeSpan`↔`Duration`, `Guid`, `byte[]`, geospatial (`GeographyPoint`, `GeometryPoint`) conversions are built in; `ITypeConverter` remains the extension point for custom converters.

## 5. Public API

### 5.1 Entry points

```csharp
public sealed class ODataClient : IODataClient, IDisposable
{
    public ODataClient(Uri baseUri);
    public ODataClient(ODataClientSettings settings);
    public ODataClient(HttpClient httpClient, ODataClientSettings? settings = null);

    public static Task<IEdmModel> GetMetadataAsync(Uri baseUri, CancellationToken ct = default);
    public static Task<string> GetMetadataDocumentAsync(Uri baseUri, CancellationToken ct = default);
    public static IEdmModel ParseMetadata(string csdl);            // XML or CSDL JSON
    public static void ClearMetadataCache();

    public IBoundClient<IDictionary<string, object?>> For(string collectionName);
    public IBoundClient<ODataEntry> For(ODataExpression expression);
    public IBoundClient<T> For<T>(string? collectionName = null) where T : class;
    public IUnboundClient<object> Unbound();
    public IUnboundClient<T> Unbound<T>() where T : class;

    public Task<IEdmModel> GetMetadataAsync(CancellationToken ct = default);
    public Task<string> GetMetadataDocumentAsync(CancellationToken ct = default);
    public Task<string> GetCommandTextAsync<T>(string collection, Expression<Func<T, bool>> expression, CancellationToken ct = default);

    // Untyped, command-text based API (kept for power users and batches)
    public Task<IReadOnlyList<IDictionary<string, object?>>> FindEntriesAsync(string commandText, ODataFeedAnnotations? annotations = null, CancellationToken ct = default);
    public Task<IDictionary<string, object?>?> FindEntryAsync(string commandText, CancellationToken ct = default);
    public Task<object?> FindScalarAsync(string commandText, CancellationToken ct = default);
    public Task<IDictionary<string, object?>?> GetEntryAsync(string collection, IDictionary<string, object?> key, CancellationToken ct = default);
    public Task<IDictionary<string, object?>?> InsertEntryAsync(string collection, IDictionary<string, object?> data, bool resultRequired = true, CancellationToken ct = default);
    public Task<IDictionary<string, object?>?> UpdateEntryAsync(string collection, IDictionary<string, object?> key, IDictionary<string, object?> data, bool resultRequired = true, CancellationToken ct = default);
    public Task<IReadOnlyList<IDictionary<string, object?>>> UpdateEntriesAsync(string collection, string commandText, IDictionary<string, object?> data, bool resultRequired = true, CancellationToken ct = default);
    public Task DeleteEntryAsync(string collection, IDictionary<string, object?> key, CancellationToken ct = default);
    public Task<int> DeleteEntriesAsync(string collection, string commandText, CancellationToken ct = default);
    public Task LinkEntryAsync(string collection, IDictionary<string, object?> key, string linkName, IDictionary<string, object?> linkedKey, CancellationToken ct = default);
    public Task UnlinkEntryAsync(string collection, IDictionary<string, object?> key, string linkName, IDictionary<string, object?>? linkedKey = null, CancellationToken ct = default);
    public Task<Stream> GetMediaStreamAsync(string commandText, CancellationToken ct = default);
    public Task SetMediaStreamAsync(string commandText, Stream stream, string contentType, bool optimisticConcurrency = false, CancellationToken ct = default);
    public Task<IDictionary<string, object?>?> ExecuteFunctionAsSingleAsync(string name, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
    public Task<IReadOnlyList<IDictionary<string, object?>>> ExecuteFunctionAsEnumerableAsync(...);
    public Task<T?> ExecuteFunctionAsScalarAsync<T>(...);
    public Task<T[]> ExecuteFunctionAsArrayAsync<T>(...);
    public Task ExecuteActionAsync(...); ExecuteActionAsSingleAsync / AsEnumerableAsync / AsScalarAsync<T> / AsArrayAsync<T>
    public Task<ODataDelta> FindDeltaAsync(Uri deltaLink, CancellationToken ct = default);
    public Task<ODataResponse> GetResponseAsync(ODataRequest request, CancellationToken ct = default);
    public Task<Stream> GetResponseStreamAsync(ODataRequest request, CancellationToken ct = default);
}
```

### 5.2 Fluent client (`IBoundClient<T> : IFluentClient<T, IBoundClient<T>>`)

Query building (all return the client): `For`, `As`/`As<U>`, `Key(...)`, `Filter(string | ODataExpression | Expression<Func<T,bool>>)`, `Search`, `Skip`, `Top`, `Index` (4.01 `$index`), `OrderBy`/`ThenBy`/`OrderByDescending`/`ThenByDescending`, `Select`, `Expand(...)` (string paths with inline options, `ODataExpandOptions`, typed lambdas with `Where/Select/OrderBy/Take/Skip`), `Compute(string | Expression<Func<T, TR>>)`, `Apply(...)` (typed/dynamic/string `$apply`), `Count()` (`/$count` segment), `WithCount()` (`$count=true`), `QueryOptions(...)`, `WithHeader(s)`, `WithIfMatch(etag)`, `WithIfNoneMatch(etag)`, `WithMaxPageSize(n)`, `TrackChanges()`, `WithProperties(...)` (dynamic container), `WithMedia(...)`, `Media(...)`, `NavigateTo(...)`, `Function(name)`, `Action(name)`, `Set(...)`, `BuildRequestFor()`.

Execution:

```csharp
Task<IReadOnlyList<T>> FindEntriesAsync(CancellationToken ct = default);
Task<IReadOnlyList<T>> FindEntriesAsync(ODataFeedAnnotations annotations, CancellationToken ct = default);
Task<IReadOnlyList<T>> FindEntriesAsync(Uri annotatedUri, ODataFeedAnnotations annotations, CancellationToken ct = default);
IAsyncEnumerable<T> FindEntriesAsAsyncEnumerable(CancellationToken ct = default);   // follows @odata.nextLink
Task<T?> FindEntryAsync(CancellationToken ct = default);
Task<U?> FindScalarAsync<U>(CancellationToken ct = default);
Task<T?> InsertEntryAsync(bool resultRequired = true, CancellationToken ct = default);
Task<T?> UpdateEntryAsync(bool resultRequired = true, CancellationToken ct = default);
Task<IReadOnlyList<T>> UpdateEntriesAsync(bool resultRequired = true, CancellationToken ct = default);
Task DeleteEntryAsync(CancellationToken ct = default);
Task<int> DeleteEntriesAsync(CancellationToken ct = default);
Task LinkEntryAsync<U>(...); Task UnlinkEntryAsync<U>(...);   // $ref
Task ExecuteAsync(...); ExecuteAsSingleAsync / AsEnumerableAsync / AsScalarAsync<U> / AsArrayAsync<U>
Task<string> GetCommandTextAsync(CancellationToken ct = default);
```

`IUnboundClient<T>` (functions/actions/singletons not bound to a set), `IMediaClient` (streams), `IRequestBuilder<T>`/`IClientWithRequest<T>`/`IClientWithResponse<T>` (two-phase request building for custom transports) are retained with the same collapsed-overload treatment.

### 5.3 Settings

```csharp
public sealed class ODataClientSettings
{
    Uri? BaseUri; HttpClient? HttpClient; ICredentials? Credentials; TimeSpan RequestTimeout;
    Func<HttpMessageHandler>? OnCreateMessageHandler; Action<SocketsHttpHandler>? ConfigureHandler;
    Func<HttpRequestMessage, Task<HttpResponseMessage>>? RequestExecutor;
    Action<HttpRequestMessage>? BeforeRequest; Func<HttpRequestMessage, CancellationToken, Task>? BeforeRequestAsync;
    Action<HttpResponseMessage>? AfterResponse; Func<HttpResponseMessage, CancellationToken, Task>? AfterResponseAsync;
    int MaxConcurrentRequests;

    string? MetadataDocument; IEdmModel? MetadataModel; MetadataFormat MetadataFormat (Xml | Json); string? SchemaVersion;
    INameMatchResolver NameMatchResolver; ITypeCache TypeCache;

    ODataProtocolVersion ProtocolVersion (V4 | V401); ODataMetadataLevel MetadataLevel (Minimal | Full | None);
    bool KeyAsSegment; bool UnqualifiedNameCall; bool EnumPrefixFree; bool UseAbsoluteReferenceUris;
    BatchFormat BatchFormat (Multipart | Json); BatchPayloadUriOption BatchPayloadUriOption;
    ODataUpdateMethod PreferredUpdateMethod (Patch | Put); BulkOperationMode BulkOperationMode (ClientSide | Each);
    bool IgnoreNavigationPropertiesOnUpdate; bool IgnoreUnmappedProperties; bool IgnoreResourceNotFoundException;
    bool IncludeAnnotationsInResults; string? IncludeAnnotations (Prefer odata.include-annotations); bool ReadUntypedAsString;
    int? MaxPageSize; bool TrackChanges; bool OmitNullValues; bool ContinueOnError; bool RespondAsync; TimeSpan AsyncPollingInterval;
    bool Ieee754Compatible; ValidationKinds Validations;
    WebRequestExceptionMessageSource WebRequestExceptionMessageSource;
    Action<string, object?[]>? OnTrace; ODataTrace TraceFilter; TimeProvider TimeProvider;
}
```

### 5.4 Models and exceptions

`ODataEntry`, `ODataEntryAnnotations`, `ODataFeedAnnotations`, `ODataMediaAnnotations`, `ODataExpandOptions`, `ODataExpandAssociation`, `ODataOrderByColumn`, `EntityCollection`, `ODataRequest`, `ODataResponse` (with `Error` and `Headers`), new `ODataDelta` (`Changed`, `Deleted`, `AddedLinks`, `DeletedLinks`, `DeltaLink`, `NextPageLink`), new `ODataErrorDetails` (code, message, target, details, inner error) exposed on `WebRequestException.Error`. `UnresolvableObjectException` kept. Immutable value objects become `sealed record`s.

## 6. OData 4.01 feature matrix

| Feature | Client surface | Mechanism | Test |
|---|---|---|---|
| `$filter` with all 4.01 built-ins | LINQ (`Contains`, `StartsWith`, … `Regex.IsMatch` → `matchesPattern`), `ODataFunctions.HasSubset/HasSubsequence/GeoDistance/GeoLength/GeoIntersects/Case`, `in`, `has`, `any/all`, `isof/cast`, `now()`, date/time parts, arithmetic (`add sub mul div divby mod`) | `FunctionMapping` table (V4-only), per-context lambda variable naming | unit (URL text) |
| `$select`, `$expand` with nested `$filter/$select/$orderby/$top/$skip/$count/$levels/$search/$expand`, `*`, `*/$ref` | typed lambdas, strings with inline options, `ODataExpandOptions` | `UriBuilder` | unit + server |
| `$orderby`, `$top`, `$skip`, `$count`, `/$count`, `$search`, `$index`, `$schemaversion`, `$format` | fluent methods / settings | `UriBuilder` | unit + server |
| `$compute` | `Compute(string)`, `Compute(x => new { Alias = expr })` | expression formatter with `as Alias` | unit + server |
| `$apply` (filter, groupby, aggregate, compute, topcount/bottomcount…) | `Apply(...)` typed, dynamic, string | `Aggregation` builders | unit + server |
| Server-driven paging (`@odata.nextLink`, `Prefer: odata.maxpagesize`) | `FindEntriesAsync(annotations)`, `FindEntriesAsAsyncEnumerable`, `WithMaxPageSize` | runner + reader | server |
| Delta / change tracking (`Prefer: odata.track-changes`, `@odata.deltaLink`, delta payloads incl. deleted entities/links) | `TrackChanges()`, `FindDeltaAsync(deltaLink)` → `ODataDelta` | ODataLib delta resource set reader | server (delta endpoint) |
| Keys: single, compound, named, alternate keys, key-as-segment, filter-as-key | `Key(...)`, settings `KeyAsSegment` | metadata index | unit + server |
| Singletons, containment, derived types (`/Ns.Type`), open types, enums (flags), complex types/collections | existing fluent | metadata index + writer | server |
| CRUD: POST (deep insert, `@odata.bind`), PATCH/PUT, DELETE, `Prefer: return=representation|minimal`, `omit-values=nulls`, ETags (`If-Match`, `If-None-Match`, `Core.OptimisticConcurrency`) | fluent, `WithIfMatch` | writer + runner | server |
| Bulk ops: `$each` (PATCH/DELETE over `Set/$each?$filter=`) | `BulkOperationMode.Each` on `UpdateEntriesAsync`/`DeleteEntriesAsync` | `UriBuilder` | server (4.01) |
| References: `$ref` add/remove/replace, `$id` | `LinkEntryAsync`, `UnlinkEntryAsync` | writer | server |
| Functions & actions: bound/unbound, imports, overloads, complex/collection/entity parameters, parameter aliases (`@p1`), `$root`-free | `Function/Action`, `Set(parameters)`, `Execute*` | writer (parameter writer) | server |
| Media entities & stream properties (`$value`, `Photo`) | `Media()`, `GetMediaStreamAsync`, `SetMediaStreamAsync` | raw value writer/reader | server |
| Batch: multipart and **JSON batch**, changesets/atomicity groups, `Content-ID` references, partial failures, `continue-on-error` | `ODataBatch`, settings `BatchFormat` | ODataLib batch writer/reader | server |
| Asynchronous requests (`Prefer: respond-async`, 202 + monitor, `application/http` wrapped result) | settings `RespondAsync`, `AsyncPollingInterval` | runner polling + `ODataAsynchronousReader` | server (simulated endpoint) |
| Metadata: CSDL XML and **CSDL JSON** (`$metadata?$format=application/json`), vocabularies (Core, Capabilities, Community alternate keys), referenced models | settings `MetadataFormat`, `GetMetadataAsync` → `IEdmModel` | `CsdlReader` XML/JSON | unit |
| Instance annotations, `odata.include-annotations`, `odata.metadata=minimal|full|none`, `IEEE754Compatible` | settings | Accept/Prefer headers, reader `ShouldIncludeAnnotation` | server |
| Error payloads (`error.code/message/target/details/innererror`) | `WebRequestException.Error` | `ODataMessageReader.ReadError` | server |

## 7. Performance design

- **Metadata**: `FrozenDictionary` indexes (§4.4) replace 25 `ConcurrentDictionary` memo tables plus repeated LINQ scans of `SchemaElements`.
- **Materialization**: compiled accessors (§4.5); per-type frozen property maps; `ArrayPool`-free but allocation-conscious conversion; collections built via cached activators; no `TypeDescriptor` on hot paths (only as a last-resort converter).
- **URL building**: single `StringBuilder` per command, `SearchValues<char>` for reserved-character detection, span-based path splitting, `Uri.EscapeDataString` applied once per clause.
- **Expressions**: immutable `ODataExpression` nodes; lambda variable counter lives in the formatting context (removes the static, thread-unsafe `ArgumentCounter`); function lookup via frozen dictionary keyed on `(name, argc)`.
- **HTTP**: one `HttpClient` per session over `SocketsHttpHandler` (`PooledConnectionLifetime` 2 min, automatic decompression, HTTP/2 multi-connection); `HttpCompletionOption.ResponseHeadersRead` with streaming into ODataLib; request bodies written to a pooled `MemoryStream`-compatible buffer; `SemaphoreSlim` concurrency gate honours cancellation.
- **ODataLib**: `ODataMessageReaderSettings`/`WriterSettings` created once per session; `ArrayPool` enabled on the reader; `EnableMessageStreamDisposal` set appropriately; `DetectPayloadKind` only when the content type is ambiguous.
- **Threading**: `Lock` (.NET 9+) for the few synchronous critical sections; `SemaphoreSlim` for metadata initialization; no `lock(this)`.
- **Benchmarks**: BenchmarkDotNet suite covering URL formatting, LINQ translation, feed materialization (typed/untyped/dynamic), batch writing — the baseline numbers from the current code are recorded in the plan for comparison where the public API is comparable.

## 8. C# 14 / .NET 10 conventions

`LangVersion` = `14` (`preview` only if the SDK requires it); `Nullable` enabled with zero warnings in the library; `ImplicitUsings`; file-scoped namespaces; primary constructors; `field`-backed properties; extension members (`extension(IEdmModel model) { … }`) for Edm helpers; collection expressions; `params ReadOnlySpan<T>`; `required`/`init` members; `sealed record` value objects; pattern matching and switch expressions; `ArgumentNullException.ThrowIfNull`/`ArgumentException.ThrowIfNullOrWhiteSpace`; `Lock`; `FrozenDictionary`/`FrozenSet`; `SearchValues`; `TimeProvider`; `IAsyncEnumerable`; `ConfigureAwait(false)` everywhere in the library. Analyzers: `Microsoft.CodeAnalysis.NetAnalyzers` at `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` for the library project.

## 9. Error handling

- Non-2xx → `WebRequestException` with `StatusCode`, `ReasonPhrase`, `Response` (raw body), `RequestUri`, and parsed `Error` (`ODataErrorDetails`) when the body is an OData error payload. `IgnoreResourceNotFoundException` keeps returning `null`/empty for 404 on reads.
- Batch: each operation's error becomes an `ODataResponse.Error`; the first failing action rethrows after replay unless `ContinueOnError`.
- Metadata resolution failures → `UnresolvableObjectException` with the unresolved name.
- Unsupported expression shapes → `NotSupportedException` with the offending expression text.
- All public async methods observe `CancellationToken` (HTTP send, metadata initialization, paging loops, async-monitor polling).

## 10. Testing strategy

1. **Unit tests (no network)** — port the metadata-only suites (expressions typed/dynamic, expansion, keys, aggregation, request writer, response reader, type cache, pluralizer, utils, dictionary mapping). V3 rows of `[Theory]` data are dropped; V4 expectations are kept verbatim where behaviour is unchanged. New tests for `$compute`, `$each`, key-as-segment, 4.01 functions, CSDL JSON parsing, error payload parsing, `ODataDelta` reading.
2. **End-to-end tests against an in-process server** — `Simple.OData.Client.Tests.TestServer` hosts a Northwind-style OData V4.01 service with Microsoft.AspNetCore.OData 10 preview over in-memory data (reset per test class): Products/Categories/Suppliers/Employees (self-referencing Superior/Subordinates), Orders/Order_Details (compound key), Customers (Address complex type, AddressType enum), Transport/Ships/Trucks (inheritance), TypeWithGuidKey, Animals (open type), Pictures (media entity + stream property), alternate keys, a singleton, a contained set, bound/unbound functions and actions (primitive/collection/complex/entity parameters), `$apply`, `$search`, `$compute`, delta endpoint, batch (multipart + JSON), ETags, and a simulated `respond-async` endpoint. The former FluentApi/BasicApi scenarios (≈350) are ported to run through `TestServer` via `HttpClient`.
3. **Live integration tests (opt-in)** — V4 Northwind read-only and OData.svc read/write and TripPin RW on services.odata.org, enabled by `ODATA_LIVE_TESTS=1`.
4. **Benchmarks** — BenchmarkDotNet, run manually.

Verification gate for the branch: `dotnet build -warnaserror` on the library, `dotnet test` green for the two non-live test projects on Linux with .NET 10.

## 11. CI and packaging

- `pr-validation.yml` / `ci.yml` / `release.yml`: `ubuntu-latest`, `actions/setup-dotnet@v4` with `10.0.x`, `dotnet restore/build/test`, `dotnet pack` on release; `nuget-deploy.yml` unchanged in intent.
- Package: `Simple.OData.Client` 7.0.0, `PackageReadmeFile`, SourceLink, deterministic build, symbols (`snupkg`), `PackageReleaseNotes` from `RELEASE_NOTES.md`.
- README rewritten for 7.0: what changed, quick start, feature list, migration from 6.x.

## 12. Migration notes (6.x → 7.0, summary for README)

- One package, `net10.0` only, V4/4.01 only.
- `ODataClientSettings.PayloadFormat`, `RenewHttpConnection`, `AdapterFactory` removed; `WithExtensions()` removed — call `Apply` directly.
- Methods take an optional `CancellationToken` instead of duplicated overloads; results are `IReadOnlyList<T>`; `GetMetadataAsync()` returns `IEdmModel`.
- `ODataNameMatchResolver.Alpahumeric` typo fixed to `Alphanumeric`.
- New: `FindEntriesAsAsyncEnumerable`, `Compute`, `Index`, `WithIfMatch`, `WithMaxPageSize`, `TrackChanges`, `FindDeltaAsync`, JSON batch, `$each`, respond-async, CSDL JSON metadata, `WebRequestException.Error`.

## 13. Risks

- `Microsoft.AspNetCore.OData 10.0.0-preview.2` is a preview; it is confined to the test server. If a server-side bug blocks a scenario, the scenario is covered by a unit test on the client's request/response handling instead and the limitation is noted in the plan.
- Deep update (nested collections in PATCH) is a 4.01 writer feature with uneven server support; it is implemented when `ProtocolVersion = V401` and tested only at the request-payload level.
- The public services used by the opt-in live tests are outside our control; they never gate the build.
