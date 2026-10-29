using System.Collections.Concurrent;

namespace Simple.OData.Client.Commands;

/// <summary>Mutable state accumulated by <see cref="FluentCommand"/> and consumed by <see cref="ResolvedCommand"/>.</summary>
internal sealed class CommandDetails
{
	public CommandDetails(CommandDetails? parent, ConcurrentDictionary<object, IDictionary<string, object?>>? batchEntries)
	{
		Parent = parent;
		BatchEntries = batchEntries;
	}

	public CommandDetails(CommandDetails source)
	{
		ArgumentNullException.ThrowIfNull(source);
		Parent = source.Parent;
		BatchEntries = source.BatchEntries;
		CollectionName = source.CollectionName;
		CollectionExpression = source.CollectionExpression;
		DerivedCollectionName = source.DerivedCollectionName;
		DerivedCollectionExpression = source.DerivedCollectionExpression;
		DynamicPropertiesContainerName = source.DynamicPropertiesContainerName;
		FunctionName = source.FunctionName;
		ActionName = source.ActionName;
		IsAlternateKey = source.IsAlternateKey;
		KeyValues = source.KeyValues is null ? null : [.. source.KeyValues];
		NamedKeyValues = source.NamedKeyValues is null ? null : new Dictionary<string, object?>(source.NamedKeyValues);
		EntryValue = source.EntryValue;
		EntryData = source.EntryData;
		Filter = source.Filter;
		FilterExpression = source.FilterExpression;
		Search = source.Search;
		SkipCount = source.SkipCount;
		TopCount = source.TopCount;
		Index = source.Index;
		ExpandAssociations = [.. source.ExpandAssociations];
		SelectColumns = [.. source.SelectColumns];
		OrderByColumns = [.. source.OrderByColumns];
		ComputeText = source.ComputeText;
		ComputeExpressions = [.. source.ComputeExpressions];
		ApplyClauses = [.. source.ApplyClauses];
		ComputeCount = source.ComputeCount;
		IncludeCount = source.IncludeCount;
		UseEachSegment = source.UseEachSegment;
		LinkName = source.LinkName;
		LinkExpression = source.LinkExpression;
		QueryOptions = source.QueryOptions;
		QueryOptionsExpression = source.QueryOptionsExpression;
		QueryOptionsKeyValues = source.QueryOptionsKeyValues;
		MediaName = source.MediaName;
		MediaProperties = [.. source.MediaProperties];
		Headers = new Dictionary<string, string>(source.Headers, StringComparer.OrdinalIgnoreCase);
		MaxPageSize = source.MaxPageSize;
		TrackChanges = source.TrackChanges;
	}

	public CommandDetails? Parent { get; }

	public ConcurrentDictionary<object, IDictionary<string, object?>>? BatchEntries { get; }

	public string? CollectionName { get; set; }

	public ODataExpression? CollectionExpression { get; set; }

	public string? DerivedCollectionName { get; set; }

	public ODataExpression? DerivedCollectionExpression { get; set; }

	public string? DynamicPropertiesContainerName { get; set; }

	public string? FunctionName { get; set; }

	public string? ActionName { get; set; }

	public bool IsAlternateKey { get; set; }

	public List<object?>? KeyValues { get; set; }

	public Dictionary<string, object?>? NamedKeyValues { get; set; }

	public object? EntryValue { get; set; }

	public IDictionary<string, object?>? EntryData { get; set; }

	public string? Filter { get; set; }

	public ODataExpression? FilterExpression { get; set; }

	public string? Search { get; set; }

	public long SkipCount { get; set; } = -1;

	public long TopCount { get; set; } = -1;

	public long? Index { get; set; }

	public List<(ODataExpandAssociation Association, ODataExpandOptions Options)> ExpandAssociations { get; } = [];

	public List<string> SelectColumns { get; } = [];

	public List<ODataOrderByColumn> OrderByColumns { get; } = [];

	public string? ComputeText { get; set; }

	public List<(string Alias, ODataExpression Expression)> ComputeExpressions { get; } = [];

	/// <summary>Successive <c>$apply</c> pipelines, each a <see cref="string"/> or an <see cref="IApplyBuilder"/>, joined with <c>/</c>.</summary>
	public List<object> ApplyClauses { get; } = [];

	public bool ComputeCount { get; set; }

	public bool IncludeCount { get; set; }

	public bool UseEachSegment { get; set; }

	public string? LinkName { get; set; }

	public ODataExpression? LinkExpression { get; set; }

	public string? QueryOptions { get; set; }

	public ODataExpression? QueryOptionsExpression { get; set; }

	public IDictionary<string, object?>? QueryOptionsKeyValues { get; set; }

	public string? MediaName { get; set; }

	public List<string> MediaProperties { get; } = [];

	public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

	public int? MaxPageSize { get; set; }

	public bool TrackChanges { get; set; }

	public bool HasKey => KeyValues is { Count: > 0 } || NamedKeyValues is { Count: > 0 };

	public bool HasFilter => !string.IsNullOrEmpty(Filter) || FilterExpression is not null;

	public bool HasSearch => !string.IsNullOrEmpty(Search);

	public bool HasFunction => !string.IsNullOrEmpty(FunctionName);

	public bool HasAction => !string.IsNullOrEmpty(ActionName);

	public bool HasCompute => !string.IsNullOrEmpty(ComputeText) || ComputeExpressions.Count > 0;
}
