namespace Simple.OData.Client;

/// <summary>
/// Expansion depth values.
/// </summary>
public enum ODataExpandLevels
{
	/// <summary><c>$levels=max</c>.</summary>
	Max,
}

/// <summary>
/// Whether to expand entities by value or by reference (<c>$ref</c>).
/// </summary>
public enum ODataExpandMode
{
	/// <summary>Expand the related entities.</summary>
	ByValue,

	/// <summary>Expand only the related entity references.</summary>
	ByReference,
}

/// <summary>
/// Options applied to an <c>$expand</c>.
/// </summary>
public sealed class ODataExpandOptions : IEquatable<ODataExpandOptions>
{
	private ODataExpandOptions(int levels, ODataExpandMode expandMode)
	{
		Levels = levels;
		ExpandMode = expandMode;
	}

	/// <summary>The number of levels to expand; <c>0</c> means <c>max</c>.</summary>
	public int Levels { get; }

	/// <summary>By value or by reference.</summary>
	public ODataExpandMode ExpandMode { get; }

	/// <summary>Expand by value.</summary>
	public static ODataExpandOptions ByValue(int levels = 1) => new(levels, ODataExpandMode.ByValue);

	/// <summary>Expand by value to the maximum depth.</summary>
	public static ODataExpandOptions ByValue(ODataExpandLevels levels) => new(0, ODataExpandMode.ByValue);

	/// <summary>Expand by reference.</summary>
	public static ODataExpandOptions ByReference(int levels = 1) => new(levels, ODataExpandMode.ByReference);

	/// <summary>Expand by reference to the maximum depth.</summary>
	public static ODataExpandOptions ByReference(ODataExpandLevels levels) => new(0, ODataExpandMode.ByReference);

	/// <inheritdoc />
	public bool Equals(ODataExpandOptions? other) => other is not null && (ReferenceEquals(this, other) || (ExpandMode == other.ExpandMode && Levels == other.Levels));

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as ODataExpandOptions);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(ExpandMode, Levels);
}
