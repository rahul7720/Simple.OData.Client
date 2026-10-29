# Migrating from 6.x

7.0 is a rewrite. The fluent API is largely unchanged, so most query and update code compiles after the package swap; the differences below are the ones to watch for.

## Packages and targets

| 6.x | 7.0 |
|---|---|
| `Simple.OData.Client`, `Simple.OData.Client.Core`, `Simple.OData.Client.V3.Adapter`, `Simple.OData.Client.V4.Adapter` | `Nexus.Odata.Client` (one package) |
| `net461`, `netstandard2.0` | `net10.0` |
| OData V1 to V4 | OData V4 and V4.01 |
| Microsoft.OData.Core 7.x | Microsoft.OData.Core 9.x |

The assembly name and namespaces stay `Simple.OData.Client`.

## Removed

- `ODataClientSettings.PayloadFormat`, `RenewHttpConnection`, `AdapterFactory`, `UseAbsoluteReferenceUris`.
- `WithExtensions()`: call `Apply(...)` on the fluent client directly.
- `ODataNameMatchResolver.Alpahumeric` is now `ODataNameMatchResolver.Alphanumeric`.
- Duplicate `CancellationToken` overloads: every method has one optional token as its last parameter.

## Changed signatures

| 6.x | 7.0 |
|---|---|
| `Task<IEnumerable<T>> FindEntriesAsync()` | `Task<IReadOnlyList<T>> FindEntriesAsync(CancellationToken = default)` |
| `Task<object> GetMetadataAsync()` | `Task<IEdmModel> GetMetadataAsync(CancellationToken = default)` |
| `Unbound()` returned a dictionary client | `Unbound()` returns `IUnboundClient<object>`; use `Unbound<T>()` or `Unbound<IDictionary<string, object?>>()` |
| `LinkEntryAsync<U>(U linkedEntryKey, string linkName = null)` | `LinkEntryAsync<TResult>(TResult)` and `LinkEntryAsync<TResult>(TResult, string linkName)` (same for `UnlinkEntryAsync`) |
| `OrderBy(IEnumerable<KeyValuePair<string, bool>>)` | `OrderBy(IEnumerable<ODataOrderByColumn>)` |
| `new ODataExpression<T>(Expression<Predicate<T>>)` | `new ODataExpression<T>(Expression<Func<T, bool>>)` |

## Changed behaviour

- URLs keep apostrophes literal: `Products(ProductName='Chai')`, `$filter=ProductName eq 'Chai'`.
- Collection members are unlinked with a key segment: `DELETE Categories(1)/Products(2)/$ref`.
- Complex and entity function arguments are sent as JSON parameter aliases.
- A `404` inside a batch fails the batch unless `ContinueOnError` is set; `IgnoreResourceNotFoundException` applies to direct requests only.
- Undeclared properties no longer fail a read by default. Set `Validations = ValidationKinds.All` to restore the old behaviour.
- `DeleteEntriesAsync` returns `-1` under `BulkOperationMode.Each` because `$each` does not report a count.
- A key with the wrong number of values throws instead of silently widening to the whole set.
- A key-like filter combined with `$count`, `$orderby`, `$skip`, `$search`, `$apply` or `$top > 1` stays a `$filter`.
- Network failures are `HttpRequestException`; only non-success status codes become `WebRequestException`.
- Navigation values without key properties are sent as deep inserts instead of failing.

## New

`FindEntriesAsAsyncEnumerable`, `Compute`, `Index`, `WithIfMatch`/`WithIfNoneMatch`, `WithMaxPageSize`, `TrackChanges`, `FindDeltaAsync`, JSON batch (`BatchFormat.Json`), `$each` (`BulkOperationMode.Each`), `RespondAsync`, CSDL JSON metadata (`MetadataFormat.Json`), `KeyAsSegment`, `OmitNullValues`, `WebRequestException.Error`, `BuildRequestFor().…RunAsync()`, `MaxConcurrentRequests`, `TimeProvider`.
