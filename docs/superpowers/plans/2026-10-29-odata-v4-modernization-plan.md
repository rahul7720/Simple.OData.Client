# Simple.OData.Client 7.0 (.NET 10 / OData V4.01) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the multi-protocol, multi-target library with a single `net10.0` / C# 14 package built on Microsoft.OData.Core 9.0.0 that implements OData V4.0/4.01, with an end-to-end test suite against an in-process OData server.

**Architecture:** One assembly: fluent/dynamic API → `FluentCommand` → `ResolvedCommand` (resolved against a frozen `EdmMetadataIndex`) → `CommandUriBuilder` (URL) → `RequestWriter` (ODataLib) → `RequestRunner` (HttpClient, preferences, ETags, async monitor) → `ResponseReader` (ODataLib) → `DictionaryMapper` (compiled accessors) → `T` / dictionary / `dynamic`. Batches replay the same pipeline into `BatchWriter`.

**Tech Stack:** .NET 10 SDK 10.0.4xx, C# 14, Microsoft.OData.Core/Edm/Spatial 9.0.0, xunit.v3 4.0.x, AwesomeAssertions 9.x, Microsoft.AspNetCore.OData 10.0.0-preview.2 + Microsoft.OData.ModelBuilder 3.0.0 (test server only), Microsoft.AspNetCore.TestHost 10.0.x, BenchmarkDotNet 0.15.x.

**Spec:** `docs/superpowers/specs/2026-10-29-odata-v4-modernization-design.md`

## Global Constraints

- Library target: `net10.0` only; `LangVersion` `14`; `Nullable` enabled; `ImplicitUsings` enabled; `TreatWarningsAsErrors` true for `Simple.OData.Client` only.
- Library dependencies: `Microsoft.OData.Core` `9.0.0`, `Microsoft.OData.Edm` `9.0.0`, `Microsoft.Spatial` `9.0.0`, `Microsoft.SourceLink.GitHub` (PrivateAssets). No preview packages in the library.
- Package id `Simple.OData.Client`, version `7.0.0`, strong-named with `src/Simple.OData.Client.snk`, `IsTrimmable` false, `IsAotCompatible` false.
- Default wire version `OData-Version: 4.0`, `OData-MaxVersion: 4.01`; `ODataClientSettings.ProtocolVersion = ODataProtocolVersion.V401` switches both to `4.01`.
- Default `Accept: application/json;odata.metadata=minimal` (`MetadataLevel` changes the parameter; `Ieee754Compatible` appends `;IEEE754Compatible=true`); `$metadata` requests accept `application/xml` (or `application/json` when `MetadataFormat.Json`).
- Public async methods take `CancellationToken cancellationToken = default` as the last parameter and never offer a parallel overload without it.
- Collection results are `IReadOnlyList<T>`; untyped entries are `IDictionary<string, object?>`.
- Every `await` in the library uses `.ConfigureAwait(false)`.
- Commit after every task with a message prefixed `feat:`, `test:`, `chore:`, or `docs:`; never push before Task 23.
- Test and build commands run with `export PATH="/root/.dotnet:$PATH"` (the SDK is installed under `/root/.dotnet` in this environment).

## Review Focus

1. **Base URI with a path and a query string** (`https://host/svc/v2/?sap-client=100`): command URLs must keep the path and carry the base query along (`.../v2/Products?sap-client=100&$top=1`) — test added to Task 3.
2. **String literals with quotes and non-ASCII** (`x.Name == "O'Brien"`, `"Übergröße"`): `$filter=Name eq 'O''Brien'`, percent-encoded UTF-8 in the final URL — test added to Task 7.
3. **Responses with `odata.metadata=none`** (no `@odata.type`, no `@odata.context`): feeds and entries still materialize into `T` using the request's entity set type — test added to Task 12.
4. **Cancellation during the first `$metadata` download**: the call throws `OperationCanceledException`, nothing is cached, and the next call downloads again and succeeds — test added to Task 6.
5. **Concurrent first use** (20 parallel `FindEntriesAsync` on a fresh client): exactly one `$metadata` request is sent — test added to Task 6.

---

## Phase 1 — Repository skeleton

### Task 1: Remove legacy projects and create the .NET 10 solution skeleton

**Files:**
- Delete: `src/Simple.OData.Client.Core/`, `src/Simple.OData.Client.V3.Adapter/`, `src/Simple.OData.Client.V4.Adapter/`, `src/Simple.OData.Client/` (old metapackage), `src/Simple.OData.Samples.ApiV3/`, `src/Simple.OData.Samples.ApiV4/`, `src/Simple.OData.Tests.Samples.ApiV3/`, `src/Simple.OData.Tests.Samples.ApiV4/`, `src/Simple.OData.Tests.Shared.ActionProvider/`, `src/Simple.OData.Tests.Shared.NorthwindModel/`, `src/Simple.OData.Tests.Client/`, `src/Simple.OData.Tests.Client.Integration/`, `src/Simple.OData.Benchmarks.Client/`, `src/Simple.OData.Client.sln`, `MockData/`, `Simple.OData.Client.sln.vsspell`, `Simple.OData.Client.sln.vsspell.dic`
- Keep for later tasks (move, do not delete): `src/Simple.OData.Tests.Client/Resources/*` → `src/Simple.OData.Client.Tests/Resources/` (only V4 documents: `Northwind4.xml`, `Northwind4WithAlternateKeys.xml`, `TripPin.xml`, `DynamicsCRM.xml`, `ActionWithEnum.xml`, `ActionWithDerivedType.xml`, `ClientProductSku.xml`, `KeyDemo.xml`, `OpenTypes.xml`, `OpenTypeV401.json`, `WorkTaskModel.xml`, `WorkTaskModelWithNulls.xml`, `Russian.xml`, `AccountTasksAndLink.json`, `AccountTasksOnlyLink.json`, `ExampleActionComplexType.json`, `TwitterStatusesSample.txt`, the `.edmx` files whose root `Version` is `4.0`); `src/Simple.OData.Tests.Client/Entities/*.cs` → `src/Simple.OData.Client.Tests/Entities/`; `src/Simple.OData.Tests.Client.Integration/TripPinEntities.cs` → `src/Simple.OData.Client.Tests.Integration/TripPinEntities.cs`
- Create: `src/global.json` (`"version": "10.0.100"`, `"rollForward": "latestFeature"`), `src/Directory.Build.props`, `src/Directory.Packages.props`, `src/Simple.OData.Client.slnx`, `src/Simple.OData.Client/Simple.OData.Client.csproj`
- Modify: `.github/workflows/ci.yml`, `.github/workflows/pr-validation.yml`, `.github/workflows/release.yml` (runner `ubuntu-latest`, `dotnet-version: 10.0.x`, solution path `src/Simple.OData.Client.slnx`), `.gitignore` (add `TestResults/`, `BenchmarkDotNet.Artifacts/`, `*.received.*`)

**Interfaces:**
- Produces: `Directory.Build.props` with `TargetFramework=net10.0`, `LangVersion=14`, `Nullable=enable`, `ImplicitUsings=enable`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `Deterministic=true`, `ContinuousIntegrationBuild` when `GITHUB_ACTIONS`; `IsPrimaryProject` detection by name (`Simple.OData.Client` exactly) controlling `IsPackable`, `GenerateDocumentationFile`, `TreatWarningsAsErrors`, signing; `IsTestProject` detection by `.Tests` suffix adding `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `AwesomeAssertions`, `coverlet.collector`.
- Produces: `Directory.Packages.props` pinning: Microsoft.OData.Core/Edm/Spatial `9.0.0`; Microsoft.SourceLink.GitHub `8.0.0`; xunit.v3 `4.0.2`; xunit.runner.visualstudio `4.0.1`; Microsoft.NET.Test.Sdk `18.11.0`; AwesomeAssertions `9.6.0`; coverlet.collector latest `6.x`; Microsoft.AspNetCore.OData `10.0.0-preview.2`; Microsoft.OData.ModelBuilder `3.0.0`; Microsoft.AspNetCore.TestHost `10.0.14`; BenchmarkDotNet `0.15.8`.
- Produces: library csproj with `PackageId`, `Version 7.0.0`, `RootNamespace Simple.OData.Client`, `PackageReadmeFile README.md`, `InternalsVisibleTo` for `Simple.OData.Client.Tests` and `Simple.OData.Client.Benchmarks` (strong-name public key from the old props).

- [ ] **Step 1: Delete the legacy projects, solution, fixtures and spell-check files listed above with `git rm -r -q`; move the kept resources and entities with `git mv`**
- [ ] **Step 2: Write `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `Simple.OData.Client.slnx` (one project entry), and the library csproj containing a single placeholder `internal static class Placeholder` file so the project compiles**
- [ ] **Step 3: Verify the skeleton builds**

Run: `cd src && dotnet build Simple.OData.Client.slnx -c Release`
Expected: `Build succeeded.` with 0 warnings.

- [ ] **Step 4: Update the three workflow files and `.gitignore`**
- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: remove V1-V3 adapters, legacy test assets; add .NET 10 solution skeleton"
```

## Phase 2 — Core library

### Task 2: Test project scaffold and resource loader

**Files:**
- Create: `src/Simple.OData.Client.Tests/Simple.OData.Client.Tests.csproj` (references the library; embeds `Resources/**` as `EmbeddedResource`), `src/Simple.OData.Client.Tests/Infrastructure/Resources.cs`, `src/Simple.OData.Client.Tests/Infrastructure/FakeHttpHandler.cs`, `src/Simple.OData.Client.Tests/Infrastructure/MetadataClientFactory.cs`, `src/Simple.OData.Client.Tests/Unit/SmokeTests.cs`
- Modify: `src/Simple.OData.Client.slnx` (add project)

**Interfaces:**
- Produces: `internal static class Resources { static string Read(string name); static Stream Open(string name); }` (name is the file name, e.g. `"Northwind4.xml"`).
- Produces: `internal sealed class FakeHttpHandler : HttpMessageHandler` with `List<HttpRequestMessage> Requests`, `Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses`, `FakeHttpHandler Enqueue(HttpStatusCode status, string? jsonBody = null, string contentType = "application/json;odata.metadata=minimal", IDictionary<string,string>? headers = null)`, `FakeHttpHandler EnqueueMetadata(string resourceName)`; throws `InvalidOperationException("No response queued for {method} {uri}")` when the queue is empty. Captured request bodies are buffered into `string? LastRequestBody` and `List<string?> RequestBodies`.
- Produces: `internal static class MetadataClientFactory { static ODataClient Create(string metadataResource, Action<ODataClientSettings>? configure = null, FakeHttpHandler? handler = null); }` using `BaseUri = new Uri("http://localhost/" + metadataResource + "/")` and `MetadataDocument = Resources.Read(metadataResource)` (filled in by Task 6 once settings exist; this task creates the file with the resource and handler parts only).

- [ ] **Step 1: Write `SmokeTests.ResourcesAreEmbedded` asserting `Resources.Read("Northwind4.xml")` starts with `<?xml` or `<edmx:Edmx` and `Resources.Read("OpenTypeV401.json").Length > 0`**
- [ ] **Step 2: Run `dotnet test src/Simple.OData.Client.Tests` and confirm it fails (project or `Resources` missing)**
- [ ] **Step 3: Create the test csproj, `Resources.cs`, `FakeHttpHandler.cs`; add the project to the solution**
- [ ] **Step 4: Run `dotnet test src/Simple.OData.Client.Tests` — expected: 1 passed**
- [ ] **Step 5: Commit** `test: add xunit.v3 test project with embedded V4 resources and fake HTTP handler`

### Task 3: Name matching, pluralization, and URI/string utilities

**Files:**
- Create: `src/Simple.OData.Client/INameMatchResolver.cs`, `NameMatchResolvers.cs` (`ODataNameMatchResolver` statics `Strict`, `Alphanumeric`, `AlphanumericCaseInsensitive`, `NotStrict`; `ExactMatchResolver`, `BestMatchResolver`), `IPluralizer.cs`, `Pluralizer.cs`, `SimplePluralizer.cs`, `CachedPluralizer.cs`, `Pluralizers.cs`, `Internal/Homogenize.cs`, `Internal/StringExtensions.cs`, `Internal/Utils.cs`
- Test: `src/Simple.OData.Client.Tests/Unit/PluralizerTests.cs`, `Unit/NameMatchResolverTests.cs`, `Unit/UtilsTests.cs`

**Interfaces:**
- Produces: `public interface INameMatchResolver { bool IsMatch(string actualName, string requestedName); }`; `BestMatchResolver` matches homogenized equality or singular/plural equality via `Pluralizers.Cached`.
- Produces: `internal static class Homogenize { static string? Apply(this string? s) }` → lower-case, strips whitespace and punctuation (same regex `[\s\p{P}]`), memoized in a `ConcurrentDictionary<string,string>`.
- Produces: `internal static class Utils { static Uri CreateAbsoluteUri(Uri baseUri, string relativePath); static string ExtractCollectionName(string commandText); static string StreamToString(Stream, bool dispose = false); static Task<string> StreamToStringAsync(Stream, bool dispose, CancellationToken); static byte[] StreamToByteArray(Stream, bool dispose = false); static Stream StringToStream(string); static Exception NotSupportedExpression(Expression); static bool IsSystemType(Type); static T? BestMatch<T>(this IEnumerable<T>, Func<T,string>, string, INameMatchResolver) where T : class; static bool NamedKeyValuesMatchKeyNames(...); static bool NamedKeyValuesContainKeyNames(...) }` (the last two keep their current signatures with `IDictionary<string, object?>`).

- [ ] **Step 1: Write tests** — port `PluralizerTests` (all cases), `NameMatchResolverTests` (`Strict` rejects case difference; `Alphanumeric` accepts `Order_Details` vs `OrderDetails`; `NotStrict` accepts `Product` vs `Products`), `UtilsTests.CreateAbsoluteUri_KeepsBasePathAndMergesQuery` asserting `Utils.CreateAbsoluteUri(new Uri("https://host/svc/v2/?sap-client=100"), "Products?$top=1").AbsoluteUri == "https://host/svc/v2/Products?$top=1&sap-client=100"` (Review Focus 1), `UtilsTests.ExtractCollectionName` for `"Products(1)/Category"`, `"Products?$filter=..."`, absolute URL.
- [ ] **Step 2: Run the three test classes; expected compile failure**
- [ ] **Step 3: Implement the files; `SimplePluralizer` is ported verbatim (data tables unchanged) with `static readonly FrozenDictionary` tables**
- [ ] **Step 4: Run tests — expected all pass**
- [ ] **Step 5: Commit** `feat: add name matching, pluralizer and URI utilities`

### Task 4: Type system — descriptors, compiled accessors, converters, dictionary mapping

**Files:**
- Create: `src/Simple.OData.Client/ITypeCache.cs`, `ITypeConverter.cs`, `TypeSystem/TypeCache.cs`, `TypeSystem/TypeDescriptor.cs`, `TypeSystem/PropertyAccessor.cs`, `TypeSystem/TypeConverter.cs`, `TypeSystem/ValueConverter.cs`, `TypeSystem/DictionaryMapper.cs`, `TypeSystem/ExpressionActivator.cs`, `TypeSystem/MemberMapping.cs`, `TypeSystem/TypeExtensions.cs`, `ODataEntry.cs`, `ODataEntryAnnotations.cs`, `ODataFeedAnnotations.cs`, `ODataMediaAnnotations.cs`
- Test: `Unit/TypeSystem/TypeCacheTests.cs`, `Unit/TypeSystem/ValueConversionTests.cs`, `Unit/TypeSystem/DictionaryMapperTests.cs`, `Unit/TypeSystem/PropertyAccessorTests.cs`, `Unit/TypeSystem/TypeConverterTests.cs`

**Interfaces:**
- Produces: `public interface ITypeCache { ITypeConverter Converter { get; } INameMatchResolver NameMatchResolver { get; } void Register<T>(string dynamicContainerName = "DynamicProperties"); void Register(Type type, string dynamicContainerName = "DynamicProperties"); string? DynamicContainerName(Type type); PropertyInfo? GetAnnotationsProperty(Type type); IReadOnlyList<Type> GetDerivedTypes(Type type); IReadOnlyList<PropertyInfo> GetMappedProperties(Type type); PropertyInfo? GetMappedProperty(Type type, string propertyName); string GetMappedName(Type type); string GetMappedName(Type type, string propertyName); PropertyInfo? GetNamedProperty(Type type, string propertyName); bool IsAnonymousType(Type type); bool IsEnumType(Type type); bool IsValue(Type type); bool IsTypeAssignableFrom(Type type, Type otherType); bool TryConvert(object? value, Type targetType, out object? result); object? Convert(object? value, Type targetType); T? Convert<T>(object? value); IDictionary<string, object?> ToDictionary(object source); object? ToObject(IDictionary<string, object?> source, Type type, bool dynamicObject = false); T? ToObject<T>(IDictionary<string, object?>? source, bool dynamicObject = false) where T : class; }`
- Produces: `public sealed class TypeCache(ITypeConverter converter, INameMatchResolver nameMatchResolver) : ITypeCache`.
- Produces: `internal sealed class TypeDescriptor` with `Type Type`, `string MappedName`, `FrozenDictionary<string, PropertyAccessor> PropertiesByMappedName` (ordinal), `FrozenDictionary<string, PropertyAccessor> PropertiesByHomogenizedName`, `IReadOnlyList<PropertyAccessor> Properties`, `PropertyAccessor? AnnotationsProperty`, `string? DynamicContainerName`, `bool IsDynamicType`, `IReadOnlyList<Type> DerivedTypes`, `bool IsAnonymous`, `Func<object>? Activator`, `PropertyAccessor? Find(string name, INameMatchResolver resolver)`.
- Produces: `internal sealed class PropertyAccessor { PropertyInfo Property; string Name; string MappedName; Type PropertyType; bool CanWrite; object? GetValue(object instance); void SetValue(object instance, object? value); }` built with compiled `Expression` delegates (cached).
- Produces: `public interface ITypeConverter { void RegisterTypeConverter<T>(Func<IDictionary<string, object?>, T> converter); void RegisterTypeConverter(Type type, Func<IDictionary<string, object?>, object> converter); void RegisterTypeConverter<T>(Func<object, T> converter); void RegisterTypeConverter(Type type, Func<object, object> converter); bool HasDictionaryConverter(Type type); bool HasObjectConverter(Type type); object Convert(IDictionary<string, object?> value, Type type); object Convert(object value, Type type); T Convert<T>(IDictionary<string, object?> value); T Convert<T>(object value); }`
- Produces: `ODataEntry` (dictionary wrapper, indexer, `AsDictionary()`, explicit conversions), annotation classes as today but with `IReadOnlyList<object>? InstanceAnnotations` and `GetInstanceAnnotations<T>()`.

- [ ] **Step 1: Write tests**: `TypeCacheTests` (mapped names from `[Column(Name)]`, `[DataMember(Name)]`, `[JsonPropertyName]`; `[NotMapped]`/`[JsonIgnore]` excluded; derived types of `Transport` are `Ship`,`Truck` after `Register<Transport>()`; `DynamicContainerName` after `Register<Animal>("DynamicProperties")`), `ValueConversionTests` (port `TypeCacheValueConversionTests`: `DateTimeOffset→DateTime`, `string→Guid`, `int→enum`, `string→enum`, `byte[]↔string`, nullable targets, `DateTime→DateOnly`, `TimeSpan`), `DictionaryMapperTests` (port `DictionaryExtensionsTests`: nested complex, collections to `T[]`/`List<T>`/`IList<T>`/`HashSet<T>`, anonymous type, `ODataEntryAnnotations` property, `TypeWithIndexer`, open-type dynamic container receives unknown keys, derived type chosen from `__annotations.TypeName`), `PropertyAccessorTests` (get/set on class and struct properties, non-public setter), `TypeConverterTests` (port `TypeConverterCacheTests`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement the type system** — `DictionaryMapper.ToObject` uses `TypeDescriptor.Find` then `PropertyAccessor.SetValue` (no `PropertyInfo.SetValue`); anonymous types use the single constructor matched by parameter names; collections via cached `ExpressionActivator`.
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add type system with compiled accessors and dictionary mapping`

### Task 5: Metadata loading, indexing and resolution

**Files:**
- Create: `src/Simple.OData.Client/IMetadata.cs`, `EntityCollection.cs`, `EntryDetails.cs`, `UnresolvableObjectException.cs`, `MetadataFormat.cs`, `Metadata/MetadataLoader.cs`, `Metadata/EdmMetadataIndex.cs`, `Metadata/MetadataResolver.cs`, `Metadata/EdmModelCache.cs`, `Metadata/EdmExtensions.cs`
- Test: `Unit/Metadata/MetadataLoaderTests.cs`, `Unit/Metadata/MetadataResolverTests.cs`, `Unit/Metadata/EdmModelCacheTests.cs`

**Interfaces:**
- Produces: `public interface IMetadata` — current member set, with these changes: `IEnumerable<…>` → `IReadOnlyList<…>`, `GetFunctionReturnCollection`/`GetActionReturnCollection` return `EntityCollection?`, new `bool IsSingleton(string collectionName)`, `bool HasStream(string collectionName)`, `bool IsNavigationPropertyContained(string collectionName, string propertyName)`, `string GetNavigationPropertyTargetSetName(string collectionName, string propertyName)`, `IReadOnlyList<string> GetKeyPropertyNamesForUri(string collectionName)`; `ParseEntryDetails(string collectionName, IDictionary<string, object?> entryData, string? contentId = null)`.
- Produces: `public sealed record EntityCollection(string Name, EntityCollection? BaseEntityCollection = null) { public override string ToString() => Name; }`
- Produces: `public sealed class EntryDetails { IDictionary<string, object?> Properties; IDictionary<string, List<ReferenceLink>> Links; bool HasOpenTypeProperties; void AddProperty(...); void AddLink(...) }`, `public sealed record ReferenceLink(string LinkName, object? LinkData, string? ContentId)`.
- Produces: `internal static class MetadataLoader { static IEdmModel Parse(string document); static MetadataFormat Detect(string document); static IEdmModel ParseXml(string); static IEdmModel ParseJson(string); static string GetVersion(string document) }` — `Parse` throws `InvalidOperationException("Unsupported OData protocol version {v}")` for `Version` other than `4.0`/`4.01`.
- Produces: `internal sealed class EdmMetadataIndex(IEdmModel model, INameMatchResolver resolver)` with `bool TryFindEntitySet(string, out IEdmEntitySet)`, `TryFindSingleton`, `TryFindNavigationSource(string, out IEdmNavigationSource)`, `TryFindEntityType`, `TryFindComplexType`, `TryFindEnumType`, `TryFindSchemaType(string, out IEdmSchemaType)`, `TryFindFunction(string, out IEdmFunction)`, `TryFindAction(string, out IEdmAction)`, `TryFindStructuralProperty(IEdmStructuredType, string, out IEdmStructuralProperty)`, `TryFindNavigationProperty(IEdmStructuredType, string, out IEdmNavigationProperty)`, `IReadOnlyList<string> DeclaredKeyNames(IEdmEntityType)`, `IReadOnlyList<IReadOnlyList<string>> AlternateKeyNames(IEdmEntityType)`, `bool RequiresOptimisticConcurrency(IEdmVocabularyAnnotatable)`, `IReadOnlyList<IEdmEntityType> DerivedTypes(IEdmEntityType)`; built-in resolvers hit frozen maps keyed by exact, homogenized, singularized and pluralized names; other resolvers use a memoized linear scan.
- Produces: `internal sealed class MetadataResolver(EdmMetadataIndex index, IEdmModel model, ODataClientSettings-like options: INameMatchResolver, bool ignoreUnmappedProperties, bool unqualifiedNameCall) : IMetadata`.
- Produces: `internal static class EdmModelCache { static ValueTask<CachedEdmModel> GetOrAddAsync(string key, Func<CancellationToken, Task<string>> load, Func<string, IEdmModel> parse, CancellationToken ct); static CachedEdmModel GetOrAdd(string key, string document, Func<string, IEdmModel> parse); static void Clear(); static void Remove(string key); }`, `internal sealed record CachedEdmModel(string Key, string Document, IEdmModel Model)`; a failed `load` leaves no entry.
- Produces: `internal static class EdmExtensions` using C# 14 `extension(IEdmTypeReference t)` / `extension(IEdmEntityType t)` members: `IEdmEntityType? AsEntityType()`, `IEdmEntityType KeyOwner()` (walks base types), `string FullName()` wrappers used by writer/reader.

- [ ] **Step 1: Write tests**: `MetadataLoaderTests` (parses `Northwind4.xml`; parses a minimal CSDL JSON document string `{"$Version":"4.01","NS":{"P":{"$Kind":"EntityType","$Key":["Id"],"Id":{"$Type":"Edm.Int32"}},"C":{"$Kind":"EntityContainer","Ps":{"$Collection":true,"$Type":"NS.P"}}},"$EntityContainer":"NS.C"}`; rejects a `Version="3.0"` document), `MetadataResolverTests` (with `Northwind4WithAlternateKeys.xml`: `GetEntityCollectionExactName("products") == "Products"` under `NotStrict`, throws `UnresolvableObjectException` under `Strict`; `GetDeclaredKeyPropertyNames("Order_Details") == ["OrderID","ProductID"]`; alternate keys for `Products` include `["ProductName"]`-style set from the document; `GetDerivedEntityCollection(Transports,"Ships").Name=="Ships"`; `GetNavigationPropertyPartnerTypeName("Products","Category")=="Category"`; `IsNavigationPropertyCollection("Categories","Products")`; function lookup `GetFunctionFullName("PassThroughIntCollection")`; `ParseEntryDetails` splits properties/links and throws for unmapped unless ignored), `EdmModelCacheTests` (`GetOrAddAsync` calls `load` once for 20 concurrent callers; a throwing `load` leaves `Remove`-able nothing and a second call loads again).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** (`MetadataResolver` ports the V4 `Metadata.cs` logic onto the index; `GetCollectionPathSegments`, type-cast segments `/Ns.Type`, and `NavigateToCollection` behave as today).
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add CSDL XML/JSON loading, frozen metadata index and resolver`

### Task 6: Settings, session, HTTP connection

**Files:**
- Create: `src/Simple.OData.Client/ODataClientSettings.cs`, `ODataProtocolVersion.cs`, `ODataMetadataLevel.cs`, `BatchFormat.cs`, `BulkOperationMode.cs`, `ODataUpdateMethod.cs`, `BatchPayloadUriOption.cs`, `ValidationKinds.cs`, `ODataTrace.cs`, `WebRequestExceptionMessageSource.cs`, `ISession.cs`, `Session.cs`, `Http/HttpConnection.cs`
- Modify: `src/Simple.OData.Client.Tests/Infrastructure/MetadataClientFactory.cs` (now constructs `ODataClientSettings`; `ODataClient` arrives in Task 15 — until then expose `CreateSettings` and `CreateSession`)
- Test: `Unit/SettingsTests.cs`, `Unit/SessionTests.cs`

**Interfaces:**
- Produces: `public sealed class ODataClientSettings` with exactly the members in spec §5.3; constructors `()`, `(Uri baseUri, ICredentials? credentials = null)`, `(HttpClient httpClient, Uri? relativeUri = null, ICredentials? credentials = null)`; `BaseUri` getter composes `HttpClient.BaseAddress` + relative URI as today; `TypeCache` lazily created from `NameMatchResolver` (one per settings instance, not per URL); `internal ODataClientSettings Clone()`.
- Produces: `public interface ISession { ODataClientSettings Settings { get; } IMetadata Metadata { get; } IEdmModel Model { get; } ITypeCache TypeCache { get; } }`
- Produces: `internal sealed class Session : ISession, IDisposable { Session(ODataClientSettings settings); ValueTask InitializeAsync(CancellationToken ct); bool IsInitialized; string MetadataDocument; HttpConnection HttpConnection; void Trace(string format, params object?[] args); void ClearMetadataCache(); static Session FromMetadata(Uri baseUri, string metadataDocument); }` — `InitializeAsync` gates on a `SemaphoreSlim(1,1)`, uses `EdmModelCache.GetOrAddAsync(BaseUri.AbsoluteUri, …)`, builds `MetadataResolver`; when `Settings.MetadataModel` or `MetadataDocument` is set no HTTP call is made; the `$metadata` request appends `?$schemaversion=` when `SchemaVersion` is set and sends `Accept: application/xml` or `application/json` per `MetadataFormat`.
- Produces: `internal sealed class HttpConnection : IDisposable { HttpClient HttpClient; HttpConnection(ODataClientSettings settings) }` — external `HttpClient` is used as-is (not disposed); otherwise `SocketsHttpHandler { PooledConnectionLifetime = 2 min, AutomaticDecompression = All, EnableMultipleHttp2Connections = true, Credentials, PreAuthenticate = Credentials != null }` passed to `ConfigureHandler`, or `OnCreateMessageHandler()`; `Timeout` from `RequestTimeout` when > 0.

- [ ] **Step 1: Write tests**: `SettingsTests` (port `ClientSettingsTests`/`SettingsTests`: absolute `BaseUri` with `HttpClient.BaseAddress` throws; relative without base throws; `TypeCache` requires no `BaseUri` anymore; defaults: `ProtocolVersion==V4`, `MetadataLevel==Minimal`, `BatchFormat==Multipart`, `PreferredUpdateMethod==Patch`, `ReadUntypedAsString==true`), `SessionTests.InitializesFromMetadataDocumentWithoutHttp`, `SessionTests.ConcurrentInitializationSendsOneMetadataRequest` (20 parallel `InitializeAsync` on one session over `FakeHttpHandler.EnqueueMetadata("Northwind4.xml")`; `handler.Requests.Count == 1`) [Review Focus 5], `SessionTests.CancelledMetadataDownloadLeavesCacheEmpty` (handler delays until a `CancellationTokenSource` fires; `InitializeAsync` throws `OperationCanceledException`; a second `InitializeAsync` with a fresh handler succeeds) [Review Focus 4], `SessionTests.MetadataRequestUsesSchemaVersionAndJsonAccept`.
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add settings, session with cached metadata initialization, HTTP connection`

### Task 7: Expression tree and LINQ translation

**Files:**
- Create: `src/Simple.OData.Client/Expressions/ODataExpression.cs`, `ODataExpression.Operators.cs`, `ODataExpression.Linq.cs`, `ODataExpression.Format.cs`, `ExpressionFunction.cs`, `ExpressionContext.cs`, `FunctionMapping.cs`, `FunctionToOperatorMapping.cs`, `ColumnExpression.cs`, `ExpandExpression.cs`, `ComputeExpression.cs`, `src/Simple.OData.Client/ODataFunctions.cs`, `ODataExpandAssociation.cs`, `ODataExpandOptions.cs`, `ODataOrderByColumn.cs`, `Commands/ODataLiteral.cs`
- Test: `Unit/Expressions/TypedExpressionTests.cs`, `Unit/Expressions/ExpressionFormattingTests.cs`, `Unit/Expressions/ColumnExpressionTests.cs`, `Unit/Expressions/ExpandExpressionTests.cs`

**Interfaces:**
- Produces: `public class ODataExpression` (same public shape: `Reference`, `Value`, `Function`, `IsNull`, operators, implicit conversions, `AsString(ISession session)`, `public static ODataExpression FromLinqExpression(Expression)`), `public sealed class ODataExpression<T> : ODataExpression`.
- Produces: `internal sealed class ExpressionContext { ISession Session; EntityCollection? EntityCollection; string? ScopeQualifier; string? DynamicPropertiesContainerName; bool IsQueryOption; int NextLambdaIndex(); }` — lambda variables are `x1`, `x2`, … per root context (nested contexts share the root counter).
- Produces: `internal static class FunctionMapping` with frozen map `(string name, int argc) → mapping`; entries: `Contains→contains`, `StartsWith`, `EndsWith`, `Length`, `IndexOf`, `Replace`, `Substring(1|2)`, `ToLower`, `ToUpper`, `Trim`, `Concat`, `Year/Month/Day/Hour/Minute/Second`, `Millisecond→fractionalseconds`, `Date→date`, `TimeOfDay→time`, `Totaloffsetminutes`, `Now`, `Maxdatetime`, `Mindatetime`, `TotalSeconds→totalseconds`, `Round/Floor/Ceiling`, `IsMatch→matchesPattern` (from `Regex.IsMatch(input, pattern)`), `MatchesPattern`, `HasSubset→hassubset`, `HasSubsequence→hassubsequence`, `GeoDistance→geo.distance`, `GeoLength→geo.length`, `GeoIntersects→geo.intersects`.
- Produces: `public static class ODataFunctions { static bool MatchesPattern(string input, string pattern); static bool HasSubset<T>(IEnumerable<T> set, IEnumerable<T> subset); static bool HasSubsequence<T>(IEnumerable<T> sequence, IEnumerable<T> subsequence); static double GeoDistance(Geography a, Geography b); static double GeoLength(Geography line); static bool GeoIntersects(Geography a, Geography b); }` — bodies throw `InvalidOperationException("ODataFunctions are only valid inside OData expressions")`.
- Produces: `internal static class ComputeExpression { static IReadOnlyList<(string Alias, ODataExpression Expression)> Extract<T, TR>(Expression<Func<T, TR>> expression, ITypeCache typeCache) }` (NewExpression / MemberInitExpression members).
- Produces: `public sealed class ODataExpandAssociation`, `public sealed class ODataExpandOptions` (adds `string? RawOptions`, `int? Top`, `int? Skip`, `bool Count`, `string? Search`, `ODataExpression? Filter`, `IReadOnlyList<string> Select`, `IReadOnlyList<ODataOrderByColumn> OrderBy`, factory `ByValue(int levels = 1)`, `ByValue(ODataExpandLevels)`, `ByReference(...)`, fluent `With…` methods), `public sealed record ODataOrderByColumn(string Name, bool Descending)`.

- [ ] **Step 1: Write tests**: port `TypedExpressionTests` V4 cases (strings, numbers with/without suffix, `DateTime`→`DateTimeOffset` literal, `Guid` literal `guid'…'`→ V4 `…`, enums `NS.Type'Value'` and prefix-free, `in`, `not`, arithmetic, nested property paths, `any/all` lambda names reset per expression), `ExpressionFormattingTests.StringLiteralQuotesAreDoubledAndUnicodePercentEncoded` (`x => x.ProductName == "O'Brien"` → `ProductName eq 'O''Brien'`; `"Übergröße"` → literal retains text, and the Task 8 URL test asserts `%C3%9C`) [Review Focus 2], `ExpressionFormattingTests.LambdaVariablesAreNumberedPerExpression` (two independent expressions both produce `x1`), `ExpressionFormattingTests.FourOhOneFunctions` (`Regex.IsMatch(x.ProductName, "^Ch")` → `matchesPattern(ProductName,'^Ch')`; `ODataFunctions.HasSubset(x.Tags, new[]{"a"})` → `hassubset(Tags,['a'])`), `ColumnExpressionTests` (port), `ExpandExpressionTests` (port typed expand extraction incl. `Where/Select/OrderBy`, plus `Take(5)`→`Top`, `Skip(2)`→`Skip`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** — port the existing translator; remove `AdapterVersion` branches; `ConvertValueToUriLiteral` lives in Task 8, so `Format` calls `context.Session`-provided formatter through an internal `IValueFormatter` interface defined here (`internal interface IUriValueFormatter { string Format(object? value, bool escape); string FormatTypeName(string typeName); }`) and the tests use a small test implementation.
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add OData expression tree with V4.01 function mappings`

### Task 8: Commands and URI building

**Files:**
- Create: `src/Simple.OData.Client/Commands/FluentCommand.cs`, `Commands/CommandDetails.cs`, `Commands/ResolvedCommand.cs`, `Commands/CommandUriBuilder.cs`, `Commands/HttpLiteral.cs`, `Commands/RestVerbs.cs`
- Test: `Unit/Commands/CommandUriBuilderTests.cs`, `Unit/Commands/KeyResolutionTests.cs`

**Interfaces:**
- Produces: `public sealed class FluentCommand` (current builder members + `Index(long)`, `Compute(string)`, `Compute(IReadOnlyList<(string Alias, ODataExpression Expression)>)`, `Apply(object builderOrText)`, `WithIfMatch(string)`, `WithIfNoneMatch(string)`, `WithMaxPageSize(int)`, `TrackChanges()`, `Expand(string pathWithInlineOptions)` detects `(` and stores `RawOptions`); `internal CommandDetails Details`; `internal ResolvedCommand Resolve(Session session)`.
- Produces: `public sealed class ResolvedCommand { string Format(); EntityCollection? EntityCollection; string QualifiedEntityCollectionName; IReadOnlyDictionary<string, object?>? KeyValues; IReadOnlyDictionary<string, object?>? FilterAsKey; IDictionary<string, object?> CommandData; string? DynamicPropertiesContainerName; internal CommandDetails Details; }`.
- Produces: `internal sealed class CommandUriBuilder(Session session) : IUriValueFormatter { string Format(ResolvedCommand command); string FormatNavigationPath(EntityCollection collection, string path); string ConvertKeyValuesToUriLiteral(IReadOnlyDictionary<string, object?> key, bool skipKeyNameForSingleValue); string ConvertValueToUriLiteral(object? value, bool escapeDataString); }` — key segment honours `Settings.KeyAsSegment` (`Products/1`, compound keys still parenthesized), alternate keys always named; `$each` suffix when `Details.UseEachSegment`; nested expand options rendered as `Nav($filter=…;$select=…;$orderby=…;$top=…;$skip=…;$count=true;$search=…;$levels=…;$expand=…)` or raw; `$compute=expr as Alias,…`; `$index=`; `$apply=` from the aggregation builder (Task 10) or text; `$count=true`; `/$count` segment.

- [ ] **Step 1: Write tests** using `Session.FromMetadata(…, Resources.Read("Northwind4.xml"))` and `new FluentCommand()…Resolve(session).Format()`: `KeyResolutionTests` (port `KeyTests` V4 rows: single/compound/named/anonymous/entity keys; alternate key `Products(ProductName='Chai')`-style via `Northwind4WithAlternateKeys.xml`; key-as-segment `Products/1` and compound stays `Order_Details(OrderID=1,ProductID=2)`), `CommandUriBuilderTests` (filter escaping incl. `%C3%9C` for `Ü` and `%27%27` for a doubled quote; `$search`; `$index=3`; `$compute=UnitPrice%20mul%202%20as%20Doubled`; nested expand `Products($filter=…;$top=5;$count=true)`; raw expand passthrough; `$expand=*`, `*/$ref`, `$levels=max`; `$count=true`; `/$count`; `$each` for `UseEachSegment`; function with collection parameter alias `PassThroughIntCollection(numbers=@p1)?@p1=[1,2,3]`; derived collection `Transports/NorthwindModel.Ships`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** — single `StringBuilder`; `SearchValues<char>` for `!*'();:@&=+$,/?#[] `; port `FormatExpandSelectOrderby` merge logic from the V4 `CommandFormatter`.
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add command model and URI builder with 4.01 query options`

### Task 9: Dynamic API

**Files:**
- Create: `src/Simple.OData.Client/Dynamic/ODataDynamic.cs`, `Dynamic/DynamicODataExpression.cs`, `Dynamic/DynamicODataEntry.cs`
- Test: `Unit/Expressions/DynamicExpressionTests.cs`, `Unit/TypeSystem/DynamicEntryTests.cs`

**Interfaces:**
- Produces: `public static class ODataDynamic { static dynamic Expression { get; } static ODataExpression ExpressionFromReference(string); static ODataExpression ExpressionFromValue(object?); static ODataExpression ExpressionFromFunction(string functionName, string targetName, IEnumerable<object?> arguments); }`; `DynamicODataEntry` converts to `T` via `TypeCache.ToObject`.

- [ ] **Step 1: Write tests** — port `DynamicExpressionTests` V4 rows (member access, functions, `any/all`, `isof/is/cast/as`, enum comparison), `DynamicEntryTests` (port `DynamicContainerTests` conversion cases).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** (port; constructor lookups cached in `static readonly` fields).
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add dynamic expression and entry support`

### Task 10: Data aggregation builders (`$apply`)

**Files:**
- Create: `src/Simple.OData.Client/Aggregation/IDataAggregation.cs`, `IAggregationFunction.cs`, `DataAggregationBuilder.cs`, `DataAggregationClauses.cs`, `DynamicDataAggregation.cs`, `DynamicAggregationFunction.cs`, `ODataDynamicDataAggregation.cs`
- Test: `Unit/Aggregation/TypedDataAggregationTests.cs`, `Unit/Aggregation/DynamicDataAggregationTests.cs`

**Interfaces:**
- Produces (namespace `Simple.OData.Client`): `public interface IDataAggregation<T> where T : class { IDataAggregation<T> Filter(Expression<Func<T,bool>>); IDataAggregation<TR> Aggregate<TR>(Expression<Func<T, IAggregationFunction<T>, TR>>) where TR : class; IDataAggregation<TR> GroupBy<TR>(Expression<Func<T, IAggregationFunction<T>, TR>>) where TR : class; IDataAggregation<TR> Compute<TR>(Expression<Func<T, TR>>) where TR : class; IDataAggregation<T> TopCount(int count, Expression<Func<T, object>> property); IDataAggregation<T> BottomCount(int count, Expression<Func<T, object>> property); }`, `IAggregationFunction<T>` (Average, Sum, Min, Max, Count, CountDistinct), `DynamicDataAggregation` (Filter, Aggregate, GroupBy, Compute, TopCount, BottomCount), `ODataDynamicDataAggregation.Builder/AggregationFunction`.
- Produces: `internal abstract class DataAggregationBuilder { string Build(ResolvedCommand command, Session session); }` consumed by `CommandUriBuilder` (Task 8 already calls `Build` through an `internal interface IApplyBuilder { string Build(ResolvedCommand, Session); }` declared in Task 8).

- [ ] **Step 1: Write tests** — port `TypedDataAggregationTests` and `DynamicDataAggregationTests` (expected `$apply=` strings unchanged), add `Compute` (`compute(UnitPrice mul 2 as Doubled)`) and `TopCount` (`topcount(3,UnitPrice)`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: move $apply builders into core with compute/topcount/bottomcount`

### Task 11: Request serialization

**Files:**
- Create: `src/Simple.OData.Client/ODataRequest.cs`, `Serialization/ODataRequestMessage.cs`, `Serialization/ODataLibSettings.cs`, `Serialization/RequestWriter.cs`, `Serialization/EdmDeltaModel.cs`, `Serialization/ODataNamespace.cs`
- Test: `Unit/Serialization/RequestWriterTests.cs`, `Unit/Serialization/RequestWriterActionTests.cs`, `Unit/Serialization/ActionWithEnumTests.cs`

**Interfaces:**
- Produces: `public sealed class ODataRequest { HttpRequestMessage RequestMessage; string CommandText; string Method; IDictionary<string, object?>? EntryData; bool IsLink; bool ReturnsScalarResult; bool ResultRequired; bool CheckOptimisticConcurrency; string? IfMatch; string? IfNoneMatch; IDictionary<string,string> Headers; IReadOnlyList<string> Preferences; }` (internal constructors as today).
- Produces: `internal static class ODataLibSettings { static ODataMessageWriterSettings CreateWriterSettings(Session s, ODataFormat? format = null); static ODataMessageReaderSettings CreateReaderSettings(Session s); }` — `Version` from `ProtocolVersion`, `BaseUri`, `ODataUri`, `Validations`, `EnableMessageStreamDisposal=false`, reader `ArrayPool = ArrayPool<char>.Shared`, `ShouldIncludeAnnotation` from `IncludeAnnotationsInResults`, `ReadUntypedAsString`.
- Produces: `internal sealed class RequestWriter(Session session, Lazy<BatchWriter>? batchWriter)` with `Task<ODataRequest> CreateGetRequestAsync(string commandText, bool scalarResult, IDictionary<string,string>? headers = null)`, `CreatePutRequestAsync(string commandText, Stream stream, string? contentType, bool optimisticConcurrency, headers)`, `CreateInsertRequestAsync(string collection, string commandText, IDictionary<string, object?> entryData, bool resultRequired, headers)`, `CreateUpdateRequestAsync(string collection, string entryIdent, IReadOnlyDictionary<string, object?> entryKey, IDictionary<string, object?> entryData, bool resultRequired, headers)`, `CreateDeleteRequestAsync(string collection, string entryIdent, headers)`, `CreateLinkRequestAsync(...)`, `CreateUnlinkRequestAsync(...)`, `CreateFunctionRequestAsync(string commandText, string functionName, headers)`, `CreateActionRequestAsync(string commandText, string actionName, string? boundTypeName, IDictionary<string, object?>? parameters, bool resultRequired, headers)` — all `CancellationToken`-free (pure serialization). PATCH uses `EdmDeltaModel`; a 4.01 PATCH with nested navigation collections writes nested delta resource sets; nested entities in POST are written as nested resources (deep insert); navigation links written as `@odata.bind` via `ODataNestedResourceInfo` + `ODataEntityReferenceLink`.

- [ ] **Step 1: Write tests** — port `RequestWriterV4Tests` (PATCH vs PUT selection, key change forces PUT, `DateTime` not null, insert nested complex/collection, `@odata.bind` link in insert payload asserting the JSON contains `"Category@odata.bind":"Categories(1)"` or `[]` form, `ReorderLevel` etc.), `RequestWriterActionTests` (port: primitive/complex/collection/entity parameters), `ActionWithEnumTests` (port), new `InsertWritesDeepNestedEntities` (`Category` object with `Products` array → nested resource set in payload), new `PatchUnderV401UsesAtTypeAnnotations` (`ProtocolVersion=V401` payload uses `"@type"` not `"@odata.type"` when a type annotation is present).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** (port V4 `RequestWriter.cs` + `RequestWriterBase.cs`; remove payload-format branching; bodies written into a `MemoryStream` then wrapped in `StreamContent` with `Content-Length`).
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add ODataLib-based request writer`

### Task 12: Response deserialization

**Files:**
- Create: `src/Simple.OData.Client/ODataResponse.cs` (with `AnnotatedFeed`, `AnnotatedEntry`), `ODataDelta.cs`, `ODataErrorDetails.cs`, `Serialization/ODataResponseMessage.cs`, `Serialization/ResponseNode.cs`, `Serialization/ResponseReader.cs`
- Test: `Unit/Serialization/ResponseReaderTests.cs`, `Unit/Serialization/DeltaReaderTests.cs`, `Unit/Serialization/ErrorReaderTests.cs`

**Interfaces:**
- Produces: `public sealed class ODataResponse { int StatusCode; IReadOnlyDictionary<string, string> Headers; string? Location; string? ODataEntityId; string? ETag; AnnotatedFeed? Feed; IReadOnlyList<ODataResponse>? Batch; Exception? Exception; ODataErrorDetails? Error; IReadOnlyList<IDictionary<string, object?>> AsEntries(bool includeAnnotations); IDictionary<string, object?>? AsEntry(bool includeAnnotations); T? AsScalar<T>(); T[] AsArray<T>(); }` plus the `internal static From…` factories.
- Produces: `public sealed class ODataDelta { IReadOnlyList<IDictionary<string, object?>> Changed; IReadOnlyList<ODataDeletedEntry> Deleted; IReadOnlyList<ODataDeltaLink> AddedLinks; IReadOnlyList<ODataDeltaLink> DeletedLinks; Uri? DeltaLink; Uri? NextPageLink; }`, `public sealed record ODataDeletedEntry(string? Id, string? Reason, IDictionary<string, object?>? Data)`, `public sealed record ODataDeltaLink(Uri Source, string Relationship, Uri Target)`.
- Produces: `public sealed class ODataErrorDetails { string? Code; string? Message; string? Target; IReadOnlyList<ODataErrorDetails> Details; string? InnerError; }`.
- Produces: `internal sealed class ResponseReader(Session session) { Task<ODataResponse> ReadAsync(HttpResponseMessage response, CancellationToken ct); Task<ODataResponse> ReadAsync(IODataResponseMessageAsync message, CancellationToken ct); Task<ODataDelta> ReadDeltaAsync(HttpResponseMessage response, CancellationToken ct); Task<ODataErrorDetails?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct); }` — payload kind chosen from `Content-Type`/`DetectPayloadKind`; `metadata=none` payloads read with `ODataMessageReaderSettings` + expected `IEdmEntitySet` taken from the request (`ODataResponseMessage` carries `IEdmNavigationSource? ExpectedNavigationSource`).

- [ ] **Step 1: Write tests** — port `ResponseReaderV4Tests` (link annotations only/with data from `AccountTasksOnlyLink.json`/`AccountTasksAndLink.json`; `ExampleActionComplexType.json`), add `ResponseReaderTests.ReadsFeedWithMetadataNone` (handcrafted `{"value":[{"ProductID":1,"ProductName":"Chai"}]}` with `Content-Type: application/json;odata.metadata=none` and expected set `Products` → one entry with `ProductName=="Chai"`) [Review Focus 3], `ReadsSingleEntryWithEtagHeader`, `ReadsPrimitiveCollection` (`{"value":[1,2,3]}` → `AsArray<int>()`), `ReadsRawValue` (`text/plain` body `42` → `AsScalar<int>()`), `DeltaReaderTests.ReadsDeltaPayload` (handcrafted 4.01 delta JSON with one changed entity, one `@removed` entry, one `@delta` link add/remove, `@deltaLink`), `ErrorReaderTests.ParsesODataError` (`{"error":{"code":"400","message":"Bad","target":"ProductName","details":[{"code":"1","message":"d"}],"innererror":{"message":"inner"}}}`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** (port V4 `ResponseReader.cs`/`ResponseReaderBase.cs`; add delta reader using `ODataReaderState.DeletedResourceStart/DeltaLink/DeltaDeletedLink`; error via `ODataMessageReader.ReadError()`).
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add response reader with delta and error payload support`

### Task 13: Batch writer and `ODataBatch`

**Files:**
- Create: `src/Simple.OData.Client/Serialization/BatchWriter.cs`, `src/Simple.OData.Client/ODataBatch.cs`
- Test: `Unit/Serialization/BatchWriterTests.cs`

**Interfaces:**
- Produces: `internal sealed class BatchWriter(Session session, ConcurrentDictionary<object, IDictionary<string, object?>> batchEntries) { Task StartBatchAsync(); Task<HttpRequestMessage> EndBatchAsync(); Task<IODataRequestMessageAsync> CreateOperationMessageAsync(Uri uri, string method, string? collection, IDictionary<string, object?>? entryData, bool resultRequired); bool HasOperations; int LastOperationId; string? GetContentId(IDictionary<string, object?> entryData, object? linkData); void MapContentId(...); Task<ODataRequest?> CreateBatchRequestAsync(IODataClient client, IReadOnlyList<Func<IODataClient, Task>> actions, List<int> responseIndexes, IDictionary<string,string>? headers); }` — `BatchFormat.Json` sets the batch request `Content-Type: application/json` and `OData-Version: 4.01` so ODataLib emits JSON batch (`{"requests":[…]}` with `atomicityGroup`); multipart otherwise.
- Produces: `public sealed class ODataBatch { ODataBatch(Uri baseUri); ODataBatch(ODataClientSettings settings); ODataBatch(IODataClient client, bool reuseSession = false); static ODataBatch operator +(ODataBatch, Func<IODataClient, Task>); Task ExecuteAsync(CancellationToken ct = default); ODataBatch WithHeader(string, string); ODataBatch WithHeaders(IDictionary<string,string>); }` (execution wiring lands in Task 15).

- [ ] **Step 1: Write tests** — port `RequestWriterBatchTests` (multipart payload contains `--batch_`, `--changeset_`, `Content-ID: 1`, `$1` reference for a linked entry inserted in the same changeset), new `JsonBatchPayloadContainsRequestsArrayAndAtomicityGroup`.
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add multipart and JSON batch writer`

### Task 14: HTTP request runner, preferences, errors, async monitor

**Files:**
- Create: `src/Simple.OData.Client/Http/RequestRunner.cs`, `Http/PreferHeader.cs`, `Http/AsyncMonitor.cs`, `src/Simple.OData.Client/WebRequestException.cs`
- Test: `Unit/Http/RequestRunnerTests.cs`, `Unit/Http/AsyncMonitorTests.cs`

**Interfaces:**
- Produces: `internal sealed class RequestRunner(Session session) { Task<HttpResponseMessage> ExecuteRequestAsync(ODataRequest request, CancellationToken ct); }` — sets `OData-Version`/`OData-MaxVersion`, `Accept` (per Global Constraints; raw `$value`/stream requests send no OData accept), `Prefer` composed by `PreferHeader.Compose(settings, request)` from `return=representation|minimal`, `odata.maxpagesize=N`, `odata.track-changes`, `odata.include-annotations="…"`, `omit-values=nulls`, `odata.continue-on-error`, `respond-async`; `If-Match` from `request.IfMatch` or `*` when `CheckOptimisticConcurrency`; `If-None-Match`; runs `BeforeRequest(Async)`/`AfterResponse(Async)`; semaphore gate when `MaxConcurrentRequests > 0`; uses `HttpCompletionOption.ResponseHeadersRead`; non-success → `WebRequestException.CreateFromResponseMessageAsync` (includes parsed `ODataErrorDetails`); `202 Accepted` with `Location` → `AsyncMonitor.WaitForCompletionAsync`.
- Produces: `internal sealed class AsyncMonitor(Session session) { Task<HttpResponseMessage> WaitForCompletionAsync(HttpResponseMessage accepted, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, CancellationToken ct); }` — polls `Location` honouring `Retry-After` (seconds) else `Settings.AsyncPollingInterval` via `Settings.TimeProvider`; a final `200` with `Content-Type: application/http` is unwrapped with `ODataMessageReader.CreateODataAsynchronousReader()` into a new `HttpResponseMessage`.
- Produces: `public sealed class WebRequestException : Exception { HttpStatusCode Code; string? ReasonPhrase; string? Response; Uri? RequestUri; ODataErrorDetails? Error; static Task<WebRequestException> CreateFromResponseMessageAsync(HttpResponseMessage, WebRequestExceptionMessageSource, CancellationToken); static WebRequestException CreateFromStatusCode(HttpStatusCode, WebRequestExceptionMessageSource, string? responseContent = null, ODataErrorDetails? error = null); }`.

- [ ] **Step 1: Write tests** with `FakeHttpHandler`: `SendsODataVersionAndAcceptHeaders` (defaults; `V401`; `MetadataLevel.Full`; `Ieee754Compatible`), `ComposesPreferHeader` (`MaxPageSize=10`, `TrackChanges`, `OmitNullValues`, `IncludeAnnotations="*"` → `Prefer: return=representation, odata.maxpagesize=10, odata.track-changes, omit-values=nulls, odata.include-annotations="*"`), `IfMatchFromRequestOverridesStar`, `ThrowsWebRequestExceptionWithParsedError` (400 with OData error body → `ex.Error.Code=="400"`, `ex.Code==BadRequest`), `HonoursMaxConcurrentRequests` (handler asserts in-flight ≤ 2), `AsyncMonitorTests.PollsUntilCompleteAndUnwrapsApplicationHttp` (202+Location, then 202 with `Retry-After: 1`, then 200 `application/http` wrapping a `200 application/json` feed; uses `FakeTimeProvider`-style manual `TimeProvider`).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run tests — expected pass**
- [ ] **Step 5: Commit** `feat: add request runner with 4.01 preferences, ETags, error details and async monitor`

### Task 15: `ODataClient` and the fluent API

**Files:**
- Create: `src/Simple.OData.Client/IODataClient.cs`, `ODataClient.cs`, `ODataClient.Entries.cs`, `ODataClient.Operations.cs`, `ODataClient.Internals.cs`, `Fluent/IFluentClient.cs`, `Fluent/IBoundClient.cs`, `Fluent/IUnboundClient.cs`, `Fluent/IMediaClient.cs`, `Fluent/IRequestBuilder.cs`, `Fluent/IClientWithRequest.cs`, `Fluent/IClientWithResponse.cs`, `Fluent/FluentClientBase.cs`, `Fluent/BoundClient.cs`, `Fluent/BoundClient.Execution.cs`, `Fluent/UnboundClient.cs`, `Fluent/MediaClient.cs`, `Fluent/RequestBuilder.cs`, `Fluent/ClientWithRequest.cs`, `Fluent/ClientWithResponse.cs`
- Modify: `ODataBatch.cs` (execute via `ODataClient.ExecuteBatchAsync`), `Infrastructure/MetadataClientFactory.cs` (returns `ODataClient`)
- Test: `Unit/Fluent/CommandTextTests.cs` (ported `ExpansionTests`, `AggregationTests` any/all, `TypedFilterAsKeyTests`, `DynamicFilterAsKeyTests`, `OpenTypesTests`, `ODataExpandAssociationTests` — V4 rows only), `Unit/Fluent/ClientWithFakeHandlerTests.cs`

**Interfaces:**
- Produces: `public interface IODataClient` and `public sealed class ODataClient : IODataClient, IDisposable` exactly as spec §5.1; `internal Session Session`; `internal Task ExecuteBatchAsync(IReadOnlyList<Func<IODataClient, Task>> actions, IDictionary<string,string> headers, CancellationToken ct)`; batch-mode and response-mode internal constructors as today.
- Produces: `IBoundClient<T>` per spec §5.2 including `IAsyncEnumerable<T> FindEntriesAsAsyncEnumerable(CancellationToken ct = default)` (follows `NextPageLink` until null), `UpdateEntriesAsync`/`DeleteEntriesAsync` honouring `BulkOperationMode.Each` (single `PATCH`/`DELETE` to `Set/$each?$filter=…`), `Task<ODataDelta> FindDeltaAsync(Uri deltaLink, CancellationToken ct = default)` on `IODataClient`.
- Produces: `FluentClientBase<T, FT>` generic `WithIfMatch`, `WithIfNoneMatch`, `WithMaxPageSize`, `TrackChanges`, `Compute(...)`, `Index(...)`, `Apply(...)`.

- [ ] **Step 1: Write tests** — `CommandTextTests` (port all V4 `[InlineData("Northwind4.xml", …)]` rows from `ExpansionTests`, `AggregationTests`, `TypedFilterAsKeyTests`, `DynamicFilterAsKeyTests`, `OpenTypesTests`, `ODataExpandAssociationTests` through `client.For<T>()…GetCommandTextAsync()`), `ClientWithFakeHandlerTests`: `FindEntriesAsAsyncEnumerableFollowsNextLink` (two queued pages with `@odata.nextLink` → 3 items, 2 requests), `FindEntriesWithAnnotationsSetsCountAndNextLink`, `InsertEntryWithResultRequiredReturnsNullOn204`, `DeleteEntriesWithEachModeSendsSingleDelete` (`BulkOperationMode.Each` → one `DELETE …/Products/$each?$filter=…`), `UpdateEntriesWithEachModeSendsSinglePatch`, `FindDeltaAsyncReadsDelta`, `IgnoreResourceNotFoundReturnsNull`, `BatchExecutesViaSinglePost` (actions → one `POST …/$batch`; responses from a canned multipart body).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement** — port `ODataClient*.cs`, `BoundClient*.cs`, `FluentClientBase.cs`, `RequestBuilder.cs`, `ClientWith*.cs` onto the new internals; collapse overloads.
- [ ] **Step 4: Run the whole test project — expected all pass; run `dotnet build src/Simple.OData.Client -warnaserror` — expected 0 warnings**
- [ ] **Step 5: Commit** `feat: add ODataClient and fluent API with paging, $each, delta`

## Phase 4 — In-process OData V4 server and end-to-end tests

### Task 16: Test server project

**Files:**
- Create: `src/Simple.OData.Client.Tests.TestServer/Simple.OData.Client.Tests.TestServer.csproj` (`Microsoft.NET.Sdk.Web`, `OutputType Library`, `FrameworkReference Microsoft.AspNetCore.App`, packages `Microsoft.AspNetCore.OData`, `Microsoft.OData.ModelBuilder`, `Microsoft.AspNetCore.TestHost`), `NorthwindTestServer.cs`, `NorthwindEdmModel.cs`, `Data/NorthwindData.cs`, `Models/*.cs` (Category, Product, Supplier, Employee, Order, OrderDetail, Customer, Address (complex), AddressType (enum), Transport (abstract) / Ship / Truck, TypeWithGuidKey, Animal (open type, `IDictionary<string, object> DynamicProperties`), Picture (media entity: `Id`, `Name`, `Content` stream)), `Controllers/*.cs` (one `ODataController` per set with `[EnableQuery]` GET, GET by key, POST, PATCH, PUT, DELETE, navigation GETs, `$ref` POST/PUT/DELETE, `$count`, `$value`/stream for `Pictures`), `Controllers/OperationsController.cs` (functions/actions: unbound `PassThroughIntCollection(numbers)`, `GetProductsByRating(rating)`, bound `Products/Default.MostExpensive()`, `Products(1)/Default.Rate(rating)`, `Movies`-style `CheckOut`/`Return` on `Products`, action with complex parameter `UpdateAddress(address)`, action with entity collection parameter, action returning enum, action with enum parameter), `Infrastructure/ETagMiddleware.cs` (adds/validates `ETag`/`If-Match` on `Products` using `Core.OptimisticConcurrency` annotation on `RowVersion`), `Infrastructure/AsyncMonitorEndpoint.cs` (`GET /odata/Products/Slow` → when `Prefer: respond-async` present returns `202` + `Location: /odata/$monitor/{id}`; the monitor returns `202` once then `200 application/http` wrapping the JSON feed), `Infrastructure/DeltaEndpoint.cs` (`GET /odata/Products/Changes` returning `DeltaSet<Product>` with a changed entity, a deleted entity and a `@deltaLink`; with `Prefer: odata.track-changes` the normal `Products` feed gets a `@odata.deltaLink` pointing at `Products/Changes`).
- Modify: `src/Simple.OData.Client.slnx`, `src/Simple.OData.Client.Tests/Simple.OData.Client.Tests.csproj` (reference test server), create `src/Simple.OData.Client.Tests/Infrastructure/TestServerFixture.cs`, `Infrastructure/ServerTestBase.cs`
- Test: `src/Simple.OData.Client.Tests/Server/SmokeServerTests.cs`

**Interfaces:**
- Produces: `public sealed class NorthwindTestServer : IAsyncDisposable { static Task<NorthwindTestServer> StartAsync(); HttpClient CreateClient(); HttpMessageHandler Handler; Uri BaseAddress /* http://localhost/odata/ */; void ResetData(); }` — the model is exposed at `/odata`, `$metadata` as CSDL XML and JSON (`?$format=application/json`), key-as-segment routing enabled, `EnableQueryFeatures(maxTop: null)`, `$search` via a custom `ISearchBinder` on `ProductName`/`CompanyName`, `$compute`/`$apply` enabled, JSON batch and multipart batch via `ODataBatchHandler`, singleton `Company`, contained set `Customers/Addresses`, alternate key `Products(ProductName=…)`.
- Produces: seed counts used by assertions: Categories 8; Products 22 (CategoryID 1 "Beverages": 2 products, CategoryID 2 "Condiments": 7, product 1 is `Chai`, UnitPrice 18); Employees 9 with `ReportsTo` chain (employee 2 is superior of 1,3,4,5,8); Customers 5 (`ALFKI` first); Orders 6; Order_Details 12; Suppliers 3; Transports: 2 Ships + 1 Truck; TypeWithGuidKey 2; Animals 2; Pictures 1 (`Content` = `"image-bytes"`).
- Produces: `internal sealed class TestServerFixture : IAsyncLifetime { NorthwindTestServer Server; }` registered as `[CollectionDefinition("Server")]`; `internal abstract class ServerTestBase(TestServerFixture fixture) { ODataClient CreateClient(Action<ODataClientSettings>? configure = null); }` using `new ODataClientSettings(fixture.Server.CreateClient())` with `BaseUri = "http://localhost/odata/"`, `IgnoreResourceNotFoundException = true`; constructor calls `fixture.Server.ResetData()`.

- [ ] **Step 1: Write `SmokeServerTests`**: `MetadataIsServed` (`GET $metadata` 200, contains `EntityContainer`), `ProductsFeedHasTwentyTwoEntries` via raw `HttpClient`, `ClientReadsMetadataAndProducts` via `ODataClient` (`FindEntriesAsync("Products")` count 22).
- [ ] **Step 2: Run; expected compile failure**
- [ ] **Step 3: Implement the server project, fixture and base class**
- [ ] **Step 4: Run `dotnet test --filter FullyQualifiedName~SmokeServerTests` — expected pass**
- [ ] **Step 5: Commit** `test: add in-process ASP.NET Core OData V4.01 Northwind test server`

### Task 17: Query end-to-end tests (typed, dynamic, untyped)

**Files:**
- Test: `Server/FindTests.cs`, `Server/FindTypedTests.cs`, `Server/FindDynamicTests.cs`, `Server/ClientReadOnlyTests.cs`

- [ ] **Step 1: Port scenarios** from the old `FindTests`, `FindTypedTests`, `FindDynamicTests`, `ClientReadOnlyTests` (filter, filter string, get by key, non-existing, skip/top, orderby (+ expanded), select (single/multiple/homogenized), expand (one/many/second level/collection/with select), count/filter count/total count, combine all, navigate to single/multiple/recursive, base/derived class entries with/without annotations, `isof`/cast, `any`/`all`, string functions, date functions, `in`, open type dynamic properties, Guid key, compound key, alternate key, singleton, contained navigation, `$search`, enum filter) — expectations use the seed counts of Task 16.
- [ ] **Step 2: Run; fix client defects found; expected all pass**
- [ ] **Step 3: Commit** `test: port query scenarios to the in-process server`

### Task 18: Modification end-to-end tests

**Files:**
- Test: `Server/InsertTests.cs`, `Server/InsertTypedTests.cs`, `Server/InsertDynamicTests.cs`, `Server/UpdateTests.cs`, `Server/UpdateTypedTests.cs`, `Server/UpdateDynamicTests.cs`, `Server/DeleteTests.cs`, `Server/DeleteTypedTests.cs`, `Server/DeleteDynamicTests.cs`, `Server/LinkTests.cs`, `Server/LinkTypedTests.cs`, `Server/LinkDynamicTests.cs`, `Server/ClientReadWriteTests.cs`

- [ ] **Step 1: Port scenarios** (insert with value conversion / autogenerated id / expando / with category by id / by association / deep insert / derived `Ship` / complex `Address` / open type; update by key / by filter / multiple with and without result / by object as key / date / single association add,update,remove / multiple associations / derived; delete by key / by filter / by object key / derived; link / unlink; `ClientReadWriteTests` untyped API) plus `UpdateWithIfMatchStaleEtagFails` (412 → `WebRequestException` with `Code == PreconditionFailed`) and `InsertReturnsEntryWithAnnotationsAndEtag`.
- [ ] **Step 2: Run; fix defects; expected all pass**
- [ ] **Step 3: Commit** `test: port modification scenarios to the in-process server`

### Task 19: Operations and media end-to-end tests

**Files:**
- Test: `Server/FunctionTests.cs`, `Server/ActionTests.cs`, `Server/MediaTests.cs`

- [ ] **Step 1: Port/write scenarios**: functions with string/int/long/DateTime/Guid parameters, collection parameters (single/multiple/empty), complex parameter, scalar result, array result, entity collection result, bound function `MostExpensive`, unbound `Unbound().Function(...)`; actions: no result, scalar, single entity, enumerable, enum parameter and enum result, entity collection parameter, complex parameter, bound action `Rate`, action in batch; media: `GetStreamAsync`/`GetStreamAsStringAsync`/`SetStreamAsync` on `Pictures(1)`, stream property via `Media("Content")`, `WithMedia` enrichment.
- [ ] **Step 2: Run; fix defects; expected all pass**
- [ ] **Step 3: Commit** `test: add function, action and media end-to-end tests`

### Task 20: Batch, headers and request-builder end-to-end tests

**Files:**
- Test: `Server/BatchTests.cs`, `Server/BatchTypedTests.cs`, `Server/BatchDynamicTests.cs`, `Server/JsonBatchTests.cs`, `Server/WithHeaderTests.cs`, `Server/RequestBuilderTests.cs`

- [ ] **Step 1: Port/write scenarios**: success, empty batch, read-only batch, nested batches, success with results, partial failures (exception surfaced, earlier results assigned), all failures, multiple updates single batch, update+delete, insert/update/delete separate batches, insert with single/multiple associations in one changeset (`Content-ID` reference), delete non-existing (ignored), update with result required, function in batch, `ContinueOnError`; `JsonBatchTests` repeats success/results/partial failure/content-id with `BatchFormat.Json`; `WithHeaderTests` (custom header reaches server for find/insert/update/delete/link/function/batch); `RequestBuilderTests` (`BuildRequestFor().FindEntriesAsync()` → `RunAsync()` → `ReadAsCollectionAsync()`; `FromResponse`).
- [ ] **Step 2: Run; fix defects; expected all pass**
- [ ] **Step 3: Commit** `test: add batch, header and request builder end-to-end tests`

### Task 21: OData 4.01 feature end-to-end tests

**Files:**
- Test: `Server/PagingTests.cs`, `Server/ComputeTests.cs`, `Server/ApplyTests.cs`, `Server/EachTests.cs`, `Server/DeltaTests.cs`, `Server/AsyncRequestTests.cs`, `Server/KeyAsSegmentTests.cs`, `Server/MetadataJsonTests.cs`, `Server/ErrorTests.cs`, `Server/PreferenceTests.cs`
- Modify: client code as defects surface (no new public API).

- [ ] **Step 1: Write scenarios**: `PagingTests` (`WithMaxPageSize(5)` → `annotations.NextPageLink` set, `FindEntriesAsAsyncEnumerable` yields 22, `annotations.Count == 22` with `WithCount()`); `ComputeTests` (`Compute(x => new { Doubled = x.UnitPrice * 2 })` + `Select` → value present in dictionary result); `ApplyTests` (groupby count per category == 8 groups, aggregate sum, filter/groupby chain, dynamic builder, string `$apply`); `EachTests` (`BulkOperationMode.Each`: update 2 Beverages, delete 7 Condiments → counts); `DeltaTests` (`TrackChanges()` → `DeltaLink` not null; `FindDeltaAsync` → `Changed.Count==1`, `Deleted.Count==1`); `AsyncRequestTests` (`RespondAsync=true`, `AsyncPollingInterval=1ms` → `FindEntriesAsync("Products/Slow")` returns 22); `KeyAsSegmentTests` (client `KeyAsSegment=true` → `FindEntryAsync` by key works and the captured request path is `/odata/Products/1`); `MetadataJsonTests` (`MetadataFormat.Json` → `GetMetadataAsync()` model has entity set `Products`); `ErrorTests` (invalid filter → `WebRequestException` with `Error.Code` and `Error.Message` populated; 404 without ignore → exception); `PreferenceTests` (`OmitNullValues` round-trips an entry with null property; `IncludeAnnotations="*"` + `IncludeAnnotationsInResults` → `__annotations` present; `MetadataLevel.Full` → `@odata.type` visible when annotations included).
- [ ] **Step 2: Run; fix defects; expected all pass**
- [ ] **Step 3: Run the full suite: `dotnet test src/Simple.OData.Client.slnx` — expected all pass**
- [ ] **Step 4: Commit** `test: add OData 4.01 feature end-to-end tests`

## Phase 6 — Benchmarks, live tests, docs, CI

### Task 22: Live integration tests (opt-in) and benchmarks

**Files:**
- Create: `src/Simple.OData.Client.Tests.Integration/Simple.OData.Client.Tests.Integration.csproj`, `LiveFactAttribute.cs` (skips unless `ODATA_LIVE_TESTS=1`), `NorthwindLiveTests.cs` (read-only V4 Northwind: find/filter/expand/count/function-free), `ODataServiceLiveTests.cs` (`V4/OData/(S(readwrite))/OData.svc`: insert/update/delete/batch), `TripPinLiveTests.cs` (paging with annotations, `$apply` groupby, external `HttpClient`), `TripPinEntities.cs` (moved in Task 1)
- Create: `src/Simple.OData.Client.Benchmarks/Simple.OData.Client.Benchmarks.csproj`, `Program.cs`, `Resources/TripPin.xml`, `Resources/TripPin_result_20.json`, `FeedMaterializationBenchmarks.cs` (typed/untyped/dynamic over a canned 20-person feed via `FakeHttpHandler`-style handler), `UriBuildingBenchmarks.cs` (filter+expand+select command text), `ExpressionTranslationBenchmarks.cs`
- Modify: `src/Simple.OData.Client.slnx`

- [ ] **Step 1: Create both projects; `dotnet build` both**
- [ ] **Step 2: Run `dotnet test src/Simple.OData.Client.Tests.Integration` — expected all skipped; with `ODATA_LIVE_TESTS=1` run once and record pass/fail counts in the commit message (network permitting)**
- [ ] **Step 3: Run `dotnet run -c Release --project src/Simple.OData.Client.Benchmarks -- --filter '*' --job short` — expected a results table; paste the summary into `docs/benchmarks.md`**
- [ ] **Step 4: Commit** `test: add opt-in live integration tests and BenchmarkDotNet suite`

### Task 23: Documentation, release notes, CI verification, push

**Files:**
- Modify: `README.md` (7.0 overview, quick start, feature list, settings table, migration from 6.x per spec §12, remove the maintenance notice), `RELEASE_NOTES.md` (7.0.0 entry), `src/Simple.OData.Client/README.md` (package readme), `.github/workflows/*.yml` (verify), `docs/benchmarks.md`

- [ ] **Step 1: Write the documentation**
- [ ] **Step 2: Full verification: `dotnet build src/Simple.OData.Client.slnx -c Release -warnaserror:CS,CA` for the library; `dotnet test src/Simple.OData.Client.slnx -c Release`; `dotnet pack src/Simple.OData.Client -c Release -o /tmp/pkg` — expected: build succeeded, all tests pass, `Simple.OData.Client.7.0.0.nupkg` produced**
- [ ] **Step 3: Commit** `docs: document 7.0 modernization and migration`
- [ ] **Step 4: Push** `git push -u origin xver1` (retry with backoff on network errors only)

---

## Self-review notes

- Spec coverage: §4.1 layout → Tasks 1, 2, 16, 22; §4.4 metadata → Task 5; §4.5 types → Task 4; §5.1–5.4 API → Tasks 6, 11–15; §6 matrix rows → Tasks 7, 8, 10–15, 17–21 (each row names its test task); §7 performance → Tasks 4, 5, 7, 8, 11, 14, 22; §8 conventions → Task 1 props + every task; §9 errors → Tasks 12, 14, 15, 21; §10 testing → Tasks 2, 16–22; §11 CI/packaging → Tasks 1, 23; §12 migration → Task 23.
- Type consistency: `Session`, `EdmMetadataIndex`, `MetadataResolver`, `CommandUriBuilder`, `IUriValueFormatter`, `IApplyBuilder`, `RequestWriter`, `ResponseReader`, `BatchWriter`, `RequestRunner`, `AsyncMonitor`, `TypeCache`, `TypeDescriptor`, `PropertyAccessor`, `DictionaryMapper`, `FakeHttpHandler`, `NorthwindTestServer`, `TestServerFixture`, `ServerTestBase` are named identically wherever referenced.
- Review Focus items 1–5 are pinned to Tasks 3, 7/8, 12, 6, 6 respectively.
