# Getting Started

## 1. Add the GitHub Packages feed

The package is published to GitHub Packages, which requires a GitHub account token with the `read:packages` scope even for public packages.

```bash
dotnet nuget add source https://nuget.pkg.github.com/rahul7720/index.json \
  --name github-rahul7720 \
  --username <your-github-user> \
  --password <a-github-token-with-read:packages> \
  --store-password-in-clear-text
```

Alternatively download the `.nupkg` from the [latest release](https://github.com/rahul7720/Simple.OData.Client/releases) and add its folder as a local source.

## 2. Install the package

```bash
dotnet add package Nexus.Odata.Client --version 7.0.0
```

The assembly is `Simple.OData.Client.dll` and every type lives in the `Simple.OData.Client` namespace.

## 3. Create a client

```csharp
using Simple.OData.Client;

// From a service root
var client = new ODataClient(new Uri("https://services.odata.org/V4/Northwind/Northwind.svc/"));

// From an HttpClient you own (its BaseAddress is the service root)
var http = new HttpClient { BaseAddress = new Uri("https://example.com/odata/") };
var client2 = new ODataClient(http);

// From settings
var settings = new ODataClientSettings(new Uri("https://example.com/odata/"))
{
    ProtocolVersion = ODataProtocolVersion.V401,
    IgnoreResourceNotFoundException = true,
};
var client3 = new ODataClient(settings);
```

The first request downloads `$metadata` and caches the parsed model for the lifetime of the process, keyed by service root. Every later client for the same root reuses it. See [[Configuration]] for supplying the metadata yourself.

## 4. Read data

```csharp
// Untyped: dictionaries
IDictionary<string, object?>? chai = await client.For("Products").Filter("ProductName eq 'Chai'").FindEntryAsync();

// Typed: your own classes, mapped by property name
public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public decimal? UnitPrice { get; set; }
    public Category? Category { get; set; }
}

IReadOnlyList<Product> products = await client.For<Product>()
    .Filter(p => p.UnitPrice > 20 && p.ProductName.StartsWith("C"))
    .Expand(p => p.Category)
    .OrderByDescending(p => p.UnitPrice)
    .Top(5)
    .FindEntriesAsync();

// Dynamic: no classes
var x = ODataDynamic.Expression;
IEnumerable<dynamic> beverages = await client.For(x.Products).Filter(x.CategoryID == 1).FindEntriesAsync();
```

## 5. Write data

```csharp
var created = await client.For<Product>().Set(new { ProductName = "Tea", UnitPrice = 4.5m }).InsertEntryAsync();
await client.For<Product>().Key(created!.ProductID).Set(new { UnitPrice = 5m }).UpdateEntryAsync();
await client.For<Product>().Key(created.ProductID).DeleteEntryAsync();
```

## 6. Cancel and time out

Every asynchronous method takes an optional `CancellationToken` as its last parameter. `ODataClientSettings.RequestTimeout` bounds each HTTP request.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var orders = await client.For<Order>().Top(100).FindEntriesAsync(cts.Token);
```

Continue with [[Typed, Dynamic and Untyped APIs]] or [[Querying Data]].
