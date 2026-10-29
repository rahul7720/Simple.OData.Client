# Aggregation and Computed Properties

OData 4.01 data aggregation (`$apply`) and computed properties (`$compute`) are available on every API flavour.

## `$apply` with the typed builder

```csharp
public class CategoryCount { public int CategoryID { get; set; } public int Count { get; set; } }
public class PriceStats { public decimal Average { get; set; } public decimal Min { get; set; } public decimal Max { get; set; } }

var perCategory = await client.For<Product>()
    .Apply(b => b
        .Filter(p => p.UnitPrice > 20)
        .GroupBy((p, a) => new CategoryCount { CategoryID = p.CategoryID, Count = a.Count() }))
    .FindEntriesAsync();
// $apply=filter(UnitPrice gt 20)/groupby((CategoryID),aggregate($count as Count))

var stats = await client.For<Product>()
    .Apply(b => b.Aggregate((p, a) => new PriceStats { Average = a.Average(p.UnitPrice), Min = a.Min(p.UnitPrice), Max = a.Max(p.UnitPrice) }))
    .FindEntriesAsync();
```

The builder (`IDataAggregation<T>`) offers `Filter`, `GroupBy`, `Aggregate`, `Compute`, `TopCount` and `BottomCount`; the aggregation functions (`IAggregationFunction<T>`) are `Sum`, `Average`, `Min`, `Max`, `Count` and `CountDistinct`. Clauses chain into a transformation pipeline separated by `/`. The result type of `Apply` becomes the element type for the rest of the query, so you can `OrderBy`, `Top` and `Filter` the aggregated rows.

## `$apply` as text or dynamic

```csharp
var groups = await client.For("Products").Apply("groupby((CategoryID),aggregate(UnitPrice with sum as Total))").OrderByDescending("Total").Top(1).FindEntriesAsync();

var x = ODataDynamic.Expression;
var a = ODataDynamicDataAggregation.AggregationFunction;
IEnumerable<dynamic> counts = await client.For(x.Products)
    .Apply(ODataDynamicDataAggregation.Builder.GroupBy(new { x.CategoryID, Count = a.Count() }))
    .FindEntriesAsync();
```

## `$compute`

```csharp
var doubled = await client.For<Product>().Key(1).Compute(p => new { Doubled = p.UnitPrice * 2 }).FindEntryAsync();
var rows = await client.For("Products").Compute("UnitPrice mul 2 as Doubled").Select("ProductName", "Doubled").OrderBy("Doubled").FindEntriesAsync();
```

Computed properties can be selected, filtered and ordered. With the typed API the anonymous type's property names become the aliases. Because computed and aggregated properties are not declared in the metadata, the client's default `Validations` setting accepts undeclared properties; keep it that way when using these features.
