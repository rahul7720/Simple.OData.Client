# Querying Data

All queries start with `client.For(...)` and end with one of the `Find*` methods. Everything in between adds query options. The examples use the typed API; the same methods take strings or `ODataExpression` values for the untyped and dynamic APIs (see [[Typed, Dynamic and Untyped APIs]]).

## Choosing what comes back

| Method | Returns | Use for |
|---|---|---|
| `FindEntriesAsync()` | `IReadOnlyList<T>` | A collection. |
| `FindEntriesAsync(annotations)` | `IReadOnlyList<T>` | A collection plus feed metadata: count, next link, delta link. |
| `FindEntriesAsAsyncEnumerable()` | `IAsyncEnumerable<T>` | Every entity across all server pages. |
| `FindEntryAsync()` | `T?` | A single entity (`Key(...)`, a singleton, or the first match). |
| `FindScalarAsync<TResult>()` | `TResult?` | A single value: `$count`, a property, `$value`. |
| `GetCommandTextAsync()` | `string` | The relative URL the client would send. |

## Keys

```csharp
await client.For<Product>().Key(1).FindEntryAsync();                            // Products(1)
await client.For<OrderDetail>().Key(new { OrderID = 10248, ProductID = 11 }).FindEntryAsync(); // compound key
await client.For<Product>().Key(new Dictionary<string, object?> { ["ProductName"] = "Chai" }).FindEntryAsync(); // alternate key
await client.For<Company>().FindEntryAsync();                                   // singleton
```

A filter that pins every key property (`Filter(p => p.ProductID == 1)`) is sent as a key when the command addresses a single entity. Set `KeyAsSegment = true` for `Products/1`.

## Filtering

```csharp
await client.For<Product>()
    .Filter(p => p.UnitPrice > 20 && p.Discontinued == false)
    .Filter(p => p.ProductName.Contains("a") || p.ProductName.EndsWith("i"))   // combined with "and"
    .FindEntriesAsync();
```

Supported expression members translate to OData built-ins: string (`StartsWith`, `EndsWith`, `Contains`, `Length`, `ToLower`, `ToUpper`, `Trim`, `Substring`, `IndexOf`, `Replace`, `Concat`), date and time (`Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `Date`, `TimeOfDay`, `DateTime.Now`), arithmetic (`+ - * / %`, `Math.Round`, `Floor`, `Ceiling`), collections (`Any`, `All`, `Contains` for `in`), enums (`HasFlag` for `has`), types (`is` and `as` for `isof`/`cast`), and the `ODataFunctions` helpers (`MatchesPattern`, `HasSubset`, `HasSubsequence`, `TotalSeconds`, `FractionalSeconds`, `MaxDateTime`, `MinDateTime`, `Now`, `GeoDistance`, `GeoLength`, `GeoIntersects`).

Closures and method calls on captured objects are evaluated on the client before translation.

Raw filter text is also accepted and sent as given: `.Filter("startswith(ProductName,'C') and UnitPrice gt 20")`.

## Selecting and expanding

```csharp
await client.For<Product>()
    .Select(p => new { p.ProductID, p.ProductName, p.Category })
    .Expand(p => p.Category)
    .Expand(p => p.Category.Products)                       // nested expand
    .Expand(ODataExpandOptions.ByValue(2), p => p.Category) // $levels=2
    .Expand(ODataExpandOptions.ByReference(), p => p.Supplier) // $expand=Supplier/$ref
    .FindEntriesAsync();
```

Options on an expanded collection are written as nested query options: `$expand=Products($filter=...;$orderby=...;$top=...)`. The typed API derives them when you filter or order by a path through the navigation property; the untyped API accepts the nested options as text.

## Ordering, paging and counting

```csharp
await client.For<Product>()
    .OrderBy(p => p.CategoryID).ThenByDescending(p => p.UnitPrice)
    .Skip(20).Top(10)
    .FindEntriesAsync();

var annotations = new ODataFeedAnnotations();
var page = await client.For<Product>().WithCount().Top(10).FindEntriesAsync(annotations);  // $count=true
long? total = annotations.Count;

int chaiCount = await client.For<Product>().Filter(p => p.ProductName == "Chai").Count().FindScalarAsync<int>(); // Products/$count?$filter=...
```

See [[Paging, Counts and Change Tracking]] for server-driven paging and next links.

## Search and index

```csharp
await client.For<Product>().Search("chai OR tea").FindEntriesAsync();   // $search
await client.For<Product>().Index(5).FindEntriesAsync();                 // $index=5
```

## Navigation

```csharp
await client.For<Category>().Key(1).NavigateTo(c => c.Products).FindEntriesAsync();         // Categories(1)/Products
await client.For<Employee>().Key(2).NavigateTo(e => e.Superior).NavigateTo(e => e.Subordinates).FindEntriesAsync();
```

## Derived types

```csharp
await client.For<Transport>().As<Ship>().FindEntriesAsync();            // Transport/Namespace.Ship
await client.For<Transport>().Filter(t => (t as Ship)!.ShipName == "Titanic").FindEntriesAsync(); // cast in filter
```

## Open types and dynamic properties

Map undeclared properties to a dictionary with `WithProperties`:

```csharp
public class Animal { public int Id { get; set; } public IDictionary<string, object?> DynamicProperties { get; set; } = new Dictionary<string, object?>(); }
var animals = await client.For<Animal>("Animals").WithProperties(a => a.DynamicProperties).FindEntriesAsync();
```

## Arbitrary query options

```csharp
await client.For<Product>().QueryOptions("$format=application/json&custom=1").FindEntriesAsync();
```

## Command text without sending

```csharp
string url = await client.For<Product>().Filter(p => p.UnitPrice > 10).Top(3).GetCommandTextAsync();
// Products?$filter=UnitPrice%20gt%2010&$top=3
```
