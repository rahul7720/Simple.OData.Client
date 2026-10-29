# Functions and Actions

Functions are `GET` requests with parameters in the URL; actions are `POST` requests with parameters in the body. The client reads the operation's declaration from the metadata to decide how to send it and how to read the result.

## Bound to a collection or entity

```csharp
var best = await client.For<Product>().Function("MostExpensive").ExecuteAsSingleAsync();                 // Products/NS.MostExpensive()
var price = await client.For<Product>().Key(1).Function("Discounted").Set(new { percent = 10m }).ExecuteAsScalarAsync<decimal>();
await client.For<Product>().Key(1).Action("Rate").Set(new { rating = 5 }).ExecuteAsync();
```

## Unbound (imports)

```csharp
int count = await client.Unbound().Function("ProductCount").ExecuteAsScalarAsync<int>();
int echoed = await client.Unbound().Action("Echo").Set(new { value = 42 }).ExecuteAsScalarAsync<int>();
Address? address = await client.Unbound<Address>().Function("PassThroughAddress").Set(new { address = input }).ExecuteAsSingleAsync();
```

`Unbound()` returns `IUnboundClient<object>`; use `Unbound<T>()` to shape the result, or `Unbound<IDictionary<string, object?>>()` for dictionaries.

## Reading results

| Method | Returns |
|---|---|
| `ExecuteAsync()` | nothing (actions without a result) |
| `ExecuteAsScalarAsync<T>()` | a primitive or enum |
| `ExecuteAsSingleAsync()` / `ExecuteAsSingleAsync<T>()` | one entity or complex value |
| `ExecuteAsEnumerableAsync()` | a collection of entities or complex values |
| `ExecuteAsArrayAsync<T>()` | a collection of primitives or complex values as an array |

`ExecuteAsArrayAsync(annotations)` fills `ODataFeedAnnotations` for collection results that page.

## Parameters

- Primitives, enums, `DateTimeOffset`, `Guid`, `TimeSpan`, `byte[]` and collections of these are written as URL literals for functions and as JSON for actions.
- Complex and entity values passed to a function are sent as JSON parameter aliases: `PassThroughAddress(address=@p1)?@p1={...}`, including `@odata.type` for typed objects.
- Names are matched against the declared parameter names; set `UnqualifiedNameCall = true` if the service accepts bound calls without the namespace.

The untyped client offers the same operations without the fluent builder:

```csharp
await client.ExecuteFunctionAsScalarAsync<int>("ProductCount");
await client.ExecuteActionAsync("Reseed", new Dictionary<string, object?> { ["start"] = 100 });
```

## Composing with query options

A function returning a collection can be filtered, ordered and paged like a collection:

```csharp
var top = await client.For<Product>().Function("Discontinued").Filter(p => p.UnitPrice > 10).Top(5).ExecuteAsEnumerableAsync();
```
