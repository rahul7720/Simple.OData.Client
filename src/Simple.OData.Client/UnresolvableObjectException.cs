namespace Simple.OData.Client;

/// <summary>
/// Thrown when a requested entity set, type, property, function or action does not exist in the service metadata.
/// </summary>
public sealed class UnresolvableObjectException : Exception
{
	/// <summary>Initializes a new instance.</summary>
	public UnresolvableObjectException()
	{
	}

	/// <summary>Initializes a new instance for the named object.</summary>
	public UnresolvableObjectException(string objectName)
		: base($"Metadata object [{objectName}] not found")
	{
		ObjectName = objectName;
	}

	/// <summary>Initializes a new instance for the named object with a message.</summary>
	public UnresolvableObjectException(string objectName, string message)
		: base(message)
	{
		ObjectName = objectName;
	}

	/// <summary>Initializes a new instance for the named object with a message and inner exception.</summary>
	public UnresolvableObjectException(string objectName, string message, Exception inner)
		: base(message, inner)
	{
		ObjectName = objectName;
	}

	/// <summary>The name that could not be resolved.</summary>
	public string? ObjectName { get; }
}
