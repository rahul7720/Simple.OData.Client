namespace Simple.OData.Client;

/// <summary>
/// A navigation property to expand, with its nested expansions and inline query options.
/// </summary>
public sealed class ODataExpandAssociation : IEquatable<ODataExpandAssociation>
{
	/// <summary>Creates an expansion of the named navigation property.</summary>
	public ODataExpandAssociation(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	/// <summary>The navigation property name (or <c>*</c>).</summary>
	public string Name { get; }

	/// <summary>Nested expansions (<c>$expand</c> inside this expansion).</summary>
	public List<ODataExpandAssociation> ExpandAssociations { get; } = [];

	/// <summary>Nested <c>$orderby</c> columns.</summary>
	public List<ODataOrderByColumn> OrderByColumns { get; } = [];

	/// <summary>Nested <c>$filter</c>.</summary>
	public ODataExpression? FilterExpression { get; set; }

	/// <summary>Nested <c>$top</c>.</summary>
	public int? Top { get; set; }

	/// <summary>Nested <c>$skip</c>.</summary>
	public int? Skip { get; set; }

	/// <summary>Nested <c>$count=true</c>.</summary>
	public bool Count { get; set; }

	/// <summary>Nested <c>$search</c>.</summary>
	public string? Search { get; set; }

	/// <summary>Raw inline options written verbatim, e.g. <c>$filter=Price gt 10;$top=5</c>.</summary>
	public string? RawOptions { get; set; }

	/// <summary>Builds a chain of expansions from a <c>/</c>-separated path such as <c>Category/Products</c>.</summary>
	public static ODataExpandAssociation From(string association)
	{
		ArgumentException.ThrowIfNullOrEmpty(association);
		var path = association;
		string? rawOptions = null;
		var parenthesis = association.IndexOf('(');
		if (parenthesis > 0 && association.EndsWith(')'))
		{
			path = association[..parenthesis];
			rawOptions = association[(parenthesis + 1)..^1];
		}

		var items = path.Split('/');
		var root = new ODataExpandAssociation(items[0]);
		var current = root;
		for (var i = 1; i < items.Length; i++)
		{
			var next = new ODataExpandAssociation(items[i]);
			current.ExpandAssociations.Add(next);
			current = next;
		}

		current.RawOptions = rawOptions;
		return root;
	}

	/// <summary>Deep-copies the expansion.</summary>
	public ODataExpandAssociation Clone()
	{
		var clone = new ODataExpandAssociation(Name)
		{
			FilterExpression = FilterExpression,
			Top = Top,
			Skip = Skip,
			Count = Count,
			Search = Search,
			RawOptions = RawOptions,
		};
		clone.ExpandAssociations.AddRange(ExpandAssociations.Select(x => x.Clone()));
		clone.OrderByColumns.AddRange(OrderByColumns);
		return clone;
	}

	/// <inheritdoc />
	public bool Equals(ODataExpandAssociation? other) => other is not null && (ReferenceEquals(this, other) || Name == other.Name);

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as ODataExpandAssociation);

	/// <inheritdoc />
	public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
}
