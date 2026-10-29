# Batches

`ODataBatch` collects operations and sends them in one `$batch` request. Operations run through a client bound to the batch; results are available after `ExecuteAsync`.

```csharp
var batch = new ODataBatch(client);                 // shares the client's settings and metadata

Product? tea = null;
batch += async c => tea = await c.For<Product>().Set(new { ProductName = "Tea", UnitPrice = 4m }).InsertEntryAsync();
batch += c => c.For<Product>().Set(new { ProductName = "Milk", Category = tea }).InsertEntryAsync(false);   // binds to the first insert through its Content-ID ($1)
batch += c => c.For<Product>().Key(1).Set(new { UnitPrice = 19m }).UpdateEntryAsync(false);
batch += c => c.For<Product>().Key(2).DeleteEntryAsync();

await batch.ExecuteAsync();
Console.WriteLine(tea!.ProductID);
```

## What goes into one batch

- Reads (`FindEntriesAsync`, `FindEntryAsync`, `FindScalarAsync`, functions) and writes (insert, update, delete, link, actions) can be mixed.
- Consecutive writes form one changeset (multipart) or atomicity group (JSON) and succeed or fail together; reads sit between them as individual requests.
- A result produced by an earlier insert can be referenced by a later operation in the same changeset. The client writes a `Content-ID` for every insert and a `$n` reference when a later `Set` carries that result.
- Reads inside a batch return their results into the variables you capture, exactly like outside a batch.

## Formats

| `BatchFormat` | Wire format | Notes |
|---|---|---|
| `Multipart` (default) | `multipart/mixed` | Works with every OData V4 service. |
| `Json` | `application/json` JSON batch | OData 4.01; the batch request always carries `OData-Version: 4.01`. |

```csharp
var settings = new ODataClientSettings(serviceRoot) { BatchFormat = BatchFormat.Json };
var batch = new ODataBatch(settings);
```

## Failures

The service stops at the first failed operation. The client replays the successful ones and throws a `WebRequestException` for the failure, with the operation's status code and parsed OData error. A `404` inside a batch is an error even when `IgnoreResourceNotFoundException` is set.

Set `ContinueOnError = true` (sent as `Prefer: odata.continue-on-error`) to let the service process the remaining operations; the first failure is still thrown after the batch completes so you can inspect what ran.

## Headers

```csharp
batch.WithHeader("X-Correlation-Id", id);                 // on the $batch request itself
batch += c => c.For<Product>().WithHeader("If-Match", etag).Key(1).Set(...).UpdateEntryAsync();   // on one operation
```

## Media in batches

Multipart batches can carry `Media().GetStreamAsync()` operations; the raw bytes are returned from the batch results. JSON batches do not support binary operation bodies.
