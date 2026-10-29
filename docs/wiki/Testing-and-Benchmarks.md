# Testing and Benchmarks

## Test projects

| Project | What it covers | How it runs |
|---|---|---|
| `Simple.OData.Client.Tests` | unit tests (expressions, URL building, metadata, type mapping) and end-to-end scenarios against an in-process OData 4.01 server | always |
| `Simple.OData.Client.Tests.TestServer` | the in-process Northwind service built on Microsoft.AspNetCore.OData, hosted with `TestServer` | referenced by the tests |
| `Simple.OData.Client.Tests.Integration` | live tests against the public services.odata.org endpoints (Northwind, TripPin, OData reference service) | only with `ODATA_LIVE_TESTS=1` |
| `Simple.OData.Client.Benchmarks` | BenchmarkDotNet suite | manually |

```bash
dotnet test src/Simple.OData.Client.slnx
ODATA_LIVE_TESTS=1 dotnet test src/Simple.OData.Client.Tests.Integration
dotnet run -c Release --project src/Simple.OData.Client.Benchmarks -- --filter '*'
```

The tests run on the Microsoft.Testing.Platform runner with xunit.v3. To get a TRX report pass `-- --report-xunit-trx --report-xunit-trx-filename results.trx` after `dotnet test`; the VSTest `--logger trx` option is not supported by this runner.

## The in-process server

The test server exposes a Northwind-shaped model with products, categories, suppliers, employees (recursive navigation), orders, pictures (media), a `Company` singleton, derived transports, open-typed animals, alternate keys, ETags, bound and unbound functions and actions, `$apply`, `$each`, delta (`Products/NorthwindModel.Changes()`), asynchronous responses (`Products/NorthwindModel.Slow()`), server paging and `omit-values`. Requests are recorded, so tests can assert on the exact URLs, headers and bodies the client sent.

Set `ODATA_TESTSERVER_LOG=1` to get the server's debug log on the console, and open `GET /$odata` on the test server for the route table.

## Testing your own code

- Point `ODataClientSettings.RequestExecutor` at a function that returns canned `HttpResponseMessage`s, or pass an `HttpClient` built on a fake `HttpMessageHandler`.
- Supply `MetadataDocument` so no `$metadata` request is made.
- Replace `TimeProvider` to make polling and retry delays instant.
- Capture `OnTrace` output to assert on request text.

## Benchmarks

`docs/benchmarks.md` holds the current numbers for feed materialization, URL building and expression translation. The main hotspot is typed LINQ translation of filters with many captured constants, which is slower than string or dynamic filters because of expression compilation.
