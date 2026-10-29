namespace Simple.OData.Client;

/// <summary>The content of an OData error payload.</summary>
public sealed class ODataErrorDetails
{
	/// <summary>The service-defined error code.</summary>
	public string? Code { get; init; }

	/// <summary>The human-readable message.</summary>
	public string? Message { get; init; }

	/// <summary>The target of the error (a property or parameter name).</summary>
	public string? Target { get; init; }

	/// <summary>Additional error details.</summary>
	public IReadOnlyList<ODataErrorDetails> Details { get; init; } = [];

	/// <summary>Debugging information from the <c>innererror</c> object, flattened to text.</summary>
	public string? InnerError { get; init; }

	/// <inheritdoc />
	public override string ToString() => string.IsNullOrEmpty(Code) ? Message ?? string.Empty : $"{Code}: {Message}";
}
