using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Models;

namespace Simple.OData.Client.Tests.TestServer.Controllers;

public class PicturesController(NorthwindData data) : NorthwindController(data)
{
	[EnableQuery]
	public IActionResult Get() => Ok(Data.Pictures.AsQueryable());

	[EnableQuery]
	public IActionResult Get(int key)
	{
		var picture = Find(key);
		return picture is null ? NotFound() : Ok(picture);
	}

	public IActionResult Post([FromBody] Picture picture)
	{
		if (picture is null)
		{
			return BadRequest();
		}

		Data.Mutate(() =>
		{
			if (picture.Id == 0 || Data.Pictures.Any(x => x.Id == picture.Id))
			{
				picture.Id = Data.NextPictureId();
			}

			Data.Pictures.Add(picture);
		});
		return Created(picture);
	}

	public IActionResult Delete(int key)
	{
		var picture = Find(key);
		if (picture is null)
		{
			return NotFound();
		}

		Data.Mutate(() => Data.Pictures.Remove(picture));
		return NoContent();
	}

	[HttpGet("odata/Pictures({key})/$value")]
	public IActionResult GetMedia(int key)
	{
		var picture = Find(key);
		return picture is null ? NotFound() : File(picture.Media, "image/png");
	}

	[HttpPut("odata/Pictures({key})/$value")]
	public async Task<IActionResult> PutMedia(int key)
	{
		var picture = Find(key);
		if (picture is null)
		{
			return NotFound();
		}

		var bytes = await ReadBodyAsync().ConfigureAwait(false);
		Data.Mutate(() => picture.Media = bytes);
		return NoContent();
	}

	[HttpGet("odata/Pictures({key})/Content")]
	public IActionResult GetContent(int key)
	{
		var picture = Find(key);
		return picture is null ? NotFound() : File(picture.ContentBytes, "text/plain");
	}

	[HttpPut("odata/Pictures({key})/Content")]
	public async Task<IActionResult> PutContent(int key)
	{
		var picture = Find(key);
		if (picture is null)
		{
			return NotFound();
		}

		var bytes = await ReadBodyAsync().ConfigureAwait(false);
		Data.Mutate(() => picture.ContentBytes = bytes);
		return NoContent();
	}

	private async Task<byte[]> ReadBodyAsync()
	{
		using var buffer = new MemoryStream();
		await Request.Body.CopyToAsync(buffer).ConfigureAwait(false);
		return buffer.ToArray();
	}

	private Picture? Find(int key) => Data.Pictures.FirstOrDefault(x => x.Id == key);
}
