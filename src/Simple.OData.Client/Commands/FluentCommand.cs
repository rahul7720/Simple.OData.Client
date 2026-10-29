using System.Collections.Concurrent;

namespace Simple.OData.Client.Commands;

/// <summary>
/// Accumulates the parts of an OData request (resource path, key, query options, payload) before it is
/// resolved against metadata by <see cref="Resolve"/>.
/// </summary>
public sealed class FluentCommand
{
	/// <summary>Creates an empty root command.</summary>
	public FluentCommand()
		: this(null, null)
	{
	}

	internal FluentCommand(FluentCommand? parent, ConcurrentDictionary<object, IDictionary<string, object?>>? batchEntries)
	{
		Details = new CommandDetails(parent?.Details, batchEntries);
	}

	internal FluentCommand(CommandDetails details)
	{
		Details = new CommandDetails(details);
	}

	/// <summary>Creates a command from a resolved one; the filter expression has already been folded into its text or key.</summary>
	internal FluentCommand(ResolvedCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		Details = new CommandDetails(command.Details) { FilterExpression = null };
	}

	internal CommandDetails Details { get; }

	/// <summary>The property that holds dynamic (open type) properties, when set with <see cref="WithProperties"/>.</summary>
	public string? DynamicPropertiesContainerName => Details.DynamicPropertiesContainerName;

	internal ResolvedCommand Resolve(Session session) => new(this, session);

	/// <summary>Targets an entity set, singleton or <c>Set/DerivedType</c> path.</summary>
	public FluentCommand For(string collectionName)
	{
		ArgumentException.ThrowIfNullOrEmpty(collectionName);
		var separator = collectionName.IndexOf('/');
		if (separator > 0)
		{
			Details.CollectionName = collectionName[..separator];
			Details.DerivedCollectionName = collectionName[(separator + 1)..];
		}
		else
		{
			Details.CollectionName = collectionName;
		}

		return this;
	}

	/// <summary>Targets the collection named by a dynamic expression.</summary>
	public FluentCommand For(ODataExpression expression)
	{
		Details.CollectionExpression = expression ?? throw new ArgumentNullException(nameof(expression));
		return this;
	}

	/// <summary>Restricts the collection to a derived entity type.</summary>
	public FluentCommand As(string derivedCollectionName)
	{
		ArgumentException.ThrowIfNullOrEmpty(derivedCollectionName);
		Details.DerivedCollectionName = derivedCollectionName;
		return this;
	}

	/// <summary>Restricts the collection to the derived type named by a dynamic expression.</summary>
	public FluentCommand As(ODataExpression expression)
	{
		Details.DerivedCollectionExpression = expression ?? throw new ArgumentNullException(nameof(expression));
		return this;
	}

	/// <summary>Navigates from the parent command along a navigation property.</summary>
	public FluentCommand Link(string linkName)
	{
		ArgumentException.ThrowIfNullOrEmpty(linkName);
		Details.LinkName = linkName;
		return this;
	}

	/// <summary>Navigates from the parent command along the navigation property named by a dynamic expression.</summary>
	public FluentCommand Link(ODataExpression expression)
	{
		Details.LinkExpression = expression ?? throw new ArgumentNullException(nameof(expression));
		return this;
	}

	/// <summary>Sets positional key values, a single anonymous object or an entity carrying its key properties.</summary>
	public FluentCommand Key(params object?[] key) => Key((IEnumerable<object?>)key);

	/// <summary>Sets positional key values in declared key order.</summary>
	public FluentCommand Key(IEnumerable<object?> key)
	{
		ArgumentNullException.ThrowIfNull(key);
		Details.KeyValues = [.. key];
		Details.NamedKeyValues = null;
		Details.IsAlternateKey = false;
		return this;
	}

	/// <summary>Sets named key values; alternate keys are recognised from metadata.</summary>
	public FluentCommand Key(IDictionary<string, object?> key)
	{
		ArgumentNullException.ThrowIfNull(key);
		Details.KeyValues = null;
		Details.NamedKeyValues = new Dictionary<string, object?>(key);
		Details.IsAlternateKey = false;
		return this;
	}

	/// <summary>Adds a raw <c>$filter</c> text; successive filters are combined with <c>and</c>.</summary>
	public FluentCommand Filter(string filter)
	{
		ArgumentException.ThrowIfNullOrEmpty(filter);
		Details.Filter = string.IsNullOrEmpty(Details.Filter) ? filter : $"({Details.Filter}) and ({filter})";
		return this;
	}

	/// <summary>Adds a filter expression; successive filters are combined with <c>and</c>.</summary>
	public FluentCommand Filter(ODataExpression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		Details.FilterExpression = Details.FilterExpression is null ? expression : Details.FilterExpression && expression;
		return this;
	}

	/// <summary>Sets the <c>$search</c> expression.</summary>
	public FluentCommand Search(string search)
	{
		ArgumentException.ThrowIfNullOrEmpty(search);
		Details.Search = search;
		return this;
	}

	/// <summary>Sets <c>$skip</c>.</summary>
	public FluentCommand Skip(long count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		Details.SkipCount = count;
		return this;
	}

	/// <summary>Sets <c>$top</c>. When a key is present only a value of 1 is accepted.</summary>
	public FluentCommand Top(long count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		if (!Details.HasKey || Details.HasFunction)
		{
			Details.TopCount = count;
		}
		else if (count != 1)
		{
			throw new InvalidOperationException("Top count may only be assigned to 1 when key is assigned.");
		}

		return this;
	}

	/// <summary>Sets the OData 4.01 <c>$index</c> option.</summary>
	public FluentCommand Index(long index)
	{
		Details.Index = index;
		return this;
	}

	/// <summary>Expands all navigation properties (<c>$expand=*</c>) with the given options.</summary>
	public FluentCommand Expand(ODataExpandOptions expandOptions)
	{
		ArgumentNullException.ThrowIfNull(expandOptions);
		Details.ExpandAssociations.Add((new ODataExpandAssociation(ODataLiteral.Star), expandOptions));
		return this;
	}

	/// <summary>Expands the given associations by value.</summary>
	public FluentCommand Expand(IEnumerable<ODataExpandAssociation> associations) => Expand(ODataExpandOptions.ByValue(), associations);

	/// <summary>Expands the given associations with the given options.</summary>
	public FluentCommand Expand(ODataExpandOptions expandOptions, IEnumerable<ODataExpandAssociation> associations)
	{
		ArgumentNullException.ThrowIfNull(expandOptions);
		ArgumentNullException.ThrowIfNull(associations);
		foreach (var association in associations)
		{
			Details.ExpandAssociations.Add((association, expandOptions));
		}

		return this;
	}

	/// <summary>
	/// Expands navigation paths given as text. A path may carry inline options, e.g.
	/// <c>Products($filter=UnitPrice gt 10;$top=5)</c>, which are passed through verbatim.
	/// </summary>
	public FluentCommand Expand(params string[] paths)
	{
		ArgumentNullException.ThrowIfNull(paths);
		return Expand(paths.SelectMany(SplitExpandPaths).Select(ODataExpandAssociation.From));
	}

	/// <summary>Expands the navigation properties named by dynamic expressions.</summary>
	public FluentCommand Expand(params ODataExpression[] associations)
	{
		ArgumentNullException.ThrowIfNull(associations);
		return Expand(associations.Select(x => new ODataExpandAssociation(ReferenceOf(x))));
	}

	/// <summary>Adds <c>$select</c> columns; comma-separated lists are split.</summary>
	public FluentCommand Select(IEnumerable<string> columns)
	{
		ArgumentNullException.ThrowIfNull(columns);
		Details.SelectColumns.AddRange(SplitItems(columns));
		return this;
	}

	/// <summary>Adds <c>$select</c> columns; comma-separated lists are split.</summary>
	public FluentCommand Select(params string[] columns) => Select((IEnumerable<string>)columns);

	/// <summary>Adds <c>$select</c> columns named by dynamic expressions.</summary>
	public FluentCommand Select(params ODataExpression[] columns)
	{
		ArgumentNullException.ThrowIfNull(columns);
		return Select(columns.Select(ReferenceOf));
	}

	/// <summary>Appends <c>$orderby</c> columns.</summary>
	public FluentCommand OrderBy(IEnumerable<ODataOrderByColumn> columns)
	{
		ArgumentNullException.ThrowIfNull(columns);
		Details.OrderByColumns.AddRange(columns);
		return this;
	}

	/// <summary>Appends ascending <c>$orderby</c> columns; comma-separated lists are split.</summary>
	public FluentCommand OrderBy(params string[] columns) => OrderBy(ToOrderBy(columns, descending: false));

	/// <summary>Appends ascending <c>$orderby</c> columns named by dynamic expressions.</summary>
	public FluentCommand OrderBy(params ODataExpression[] columns) => OrderBy(ToOrderBy(columns, descending: false));

	/// <summary>Appends ascending <c>$orderby</c> columns.</summary>
	public FluentCommand ThenBy(params string[] columns) => OrderBy(columns);

	/// <summary>Appends ascending <c>$orderby</c> columns named by dynamic expressions.</summary>
	public FluentCommand ThenBy(params ODataExpression[] columns) => OrderBy(columns);

	/// <summary>Appends descending <c>$orderby</c> columns; comma-separated lists are split.</summary>
	public FluentCommand OrderByDescending(params string[] columns) => OrderBy(ToOrderBy(columns, descending: true));

	/// <summary>Appends descending <c>$orderby</c> columns named by dynamic expressions.</summary>
	public FluentCommand OrderByDescending(params ODataExpression[] columns) => OrderBy(ToOrderBy(columns, descending: true));

	/// <summary>Appends descending <c>$orderby</c> columns.</summary>
	public FluentCommand ThenByDescending(params string[] columns) => OrderByDescending(columns);

	/// <summary>Appends descending <c>$orderby</c> columns named by dynamic expressions.</summary>
	public FluentCommand ThenByDescending(params ODataExpression[] columns) => OrderByDescending(columns);

	/// <summary>Adds raw <c>$compute</c> text such as <c>UnitPrice mul 2 as Doubled</c>.</summary>
	public FluentCommand Compute(string compute)
	{
		ArgumentException.ThrowIfNullOrEmpty(compute);
		Details.ComputeText = string.IsNullOrEmpty(Details.ComputeText) ? compute : $"{Details.ComputeText},{compute}";
		return this;
	}

	/// <summary>Adds computed properties, each rendered as <c>expression as Alias</c>.</summary>
	public FluentCommand Compute(IReadOnlyList<(string Alias, ODataExpression Expression)> computedProperties)
	{
		ArgumentNullException.ThrowIfNull(computedProperties);
		Details.ComputeExpressions.AddRange(computedProperties);
		return this;
	}

	/// <summary>Appends raw <c>$apply</c> text; successive calls are chained with <c>/</c>.</summary>
	public FluentCommand Apply(string apply)
	{
		ArgumentException.ThrowIfNullOrEmpty(apply);
		Details.ApplyClauses.Add(apply);
		return this;
	}

	internal FluentCommand Apply(IApplyBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		Details.ApplyClauses.Add(builder);
		return this;
	}

	/// <summary>Appends raw query option text (without the leading <c>?</c>).</summary>
	public FluentCommand QueryOptions(string queryOptions)
	{
		ArgumentException.ThrowIfNullOrEmpty(queryOptions);
		Details.QueryOptions = Details.QueryOptions is null ? queryOptions : $"{Details.QueryOptions}&{queryOptions}";
		return this;
	}

	/// <summary>Adds custom query options rendered as <c>name=literal</c>.</summary>
	public FluentCommand QueryOptions(IDictionary<string, object?> queryOptions)
	{
		Details.QueryOptionsKeyValues = queryOptions ?? throw new ArgumentNullException(nameof(queryOptions));
		return this;
	}

	/// <summary>Adds custom query options from a dynamic expression.</summary>
	public FluentCommand QueryOptions(ODataExpression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		Details.QueryOptionsExpression = Details.QueryOptionsExpression is null ? expression : Details.QueryOptionsExpression && expression;
		return this;
	}

	/// <summary>Targets the media entity stream (<c>/$value</c>).</summary>
	public FluentCommand Media() => Media(ReservedNames.MediaEntity);

	/// <summary>Targets a named stream property.</summary>
	public FluentCommand Media(string streamName)
	{
		ArgumentException.ThrowIfNullOrEmpty(streamName);
		Details.MediaName = streamName;
		return this;
	}

	/// <summary>Targets the stream property named by a dynamic expression.</summary>
	public FluentCommand Media(ODataExpression expression) => Media(ReferenceOf(expression));

	/// <summary>Requests only the count of matching entities (<c>/$count</c>).</summary>
	public FluentCommand Count()
	{
		Details.ComputeCount = true;
		return this;
	}

	/// <summary>Requests the total count alongside the results (<c>$count=true</c>).</summary>
	public FluentCommand WithCount()
	{
		Details.IncludeCount = true;
		return this;
	}

	/// <summary>Targets every member of the collection (<c>/$each</c>, OData 4.01).</summary>
	internal FluentCommand Each()
	{
		Details.UseEachSegment = true;
		return this;
	}

	/// <summary>Sets the request payload from an object whose properties become entry values.</summary>
	public FluentCommand Set(object value)
	{
		ArgumentNullException.ThrowIfNull(value);
		if (value is IDictionary<string, object?> dictionary)
		{
			return Set(dictionary);
		}

		Details.EntryValue = value;
		return this;
	}

	/// <summary>Sets the request payload from named values.</summary>
	public FluentCommand Set(IDictionary<string, object?> value)
	{
		Details.EntryData = value ?? throw new ArgumentNullException(nameof(value));
		return this;
	}

	/// <summary>Sets the request payload from dynamic <c>property = value</c> expressions.</summary>
	public FluentCommand Set(params ODataExpression[] value)
	{
		ArgumentNullException.ThrowIfNull(value);
		var data = new Dictionary<string, object?>(value.Length);
		foreach (var expression in value)
		{
			data[ReferenceOf(expression)] = expression.Value;
		}

		Details.EntryData = data;
		Details.BatchEntries?.GetOrAdd(value, data);
		return this;
	}

	/// <summary>Calls a function.</summary>
	public FluentCommand Function(string functionName)
	{
		ArgumentException.ThrowIfNullOrEmpty(functionName);
		Details.FunctionName = functionName;
		return this;
	}

	/// <summary>Calls an action.</summary>
	public FluentCommand Action(string actionName)
	{
		ArgumentException.ThrowIfNullOrEmpty(actionName);
		Details.ActionName = actionName;
		return this;
	}

	/// <summary>Adds a request header.</summary>
	public FluentCommand WithHeader(string name, string value)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Details.Headers[name] = value;
		return this;
	}

	/// <summary>Adds request headers.</summary>
	public FluentCommand WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		foreach (var (name, value) in headers)
		{
			Details.Headers[name] = value;
		}

		return this;
	}

	/// <summary>Sets the <c>If-Match</c> header (optimistic concurrency).</summary>
	public FluentCommand WithIfMatch(string etag) => WithHeader(HttpLiteral.IfMatch, etag);

	/// <summary>Sets the <c>If-None-Match</c> header.</summary>
	public FluentCommand WithIfNoneMatch(string etag) => WithHeader(HttpLiteral.IfNoneMatch, etag);

	/// <summary>Requests server-driven paging with at most <paramref name="maxPageSize"/> items per page.</summary>
	public FluentCommand WithMaxPageSize(int maxPageSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPageSize);
		Details.MaxPageSize = maxPageSize;
		return this;
	}

	/// <summary>Requests a delta link for change tracking (<c>Prefer: odata.track-changes</c>).</summary>
	public FluentCommand TrackChanges()
	{
		Details.TrackChanges = true;
		return this;
	}

	/// <summary>Names the property that holds dynamic (open type) properties.</summary>
	public FluentCommand WithProperties(string propertyName)
	{
		ArgumentException.ThrowIfNullOrEmpty(propertyName);
		Details.DynamicPropertiesContainerName = propertyName;
		return this;
	}

	/// <summary>Names the stream properties to fetch with the entry.</summary>
	public FluentCommand WithMedia(IEnumerable<string> properties)
	{
		ArgumentNullException.ThrowIfNull(properties);
		Details.MediaProperties.Clear();
		Details.MediaProperties.AddRange(SplitItems(properties));
		return this;
	}

	/// <summary>Names the stream properties to fetch with the entry.</summary>
	public FluentCommand WithMedia(params string[] properties) => WithMedia((IEnumerable<string>)properties);

	/// <summary>Names the stream properties to fetch with the entry.</summary>
	public FluentCommand WithMedia(params ODataExpression[] properties)
	{
		ArgumentNullException.ThrowIfNull(properties);
		return WithMedia(properties.Select(ReferenceOf));
	}

	internal IReadOnlyList<string> SelectedColumns => Details.SelectColumns;

	private static string ReferenceOf(ODataExpression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		return expression.Reference ?? throw new ArgumentException("The expression must be a property reference.", nameof(expression));
	}

	private static IEnumerable<string> SplitItems(IEnumerable<string> items) =>
		items.SelectMany(x => x.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

	private static IEnumerable<string> SplitExpandPaths(string path) =>
		path.Contains('(') ? [path.Trim()] : path.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	private static IEnumerable<ODataOrderByColumn> ToOrderBy(string[] columns, bool descending)
	{
		ArgumentNullException.ThrowIfNull(columns);
		return SplitItems(columns).Select(x => new ODataOrderByColumn(x, descending));
	}

	private static IEnumerable<ODataOrderByColumn> ToOrderBy(ODataExpression[] columns, bool descending)
	{
		ArgumentNullException.ThrowIfNull(columns);
		return columns.Select(x => new ODataOrderByColumn(ReferenceOf(x), descending));
	}
}
