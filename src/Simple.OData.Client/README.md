# Simple.OData.Client

A metadata-driven OData V4 / V4.01 client for .NET 10. Work with any OData service through dictionaries, your own
POCOs or dynamic expressions; no code generation required.

```csharp
var client = new ODataClient(new Uri("https://services.odata.org/V4/Northwind/Northwind.svc/"));

var products = await client.For<Product>()
    .Filter(x => x.UnitPrice > 50)
    .Expand(x => x.Category)
    .OrderByDescending(x => x.UnitPrice)
    .Top(5)
    .FindEntriesAsync();

var created = await client.For("Products").Set(new { ProductName = "Tea", UnitPrice = 4.5m }).InsertEntryAsync();
```

Supports the full OData 4.01 surface: `$compute`, `$apply`, `$each`, delta links, multipart and JSON batches,
`respond-async`, server-driven paging, key-as-segment, CSDL XML and JSON metadata, media streams and more.

Documentation, feature list, settings and migration notes from 6.x:
https://github.com/simple-odata-client/Simple.OData.Client#readme
