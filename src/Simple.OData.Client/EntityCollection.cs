namespace Simple.OData.Client;

/// <summary>
/// Identifies an entity set, singleton or a derived-type projection of one (e.g. <c>Transports/NS.Ships</c>).
/// </summary>
/// <param name="Name">The exact name of the entity set, singleton or derived entity type.</param>
/// <param name="BaseEntityCollection">The collection this one derives from, when <paramref name="Name"/> is a derived type.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Established public name describing an OData entity collection, not a .NET collection type.")]
public sealed record EntityCollection(string Name, EntityCollection? BaseEntityCollection = null)
{
	/// <inheritdoc />
	public override string ToString() => Name;
}
