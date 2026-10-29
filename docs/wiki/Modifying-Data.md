# Modifying Data

## Insert

```csharp
var product = await client.For<Product>()
    .Set(new Product { ProductName = "Tea", UnitPrice = 4.5m, Category = existingCategory })
    .InsertEntryAsync();

await client.For("Products").Set(new Dictionary<string, object?> { ["ProductName"] = "Milk" }).InsertEntryAsync(resultRequired: false);
```

- `Set` accepts your class, an anonymous object, a dictionary, or dynamic assignments.
- A navigation value that carries its key properties is sent as an `@odata.bind` reference; a value without a key is sent as a nested resource (deep insert).
- `resultRequired: false` sends `Prefer: return=minimal` and returns `null`.

## Update

```csharp
await client.For<Product>().Key(1).Set(new { UnitPrice = 5m }).UpdateEntryAsync();           // PATCH Products(1)
await client.For<Product>().Key(1).Set(product).UpdateEntryAsync();                            // PATCH with every mapped property
```

- Only the properties you `Set` are sent. Set `PreferredUpdateMethod = ODataUpdateMethod.Put` to send a full replacement.
- Singletons are updated without a key: `client.For<Company>().Set(new { Name = "Contoso" }).UpdateEntryAsync()`.
- Collection navigation values in a PATCH are written as `@odata.bind` references under V4 and as a nested `@delta` resource set under V4.01.
- Set `IgnoreNavigationPropertiesOnUpdate` to leave links untouched.

### Optimistic concurrency

```csharp
var annotations = new ODataEntryAnnotations();            // or read the ETag from __annotations in an untyped result
await client.For<Product>().Key(1).WithIfMatch(etag).Set(new { UnitPrice = 6m }).UpdateEntryAsync();   // If-Match: W/"..."
await client.For<Product>().Key(1).WithIfMatch("*").DeleteEntryAsync();
await client.For<Product>().Key(1).WithIfNoneMatch(etag).FindEntryAsync();                               // 304 handling
```

Properties annotated with `Core.OptimisticConcurrency` in the metadata are sent in `If-Match` automatically when the entry carries an ETag.

## Delete

```csharp
await client.For<Product>().Key(1).DeleteEntryAsync();
```

## Links

```csharp
await client.For<Product>().Key(1).LinkEntryAsync(p => p.Category, new Category { CategoryID = 2 }); // PUT Products(1)/Category/$ref
await client.For<Product>().Key(1).UnlinkEntryAsync(p => p.Category);                                 // DELETE Products(1)/Category/$ref
await client.For<Category>().Key(2).LinkEntryAsync(c => c.Products, product);                          // POST Categories(2)/Products/$ref
await client.For<Category>().Key(2).UnlinkEntryAsync(c => c.Products, product);                        // DELETE Categories(2)/Products(1)/$ref
```

Collection members are unlinked with the key-segment form (`Categories(2)/Products(1)/$ref`), which is the OData 4.01 convention.

## Bulk update and delete

A command with a filter instead of a key updates or deletes every matching entity.

```csharp
var updated = await client.For<Product>().Filter(p => p.CategoryID == 1).Set(new { Discontinued = true }).UpdateEntriesAsync();
int deleted = await client.For<Product>().Filter(p => p.Discontinued).DeleteEntriesAsync();
```

| `BulkOperationMode` | Requests sent | Return value |
|---|---|---|
| `ClientSide` (default) | one GET, then one PATCH or DELETE per entity | the updated entities, or the number deleted |
| `Each` | one `PATCH Products/$each?$filter=...` or `DELETE Products/$each?$filter=...` | the updated entities (`$each` returns them), or `-1` because the service does not report a count |

## Return values and `IgnoreResourceNotFoundException`

Insert and update return the server representation by default. With `IgnoreResourceNotFoundException = true` a 404 on a direct request returns `null` instead of throwing; inside a batch a 404 always fails the batch unless `ContinueOnError` is set. See [[Error Handling]].
