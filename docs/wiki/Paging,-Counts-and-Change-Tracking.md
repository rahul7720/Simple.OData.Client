# Paging, Counts and Change Tracking

## Server-driven paging

Services may return a page and a next link. Ask for a page size with `WithMaxPageSize` (sent as `Prefer: odata.maxpagesize=N`) or set `ODataClientSettings.MaxPageSize` for every request.

### Follow pages automatically

```csharp
await foreach (var product in client.For<Product>().WithMaxPageSize(100).FindEntriesAsAsyncEnumerable(ct))
{
    // one request per page, headers and preferences repeated on every page
}
```

### Follow pages yourself

```csharp
var annotations = new ODataFeedAnnotations();
var page = await client.For<Product>().WithMaxPageSize(100).FindEntriesAsync(annotations);
while (annotations.NextPageLink is { } next)
{
    page = await client.For<Product>().WithMaxPageSize(100).FindEntriesAsync(next, annotations);
}
```

### Client-side paging

`Skip` and `Top` translate to `$skip` and `$top` and work with or without server paging.

## Counts

```csharp
var annotations = new ODataFeedAnnotations();
await client.For<Product>().WithCount().Top(10).FindEntriesAsync(annotations);   // $count=true
long? total = annotations.Count;                                                  // total across all pages

int filtered = await client.For<Product>().Filter(p => p.UnitPrice > 50).Count().FindScalarAsync<int>();   // Products/$count?$filter=...
```

## Feed annotations

`ODataFeedAnnotations` carries `Id`, `Count`, `NextPageLink`, `DeltaLink` and `InstanceAnnotations`. Pass one instance to `FindEntriesAsync` or `ExecuteAsArrayAsync` to have it filled.

## Change tracking (delta)

Request a delta link with `TrackChanges()` (or `ODataClientSettings.TrackChanges`), then read what changed:

```csharp
var annotations = new ODataFeedAnnotations();
var snapshot = await client.For<Product>().TrackChanges().FindEntriesAsync(annotations);   // Prefer: odata.track-changes

ODataDelta delta = await client.FindDeltaAsync(annotations.DeltaLink!);
foreach (var changed in delta.Changed) { /* IDictionary<string, object?> with the changed properties */ }
foreach (var deleted in delta.Deleted) { /* ODataDeletedEntry: Id, Reason, Data */ }
foreach (var link in delta.AddedLinks) { /* ODataDeltaLink: Source, Relationship, Target */ }
foreach (var link in delta.DeletedLinks) { }

var later = await client.FindDeltaAsync(delta.DeltaLink!);   // keep following
```

A delta response may itself page; `delta.NextPageLink` points at the next delta page and `delta.DeltaLink` at the link to use for the next round.
