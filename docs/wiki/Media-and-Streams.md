# Media and Streams

OData distinguishes **media entities** (the entity itself has a binary payload at `Entity/$value`) and **stream properties** (a named binary property such as `Pictures(1)/Content`). The client handles both through `Media()`.

## Read

```csharp
using Stream image = await client.For("Pictures").Key(1).Media().GetStreamAsync();              // GET Pictures(1)/$value
byte[] bytes = await client.For<Picture>().Key(1).Media().GetStreamAsArrayAsync();
string text = await client.For<Picture>().Key(1).Media("Content").GetStreamAsStringAsync();      // GET Pictures(1)/Content
string text2 = await client.For<Picture>().Key(1).Media(p => p.Content).GetStreamAsStringAsync();
```

## Write

```csharp
await client.For("Pictures").Key(1).Media().SetStreamAsync(stream, "image/png");                // PUT Pictures(1)/$value
await client.For("Pictures").Key(1).Media().SetStreamAsync(bytes, "image/png");
await client.For("Pictures").Key(1).Media("Content").SetStreamAsync("plain text");              // text/plain
await client.For("Pictures").Key(1).Media().SetStreamAsync(bytes, "image/png", optimisticConcurrency: true);   // sends If-Match from the entry's ETag
```

## Load streams with the entity

`WithMedia` fetches the named streams after the entity and fills them in:

```csharp
var picture = await client.For<Picture>().Key(1).WithMedia(p => p.Media).FindEntryAsync();      // byte[] Media filled from Pictures(1)/$value
var entry = await client.For("Pictures").Key(1).WithMedia("Content").FindEntryAsync();           // entry["Content"] holds a Stream
```

Typed properties of type `byte[]`, `string` or `Stream` receive the content. Untyped entries receive a `Stream`.

## Annotations

With `IncludeAnnotationsInResults`, `ODataEntryAnnotations.MediaResource` carries the media entity's `ContentType`, `ReadLink`, `EditLink` and `ETag`.

## Command-text API

```csharp
Stream s = await client.GetMediaStreamAsync("Pictures(1)/$value");
await client.SetMediaStreamAsync("Pictures(1)/$value", stream, "image/png");
```
