using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;

namespace Simple.OData.Client.Metadata;

/// <summary>
/// Parses CSDL XML and CSDL JSON metadata documents into an <see cref="IEdmModel"/>.
/// </summary>
internal static class MetadataLoader
{
	private static readonly string[] SupportedVersions = ["4.0", "4.01"];

	public static MetadataFormat Detect(string document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var span = Trim(document);
		return span.Length > 0 && span[0] == '{' ? MetadataFormat.Json : MetadataFormat.Xml;
	}

	public static string GetVersion(string document)
	{
		ArgumentNullException.ThrowIfNull(document);
		try
		{
			return Detect(document) == MetadataFormat.Json ? GetJsonVersion(document) : GetXmlVersion(document);
		}
		catch (Exception ex) when (ex is XmlException or JsonException)
		{
			throw new InvalidOperationException("The metadata document is not a valid CSDL XML or CSDL JSON document.", ex);
		}
	}

	public static IEdmModel Parse(string document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var version = GetVersion(document);
		if (!SupportedVersions.Contains(version, StringComparer.Ordinal))
		{
			throw new InvalidOperationException($"Unsupported OData protocol version '{version}'. Only OData 4.0 and 4.01 metadata documents are supported.");
		}

		return Detect(document) == MetadataFormat.Json ? ParseJson(document) : ParseXml(document);
	}

	public static IEdmModel ParseXml(string document)
	{
		ArgumentNullException.ThrowIfNull(document);

		// Some services still emit the V3-only ConcurrencyMode attribute, which the V4 reader rejects.
		if (document.Contains(" ConcurrencyMode=", StringComparison.Ordinal))
		{
			document = document.Replace(" ConcurrencyMode=\"None\"", string.Empty, StringComparison.Ordinal)
				.Replace(" ConcurrencyMode=\"Fixed\"", string.Empty, StringComparison.Ordinal);
		}

		using var reader = XmlReader.Create(new StringReader(Trim(document).ToString()), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		if (!CsdlReader.TryParse(reader, ignoreUnexpectedAttributesAndElements: true, out var model, out var errors))
		{
			throw CreateParseException(errors);
		}

		return model;
	}

	public static IEdmModel ParseJson(string document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var bytes = Encoding.UTF8.GetBytes(Trim(document).ToString());
		var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
		var settings = new CsdlJsonReaderSettings { IgnoreUnexpectedJsonElements = true };
		if (!CsdlReader.TryParse(ref reader, settings, out var model, out var errors))
		{
			throw CreateParseException(errors);
		}

		return model;
	}

	private static InvalidOperationException CreateParseException(IEnumerable<EdmError> errors)
	{
		var details = string.Join("; ", errors.Select(x => $"{x.ErrorCode}: {x.ErrorMessage}"));
		return new InvalidOperationException($"Unable to parse the metadata document: {details}");
	}

	private static string GetXmlVersion(string document)
	{
		using var reader = XmlReader.Create(new StringReader(Trim(document).ToString()), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		reader.MoveToContent();
		return reader.GetAttribute("Version") ?? string.Empty;
	}

	private static string GetJsonVersion(string document)
	{
		using var json = JsonDocument.Parse(Trim(document).ToString(), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
		return json.RootElement.TryGetProperty("$Version", out var version) ? version.GetString() ?? string.Empty : string.Empty;
	}

	private static ReadOnlySpan<char> Trim(string document) => document.AsSpan().Trim().TrimStart('﻿').Trim();
}
