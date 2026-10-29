using System.Collections.Frozen;

namespace Simple.OData.Client.Expressions;

/// <summary>
/// Maps CLR method and property names (by name and arity) to OData V4/4.01 built-in functions.
/// </summary>
internal sealed class FunctionMapping
{
	private enum ArgumentOrder
	{
		/// <summary>f(target)</summary>
		Target,

		/// <summary>f(target, args…)</summary>
		TargetThenArguments,

		/// <summary>f(args…, target)</summary>
		ArgumentsThenTarget,
	}

	private static readonly FrozenDictionary<(string Name, int ArgumentCount), FunctionMapping> Definitions = BuildDefinitions();

	private readonly ArgumentOrder _order;

	private FunctionMapping(string mappedName, int argumentCount, ArgumentOrder order)
	{
		MappedName = mappedName;
		ArgumentCount = argumentCount;
		_order = order;
	}

	/// <summary>The OData function name.</summary>
	public string MappedName { get; }

	/// <summary>The number of arguments beside the target.</summary>
	public int ArgumentCount { get; }

	public static bool ContainsFunction(string functionName, int argumentCount) => Definitions.ContainsKey((functionName, argumentCount));

	public static bool TryGet(string functionName, int argumentCount, out FunctionMapping mapping) => Definitions.TryGetValue((functionName, argumentCount), out mapping!);

	/// <summary>Builds the OData function call for a target and its arguments.</summary>
	public ExpressionFunction Map(ODataExpression? target, IReadOnlyList<ODataExpression?> arguments)
	{
		var mapped = new List<ODataExpression?>(arguments.Count + 1);
		switch (_order)
		{
			case ArgumentOrder.Target:
				mapped.Add(target);
				break;
			case ArgumentOrder.TargetThenArguments:
				mapped.Add(target);
				mapped.AddRange(arguments);
				break;
			default:
				mapped.AddRange(arguments);
				mapped.Add(target);
				break;
		}

		return new ExpressionFunction(MappedName, mapped);
	}

	private static FrozenDictionary<(string Name, int ArgumentCount), FunctionMapping> BuildDefinitions()
	{
		var definitions = new Dictionary<(string, int), FunctionMapping>(new KeyComparer());
		void Add(string name, int argumentCount, string mappedName, ArgumentOrder order = ArgumentOrder.TargetThenArguments) =>
			definitions[(name, argumentCount)] = new FunctionMapping(mappedName, argumentCount, order);

		// string functions
		Add("Contains", 1, "contains");
		Add("StartsWith", 1, "startswith");
		Add("EndsWith", 1, "endswith");
		Add("Length", 0, "length", ArgumentOrder.Target);
		Add("IndexOf", 1, "indexof");
		Add("Replace", 2, "replace");
		Add("Substring", 1, "substring");
		Add("Substring", 2, "substring");
		Add("ToLower", 0, "tolower", ArgumentOrder.Target);
		Add("ToLowerInvariant", 0, "tolower", ArgumentOrder.Target);
		Add("ToUpper", 0, "toupper", ArgumentOrder.Target);
		Add("ToUpperInvariant", 0, "toupper", ArgumentOrder.Target);
		Add("Trim", 0, "trim", ArgumentOrder.Target);
		Add("Concat", 1, "concat");
		Add("IsMatch", 1, "matchesPattern");
		Add("MatchesPattern", 1, "matchesPattern");

		// date and time functions
		Add("Year", 0, "year", ArgumentOrder.Target);
		Add("Month", 0, "month", ArgumentOrder.Target);
		Add("Day", 0, "day", ArgumentOrder.Target);
		Add("Hour", 0, "hour", ArgumentOrder.Target);
		Add("Minute", 0, "minute", ArgumentOrder.Target);
		Add("Second", 0, "second", ArgumentOrder.Target);
		Add("Millisecond", 0, "fractionalseconds", ArgumentOrder.Target);
		Add("FractionalSeconds", 0, "fractionalseconds", ArgumentOrder.Target);
		Add("Date", 0, "date", ArgumentOrder.Target);
		Add("TimeOfDay", 0, "time", ArgumentOrder.Target);
		Add("Time", 0, "time", ArgumentOrder.Target);
		Add("TotalOffsetMinutes", 0, "totaloffsetminutes", ArgumentOrder.Target);
		Add("TotalSeconds", 0, "totalseconds", ArgumentOrder.Target);
		Add("Now", 0, "now", ArgumentOrder.Target);
		Add("MaxDateTime", 0, "maxdatetime", ArgumentOrder.Target);
		Add("MinDateTime", 0, "mindatetime", ArgumentOrder.Target);

		// arithmetic functions
		Add("Round", 0, "round", ArgumentOrder.Target);
		Add("Floor", 0, "floor", ArgumentOrder.Target);
		Add("Ceiling", 0, "ceiling", ArgumentOrder.Target);

		// collection functions (4.01)
		Add("HasSubset", 1, "hassubset");
		Add("HasSubsequence", 1, "hassubsequence");

		// geo functions
		Add("GeoDistance", 1, "geo.distance");
		Add("GeoLength", 0, "geo.length", ArgumentOrder.Target);
		Add("GeoIntersects", 1, "geo.intersects");

		return definitions.ToFrozenDictionary(new KeyComparer());
	}

	private sealed class KeyComparer : IEqualityComparer<(string Name, int ArgumentCount)>
	{
		public bool Equals((string Name, int ArgumentCount) x, (string Name, int ArgumentCount) y) =>
			x.ArgumentCount == y.ArgumentCount && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

		public int GetHashCode((string Name, int ArgumentCount) obj) =>
			HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name), obj.ArgumentCount);
	}
}
