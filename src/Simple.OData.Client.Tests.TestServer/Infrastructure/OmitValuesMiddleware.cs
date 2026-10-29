using System.Text.Json;
using Microsoft.AspNetCore.Builder;

namespace Simple.OData.Client.Tests.TestServer.Infrastructure;

/// <summary>Honours <c>Prefer: omit-values=nulls</c> by stripping null-valued properties from JSON responses.</summary>
public static class OmitValuesMiddleware
{
	private const string Preference = "omit-values=nulls";

	public static IApplicationBuilder UseOmitValues(this IApplicationBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);
		return app.Use(async (context, next) =>
		{
			if (!context.Request.Headers["Prefer"].Any(x => x?.Contains(Preference, StringComparison.OrdinalIgnoreCase) == true))
			{
				await next(context).ConfigureAwait(false);
				return;
			}

			var original = context.Response.Body;
			using var buffer = new MemoryStream();
			context.Response.Body = buffer;
			try
			{
				await next(context).ConfigureAwait(false);
				buffer.Position = 0;
				if (buffer.Length > 0 && context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
				{
					using var document = await JsonDocument.ParseAsync(buffer).ConfigureAwait(false);
					using var output = new MemoryStream();
					using (var writer = new Utf8JsonWriter(output))
					{
						WriteWithoutNulls(document.RootElement, writer);
					}

					context.Response.Headers.Append("Preference-Applied", Preference);
					context.Response.ContentLength = output.Length;
					output.Position = 0;
					await output.CopyToAsync(original).ConfigureAwait(false);
				}
				else
				{
					await buffer.CopyToAsync(original).ConfigureAwait(false);
				}
			}
			finally
			{
				context.Response.Body = original;
			}
		});
	}

	private static void WriteWithoutNulls(JsonElement element, Utf8JsonWriter writer)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();
				foreach (var property in element.EnumerateObject())
				{
					if (property.Value.ValueKind == JsonValueKind.Null)
					{
						continue;
					}

					writer.WritePropertyName(property.Name);
					WriteWithoutNulls(property.Value, writer);
				}

				writer.WriteEndObject();
				break;
			case JsonValueKind.Array:
				writer.WriteStartArray();
				foreach (var item in element.EnumerateArray())
				{
					WriteWithoutNulls(item, writer);
				}

				writer.WriteEndArray();
				break;
			default:
				element.WriteTo(writer);
				break;
		}
	}
}
