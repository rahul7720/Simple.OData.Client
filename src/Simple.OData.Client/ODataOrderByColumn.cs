namespace Simple.OData.Client;

/// <summary>
/// A column of an <c>$orderby</c> clause.
/// </summary>
public sealed record ODataOrderByColumn
{
	/// <summary>Creates an order-by column.</summary>
	public ODataOrderByColumn(string name, bool descending)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
		Descending = descending;
	}

	/// <summary>The property path.</summary>
	public string Name { get; }

	/// <summary>Whether the sort is descending.</summary>
	public bool Descending { get; }

	/// <inheritdoc />
	public override string ToString() => Descending ? Name + " desc" : Name;
}
