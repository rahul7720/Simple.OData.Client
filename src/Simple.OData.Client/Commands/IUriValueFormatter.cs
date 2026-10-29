namespace Simple.OData.Client.Commands;

/// <summary>Formats CLR values as OData URI literals.</summary>
internal interface IUriValueFormatter
{
	/// <summary>Converts a value to its URI literal form, optionally percent-encoding it.</summary>
	string ConvertValueToUriLiteral(object? value, bool escapeDataString);
}
