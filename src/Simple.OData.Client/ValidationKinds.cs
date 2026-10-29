namespace Simple.OData.Client;

/// <summary>
/// Payload validations performed by the underlying OData library. Mirrors <c>Microsoft.OData.ValidationKinds</c>.
/// </summary>
[Flags]
public enum ValidationKinds
{
	/// <summary>No validation.</summary>
	None = 0,

	/// <summary>Reject duplicate property names in a resource.</summary>
	ThrowOnDuplicatePropertyNames = 1,

	/// <summary>Reject undeclared properties on non-open types.</summary>
	ThrowOnUndeclaredPropertyForNonOpenType = 2,

	/// <summary>Reject payload types that conflict with the model.</summary>
	ThrowIfTypeConflictsWithMetadata = 4,

	/// <summary>All validations (default).</summary>
	All = ~0,
}
