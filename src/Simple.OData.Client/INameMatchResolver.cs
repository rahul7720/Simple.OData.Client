namespace Simple.OData.Client;

/// <summary>
/// Decides whether a name requested by the caller refers to a name declared in the service metadata.
/// </summary>
public interface INameMatchResolver
{
	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="requestedName"/> should be treated as referring to <paramref name="actualName"/>.
	/// </summary>
	/// <param name="actualName">The name declared in the metadata (possibly namespace-qualified).</param>
	/// <param name="requestedName">The name supplied by the caller.</param>
	bool IsMatch(string actualName, string requestedName);
}
