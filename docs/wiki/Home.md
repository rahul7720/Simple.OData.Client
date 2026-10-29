# Simple.OData.Client 7

Simple.OData.Client is a metadata-driven **OData V4 / V4.01** client for **.NET 10**. It reads the service metadata once and lets you query and modify any OData service through dictionaries, your own classes or `dynamic`, without generating proxy classes.

The NuGet package is **`Nexus.Odata.Client`** (published to GitHub Packages); the assembly and namespaces remain `Simple.OData.Client`.

```csharp
using Simple.OData.Client;

var client = new ODataClient(new Uri("https://services.odata.org/V4/Northwind/Northwind.svc/"));
var cheap = await client.For<Product>().Filter(p => p.UnitPrice < 10).OrderBy(p => p.ProductName).FindEntriesAsync();
```

## Where to start

| If you want to… | Read |
|---|---|
| Install the package and run a first query | [[Getting Started]] |
| Choose between dictionaries, POCOs and `dynamic` | [[Typed, Dynamic and Untyped APIs]] |
| Filter, select, expand, sort and page | [[Querying Data]] |
| Insert, update, delete and link entities | [[Modifying Data]] |
| Call functions and actions | [[Functions and Actions]] |
| Group, aggregate and compute | [[Aggregation and Computed Properties]] |
| Page through large sets and track changes | [[Paging, Counts and Change Tracking]] |
| Send several operations in one request | [[Batches]] |
| Read and write media and stream properties | [[Media and Streams]] |
| Set headers, preferences, inspect requests, use metadata | [[Advanced Requests]] |
| Handle errors | [[Error Handling]] |
| Tune the client | [[Configuration]] |
| Move from 6.x | [[Migrating from 6.x]] |
| Run the tests and benchmarks | [[Testing and Benchmarks]] |
| Cut a release | [[Releasing]] |

## What the library supports

- **Querying:** `$filter` with the full 4.01 function set (`in`, `has`, `any`/`all`, `isof`/`cast`, `matchesPattern`, `hassubset`, geo functions), `$select`, `$expand` with nested options and `$levels`, `$orderby`, `$top`/`$skip`, `$count`, `$search`, `$index`, `$compute`, `$apply`.
- **Keys:** single, compound, named, alternate keys, key-as-segment URLs, filter-as-key.
- **Model:** singletons, containment, derived types, open types, enums (flags), complex types and collections, media entities, stream properties.
- **Writing:** POST with deep insert and `@odata.bind`, PATCH/PUT, DELETE, `$ref` links, `$each` bulk operations, `Prefer: return=representation|minimal`, `omit-values=nulls`, ETags.
- **Operations:** bound and unbound functions and actions, complex, collection and entity parameters, parameter aliases.
- **Batch:** multipart and JSON batch, changesets and atomicity groups, `Content-ID` references, partial failures, `continue-on-error`.
- **Protocol:** server-driven paging, delta links and delta payloads, `respond-async` with status-monitor polling, instance annotations, `odata.metadata=minimal|full|none`, OData error payloads.
- **Metadata:** CSDL XML and CSDL JSON, built-in vocabularies, a process-wide model cache, or supply your own `IEdmModel`.

## Requirements

- .NET 10 SDK or runtime.
- An OData V4 or V4.01 service. OData V1 to V3 services are not supported.
