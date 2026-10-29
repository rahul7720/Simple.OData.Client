namespace Simple.OData.Client;

/// <summary>
/// A property bag holding the content of an OData entry. Used for untyped results and as the base of dynamic entries.
/// </summary>
public class ODataEntry
{
	private protected readonly Dictionary<string, object?> Entry;

	/// <summary>Initializes an empty entry.</summary>
	public ODataEntry()
	{
		Entry = new Dictionary<string, object?>(StringComparer.Ordinal);
	}

	/// <summary>Initializes an entry with a copy of the supplied properties.</summary>
	public ODataEntry(IDictionary<string, object?> entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		Entry = new Dictionary<string, object?>(entry, StringComparer.Ordinal);
	}

	private ODataEntry(Dictionary<string, object?> entry, bool wrap)
	{
		Entry = wrap ? entry : new Dictionary<string, object?>(entry, StringComparer.Ordinal);
	}

	/// <summary>Gets or sets the value of the named property.</summary>
	public object? this[string key]
	{
		get => Entry[key];
		set => Entry[key] = value;
	}

	/// <summary>The property names of the entry.</summary>
	public IEnumerable<string> Keys => Entry.Keys;

	/// <summary>The number of properties in the entry.</summary>
	public int Count => Entry.Count;

	/// <summary>Returns whether the entry contains the named property.</summary>
	public bool ContainsKey(string key) => Entry.ContainsKey(key);

	/// <summary>Gets the value of the named property when present.</summary>
	public bool TryGetValue(string key, out object? value) => Entry.TryGetValue(key, out value);

	/// <summary>Returns the underlying property dictionary (not a copy).</summary>
	public IDictionary<string, object?> AsDictionary() => Entry;

	/// <summary>Wraps a dictionary without copying it.</summary>
	public static ODataEntry FromDictionary(Dictionary<string, object?> entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		return new ODataEntry(entry, wrap: true);
	}

	/// <summary>Wraps a dictionary without copying it.</summary>
	public static explicit operator ODataEntry(Dictionary<string, object?> entry) => FromDictionary(entry);

	/// <summary>Returns the underlying property dictionary.</summary>
	public static explicit operator Dictionary<string, object?>(ODataEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		return entry.Entry;
	}
}
