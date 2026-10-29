# Error Handling

## `WebRequestException`

Any response with a non-success status code throws `WebRequestException`:

| Member | Content |
|---|---|
| `Code` | the `HttpStatusCode` |
| `ReasonPhrase` | the HTTP reason phrase |
| `Response` | the raw response body |
| `RequestUri` | the request URL |
| `Error` | the parsed OData error (`ODataErrorDetails`), when the body is an OData error payload |

`ODataErrorDetails` has `Code`, `Message`, `Target`, `Details` (nested `ODataErrorDetails`) and `InnerError`.

```csharp
try
{
    await client.For<Product>().Key(99999).Set(new { UnitPrice = -1m }).UpdateEntryAsync();
}
catch (WebRequestException ex) when (ex.Code == HttpStatusCode.BadRequest)
{
    Console.WriteLine(ex.Error?.Message ?? ex.Response);
}
```

The exception message defaults to the reason phrase; set `WebRequestExceptionMessageSource` to `ResponseContent` or `Both` to put the body in the message.

Failures while downloading `$metadata` also surface as `WebRequestException`. Connectivity failures (DNS, refused connections, timeouts) are `HttpRequestException` or `TaskCanceledException` from `HttpClient`.

## Not found

With `IgnoreResourceNotFoundException = true`, a `404` on a direct request returns `null` (single entity), an empty list (collection) or `default` (scalar), and insert, update and delete become a no-op. Inside a batch a `404` always fails the batch unless `ContinueOnError` is set.

## Client-side validation

- `UnresolvableObjectException` is thrown before any request is sent when a collection, property, function or type name cannot be found in the metadata. Check spelling and the `NameMatchResolver`.
- `InvalidOperationException("No entry key specified.")` means an update, delete or link was attempted on a command with neither key nor filter.
- A key with the wrong number of values throws instead of silently addressing the whole collection.
- `NotSupportedException` is thrown for expressions that have no OData translation.

## Batch failures

A failed operation throws `WebRequestException` after the successful operations have been replayed into your captured variables. See [[Batches]].
