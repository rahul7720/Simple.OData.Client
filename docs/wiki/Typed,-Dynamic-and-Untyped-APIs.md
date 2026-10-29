# Typed, Dynamic and Untyped APIs

The same fluent surface is available in three flavours. Pick per call; a single client serves all three.

| Flavour | Entry point | Values | Best for |
|---|---|---|---|
| Typed | `client.For<T>()` | your classes | application code, compile-time checks, LINQ-style filters |
| Untyped | `client.For("Set")` | `IDictionary<string, object?>` | generic tooling, properties unknown at compile time |
| Dynamic | `client.For(x.Set)` with `var x = ODataDynamic.Expression;` | `dynamic` | scripts and quick exploration |

## Typed

Properties are mapped by name using `ODataClientSettings.NameMatchResolver` (alphanumeric, case-sensitive by default). Navigation properties are plain references or collections. Unmapped POCO properties fail the request unless `IgnoreUnmappedProperties` is set.

```csharp
public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = "";
    public decimal? UnitPrice { get; set; }
    public Category? Category { get; set; }
}

var product = await client.For<Product>().Key(1).Expand(p => p.Category).FindEntryAsync();
```

- The collection name defaults to the class name, then its plural (`Product` → `Products`). Override it with `For<Product>("Items")` or register it once: `settings.TypeCache.Register<Product>("Items")`.
- Enums map to Edm enums, `DateOnly`/`TimeOnly`/`DateTimeOffset` to the Edm date and time types, `byte[]` to `Edm.Binary`, `Microsoft.Spatial` types to geography and geometry.
- Register conversions for your own value types with `settings.TypeCache.RegisterTypeConverter<T>(...)`.
- Server nulls do not overwrite a property's default value in your class.

## Untyped

```csharp
var entry = await client.For("Products").Key(1).Expand("Category").Select("ProductName", "Category").FindEntryAsync();
var category = (IDictionary<string, object?>)entry!["Category"]!;
```

Filters, ordering and expansions are strings in OData syntax. Values come back as CLR primitives (`int`, `decimal`, `DateTimeOffset`, `Guid`, `byte[]`, …), nested dictionaries for complex and entity values, and `IList<object?>` for collections. With `IncludeAnnotationsInResults` the dictionary also carries an `__annotations` entry of type `ODataEntryAnnotations` (ETag, id, type name, read and edit links, media resource, instance annotations).

The `ODataClient` class also exposes a command-text API that bypasses the fluent builder:

```csharp
var products = await client.FindEntriesAsync("Products?$filter=UnitPrice gt 20&$top=3");
var one = await client.FindEntryAsync("Products(1)");
var count = await client.FindScalarAsync("Products/$count");
var inserted = await client.InsertEntryAsync("Products", new Dictionary<string, object?> { ["ProductName"] = "Tea" });
```

## Dynamic

```csharp
var x = ODataDynamic.Expression;

IEnumerable<dynamic> products = await client
    .For(x.Products)
    .Filter(x.UnitPrice > 20 && x.ProductName.StartsWith("C"))
    .Expand(x.Category)
    .OrderBy(x.ProductName).ThenByDescending(x.UnitPrice)
    .FindEntriesAsync();

dynamic chai = await client.For(x.Products).Filter(x.ProductName == "Chai").FindEntryAsync();
Console.WriteLine(chai.Category.CategoryName);
```

Dynamic expressions support the same operators and functions as typed expressions, including `x.Products.Any(p => p.UnitPrice > 10)`, `x.Is(x.Ship)` / `x.As(x.Ship)` for type checks and casts, and `x.Products.Count` in filters. Writes use the same expressions: `.Set(x.ProductName = "Tea", x.UnitPrice = 4.5m)`.

## Mixing

Everything composes: a typed query can take a raw filter string, an untyped command can take an `ODataExpression`, and results can be read into a different type with `As<TResult>()`:

```csharp
var ships = await client.For<Transport>().As<Ship>().Filter("ShipName eq 'Titanic'").FindEntriesAsync();
```
