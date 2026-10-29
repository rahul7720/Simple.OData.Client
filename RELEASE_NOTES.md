#### 7.0.0 (2026-10-29)
* Rewritten for .NET 10 and C# 14 as a single package on Microsoft.OData.Core 9; OData V4 and V4.01 only
* OData 4.01 support: `$compute`, `$apply`, `$each`, delta links and delta payloads, JSON batch, `respond-async`, `odata.maxpagesize` paging, key-as-segment, CSDL JSON metadata, `omit-values=nulls`, instance annotations, `in`/`has`/`isof`/`cast`, `$search`, `$index`
* API: `CancellationToken` on every operation, `IReadOnlyList<T>` results, `FindEntriesAsAsyncEnumerable`, `GetMetadataAsync` returns `IEdmModel`, `WebRequestException.Error`, `ODataDelta`, two-phase requests that can be re-run
* Removed: V1–V3 support, adapter packages, `PayloadFormat`, `RenewHttpConnection`, `AdapterFactory`, `UseAbsoluteReferenceUris`, `WithExtensions()`
* Behaviour: apostrophes stay literal in URLs, collection members are unlinked with a key segment, complex function arguments are JSON parameter aliases, a 404 inside a batch fails the batch unless `ContinueOnError` is set, undeclared properties are accepted by default
* Package: published to GitHub Packages as `Nexus.Odata.Client` (assembly and namespaces remain `Simple.OData.Client`)
* Tests: more than 500 end-to-end scenarios against an in-process ASP.NET Core OData 4.01 service, opt-in live tests (`ODATA_LIVE_TESTS=1`), BenchmarkDotNet suite

#### 5.0.0.1 (2018-05-22)
* Fix netstandard dependencies

#### 5.0.0 (2018-05-21)
* Support for netstandard 2.0
