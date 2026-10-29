using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Simple.OData.Client.Aggregation;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

/// <summary>
/// The shared implementation of the fluent clients. Public so that dynamically bound calls (<c>client.For(x.Products)</c>)
/// can resolve its members at runtime; it cannot be derived from outside the library.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public abstract class FluentClientBase<T, TClient> : IFluentClient<T, TClient>
	where T : class
	where TClient : class
{
	private readonly FluentCommand? _parentCommand;
	private FluentCommand? _command;

	internal FluentClientBase(ODataClient client, FluentCommand? parentCommand = null, FluentCommand? command = null, bool dynamicResults = false)
	{
		Client = client ?? throw new ArgumentNullException(nameof(client));
		_parentCommand = parentCommand;
		_command = command;
		DynamicResults = dynamicResults;
	}

	internal ODataClient Client { get; }

	internal Session Session => Client.Session;

	internal bool DynamicResults { get; }

	internal FluentCommand Command => _command ??= new FluentCommand(_parentCommand, Client.BatchEntries);

	private protected FluentCommand? ParentCommand => _parentCommand;

	private protected ITypeCache TypeCache => Session.TypeCache;

	private TClient Self => (TClient)(object)this;

	// ---- request shape -----------------------------------------------------------------------------------------

	/// <inheritdoc />
	public TClient WithProperties(Expression<Func<T, IDictionary<string, object?>>> expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		Command.WithProperties(expression.ExtractColumnName(TypeCache));
		return Self;
	}

	/// <inheritdoc />
	public TClient WithMedia(IEnumerable<string> properties)
	{
		Command.WithMedia(properties);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithMedia(params string[] properties)
	{
		Command.WithMedia(properties);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithMedia(params ODataExpression[] properties)
	{
		Command.WithMedia(properties);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithMedia(Expression<Func<T, object>> properties)
	{
		Command.WithMedia(properties.ExtractColumnNames(TypeCache));
		return Self;
	}

	/// <inheritdoc />
	public TClient Key(params object?[] entryKey)
	{
		Command.Key(entryKey);
		return Self;
	}

	/// <inheritdoc />
	public TClient Key(IEnumerable<object?> entryKey)
	{
		Command.Key(entryKey);
		return Self;
	}

	/// <inheritdoc />
	public TClient Key(IDictionary<string, object?> entryKey)
	{
		Command.Key(entryKey);
		return Self;
	}

	/// <inheritdoc />
	public TClient Key(T entryKey)
	{
		ArgumentNullException.ThrowIfNull(entryKey);
		Command.Key(entryKey is IDictionary<string, object?> dictionary ? dictionary : TypeCache.ToDictionary(entryKey));
		return Self;
	}

	/// <inheritdoc />
	public TClient Filter(string filter)
	{
		Command.Filter(filter);
		return Self;
	}

	/// <inheritdoc />
	public TClient Filter(ODataExpression expression)
	{
		Command.Filter(expression);
		return Self;
	}

	/// <inheritdoc />
	public TClient Filter(Expression<Func<T, bool>> expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		Command.Filter(ODataExpression.FromLinqExpression(expression.Body));
		return Self;
	}

	/// <inheritdoc />
	public TClient Search(string search)
	{
		Command.Search(search);
		return Self;
	}

	/// <inheritdoc />
	public TClient Function(string functionName)
	{
		Command.Function(functionName);
		return Self;
	}

	/// <inheritdoc />
	public IBoundClient<TResult> Function<TResult>(string functionName)
		where TResult : class
	{
		Command.Function(functionName);
		return new BoundClient<TResult>(Client, _parentCommand, Command, DynamicResults);
	}

	/// <inheritdoc />
	public TClient Action(string actionName)
	{
		Command.Action(actionName);
		return Self;
	}

	/// <inheritdoc />
	public TClient Skip(long count)
	{
		Command.Skip(count);
		return Self;
	}

	/// <inheritdoc />
	public TClient Top(long count)
	{
		Command.Top(count);
		return Self;
	}

	/// <inheritdoc />
	public TClient Index(long index)
	{
		Command.Index(index);
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(ODataExpandOptions expandOptions)
	{
		Command.Expand(expandOptions);
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(IEnumerable<string> associations)
	{
		ArgumentNullException.ThrowIfNull(associations);
		Command.Expand([.. associations]);
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(ODataExpandOptions expandOptions, IEnumerable<string> associations)
	{
		ArgumentNullException.ThrowIfNull(associations);
		Command.Expand(expandOptions, associations.Select(ODataExpandAssociation.From));
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(params string[] associations)
	{
		Command.Expand(associations);
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(ODataExpandOptions expandOptions, params string[] associations)
	{
		ArgumentNullException.ThrowIfNull(associations);
		Command.Expand(expandOptions, associations.Select(ODataExpandAssociation.From));
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(params ODataExpression[] associations)
	{
		Command.Expand(associations);
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(ODataExpandOptions expandOptions, params ODataExpression[] associations)
	{
		ArgumentNullException.ThrowIfNull(associations);
		Command.Expand(expandOptions, associations.Select(x => ODataExpandAssociation.From(ReferenceOf(x))));
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(Expression<Func<T, object>> expression)
	{
		Command.Expand(expression.ExtractExpandAssociations(TypeCache));
		return Self;
	}

	/// <inheritdoc />
	public TClient Expand(ODataExpandOptions expandOptions, Expression<Func<T, object>> expression)
	{
		Command.Expand(expandOptions, expression.ExtractExpandAssociations(TypeCache));
		return Self;
	}

	/// <inheritdoc />
	public TClient Select(IEnumerable<string> columns)
	{
		Command.Select(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient Select(params string[] columns)
	{
		Command.Select(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient Select(params ODataExpression[] columns)
	{
		Command.Select(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient Select(Expression<Func<T, object>> expression)
	{
		Command.Select(expression.ExtractColumnNames(TypeCache));
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderBy(IEnumerable<ODataOrderByColumn> columns)
	{
		Command.OrderBy(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderBy(params string[] columns)
	{
		Command.OrderBy(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderBy(params ODataExpression[] columns)
	{
		Command.OrderBy(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderBy(Expression<Func<T, object>> expression)
	{
		Command.OrderBy([.. expression.ExtractColumnNames(TypeCache)]);
		return Self;
	}

	/// <inheritdoc />
	public TClient ThenBy(params ODataExpression[] columns)
	{
		Command.ThenBy(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient ThenBy(Expression<Func<T, object>> expression)
	{
		Command.ThenBy([.. expression.ExtractColumnNames(TypeCache)]);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderByDescending(params string[] columns)
	{
		Command.OrderByDescending(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderByDescending(params ODataExpression[] columns)
	{
		Command.OrderByDescending(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient OrderByDescending(Expression<Func<T, object>> expression)
	{
		Command.OrderByDescending([.. expression.ExtractColumnNames(TypeCache)]);
		return Self;
	}

	/// <inheritdoc />
	public TClient ThenByDescending(params ODataExpression[] columns)
	{
		Command.ThenByDescending(columns);
		return Self;
	}

	/// <inheritdoc />
	public TClient ThenByDescending(Expression<Func<T, object>> expression)
	{
		Command.ThenByDescending([.. expression.ExtractColumnNames(TypeCache)]);
		return Self;
	}

	/// <inheritdoc />
	public TClient Compute(string compute)
	{
		Command.Compute(compute);
		return Self;
	}

	/// <inheritdoc />
	public TClient Compute<TR>(Expression<Func<T, TR>> expression)
	{
		Command.Compute(ComputeExpression.Extract(expression));
		return Self;
	}

	/// <inheritdoc />
	public TClient Apply(string apply)
	{
		Command.Apply(apply);
		return Self;
	}

	/// <inheritdoc />
	public TClient Apply(DynamicDataAggregation aggregation)
	{
		ArgumentNullException.ThrowIfNull(aggregation);
		Command.Apply(aggregation.CreateBuilder());
		return Self;
	}

	/// <inheritdoc />
	public IBoundClient<TR> Apply<TR>(Func<IDataAggregation<T>, IDataAggregation<TR>> aggregation)
		where TR : class
	{
		ArgumentNullException.ThrowIfNull(aggregation);
		var builder = new DataAggregationBuilder<T>();
		_ = aggregation(builder);
		Command.Apply(builder);
		return new BoundClient<TR>(Client, _parentCommand, Command, DynamicResults);
	}

	/// <inheritdoc />
	public TClient QueryOptions(string queryOptions)
	{
		Command.QueryOptions(queryOptions);
		return Self;
	}

	/// <inheritdoc />
	public TClient QueryOptions(IDictionary<string, object?> queryOptions)
	{
		Command.QueryOptions(queryOptions);
		return Self;
	}

	/// <inheritdoc />
	public TClient QueryOptions(ODataExpression expression)
	{
		Command.QueryOptions(expression);
		return Self;
	}

	/// <inheritdoc />
	public TClient QueryOptions<TResult>(Expression<Func<TResult, bool>> expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		Command.QueryOptions(ODataExpression.FromLinqExpression(expression.Body));
		return Self;
	}

	/// <inheritdoc />
	public TClient WithHeader(string name, string value)
	{
		Command.WithHeader(name, value);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
	{
		Command.WithHeaders(headers);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithIfMatch(string etag)
	{
		Command.WithIfMatch(etag);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithIfNoneMatch(string etag)
	{
		Command.WithIfNoneMatch(etag);
		return Self;
	}

	/// <inheritdoc />
	public TClient WithMaxPageSize(int maxPageSize)
	{
		Command.WithMaxPageSize(maxPageSize);
		return Self;
	}

	/// <inheritdoc />
	public TClient TrackChanges()
	{
		Command.TrackChanges();
		return Self;
	}

	/// <inheritdoc />
	public IMediaClient Media()
	{
		Command.Media();
		return new MediaClient(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IMediaClient Media(string streamName)
	{
		Command.Media(streamName);
		return new MediaClient(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IMediaClient Media(ODataExpression expression)
	{
		Command.Media(expression);
		return new MediaClient(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public IMediaClient Media(Expression<Func<T, object>> expression)
	{
		Command.Media(expression.ExtractColumnName(TypeCache));
		return new MediaClient(Client, Command, DynamicResults);
	}

	/// <inheritdoc />
	public TClient Count()
	{
		Command.Count();
		return Self;
	}

	/// <inheritdoc />
	public TClient WithCount()
	{
		Command.WithCount();
		return Self;
	}

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(string? linkName = null)
		where TResult : class => Link<TResult>(Command, linkName);

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, TResult>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, IEnumerable<TResult>>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, IList<TResult>>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, ISet<TResult>>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, HashSet<TResult>>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, TResult[]>> expression)
		where TResult : class => Link<TResult>(Command, expression.ExtractColumnName(TypeCache));

	/// <inheritdoc />
	public IBoundClient<IDictionary<string, object?>> NavigateTo(string linkName) => Link<IDictionary<string, object?>>(Command, linkName);

	/// <inheritdoc />
	public IBoundClient<T> NavigateTo(ODataExpression expression) => Link<T>(Command, ReferenceOf(expression));

	// ---- execution ---------------------------------------------------------------------------------------------

	/// <inheritdoc />
	public Task ExecuteAsync(CancellationToken cancellationToken = default) => Client.ExecuteAsync(Command, cancellationToken);

	/// <inheritdoc />
	public async Task<T?> ExecuteAsSingleAsync(CancellationToken cancellationToken = default) =>
		ConvertResult(await Client.ExecuteAsSingleAsync(Command, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public async Task<TResult?> ExecuteAsSingleAsync<TResult>(CancellationToken cancellationToken = default)
	{
		var results = await ExecuteAsArrayAsync<TResult>(cancellationToken).ConfigureAwait(false);
		return results.Length == 0 ? default : results.Single();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<T>> ExecuteAsEnumerableAsync(CancellationToken cancellationToken = default) =>
		ConvertResults(await Client.ExecuteAsEnumerableAsync(Command, null, cancellationToken).ConfigureAwait(false));

	/// <inheritdoc />
	public Task<TResult?> ExecuteAsScalarAsync<TResult>(CancellationToken cancellationToken = default) => Client.ExecuteAsScalarAsync<TResult>(Command, cancellationToken);

	/// <inheritdoc />
	public Task<TResult[]> ExecuteAsArrayAsync<TResult>(CancellationToken cancellationToken = default) => Client.ExecuteAsArrayAsync<TResult>(Command, null, cancellationToken);

	/// <inheritdoc />
	public Task<TResult[]> ExecuteAsArrayAsync<TResult>(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default) =>
		Client.ExecuteAsArrayAsync<TResult>(Command, annotations, cancellationToken);

	/// <inheritdoc />
	public Task<T[]> ExecuteAsArrayAsync(CancellationToken cancellationToken = default) => ExecuteAsArrayAsync<T>(cancellationToken);

	/// <inheritdoc />
	public Task<T[]> ExecuteAsArrayAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default) => ExecuteAsArrayAsync<T>(annotations, cancellationToken);

	/// <inheritdoc />
	public async Task<string> GetCommandTextAsync(CancellationToken cancellationToken = default)
	{
		await Session.InitializeAsync(cancellationToken).ConfigureAwait(false);
		return Command.Resolve(Session).Format();
	}

	// ---- helpers -----------------------------------------------------------------------------------------------

	private protected BoundClient<TResult> Link<TResult>(FluentCommand command, string? linkName)
		where TResult : class
	{
		linkName ??= typeof(TResult).Name;
		var linkCommand = command;
		BoundClient<TResult>? linkedClient = null;
		foreach (var link in linkName.Split('/'))
		{
			linkedClient = new BoundClient<TResult>(Client, linkCommand, null, DynamicResults);
			linkedClient.Command.Link(link);
			linkCommand = linkedClient.Command;
		}

		return linkedClient!;
	}

	private protected static string ReferenceOf(ODataExpression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		return expression.Reference ?? throw new ArgumentException("The expression must reference a property.", nameof(expression));
	}

	/// <summary>Materializes result dictionaries as <typeparamref name="T"/>, keeping only selected columns.</summary>
	private protected IReadOnlyList<T> ConvertResults(IReadOnlyList<IDictionary<string, object?>> entries)
	{
		var selected = Command.SelectedColumns;
		var containerName = Command.DynamicPropertiesContainerName;
		var results = new List<T>(entries.Count);
		foreach (var entry in entries)
		{
			results.Add(ConvertResult(FilterColumns(entry, selected), containerName));
		}

		return results;
	}

	/// <summary>Materializes one result dictionary as <typeparamref name="T"/>, keeping only selected columns.</summary>
	[return: NotNullIfNotNull(nameof(entry))]
	private protected T? ConvertResult(IDictionary<string, object?>? entry) =>
		entry is null ? null : ConvertResult(FilterColumns(entry, Command.SelectedColumns), Command.DynamicPropertiesContainerName);

	private T ConvertResult(IDictionary<string, object?> result, string? dynamicPropertiesContainerName)
	{
		if (!string.IsNullOrEmpty(dynamicPropertiesContainerName) && IsMaterializedType(typeof(T)))
		{
			TypeCache.Register<T>(dynamicPropertiesContainerName);
		}

		if (result.Count == 1 && result.TryGetValue(ReservedNames.Result, out var value) &&
			(TypeCache.IsValue(typeof(T)) || typeof(T) == typeof(string) || typeof(T) == typeof(object)))
		{
			return TypeCache.Convert<T>(value)!;
		}

		return TypeCache.ToObject<T>(result, DynamicResults)!;
	}

	private static bool IsMaterializedType(Type type) => type != typeof(object) && !type.IsInterface && type != typeof(ODataEntry);

	private IDictionary<string, object?> FilterColumns(IDictionary<string, object?> entry, IReadOnlyList<string> selectedColumns)
	{
		if (selectedColumns.Count == 0)
		{
			return entry;
		}

		var filtered = new Dictionary<string, object?>(StringComparer.Ordinal);
		foreach (var (key, value) in entry)
		{
			if (key == ReservedNames.Annotations || selectedColumns.Any(column => IsSelectedColumn(key, value, column)))
			{
				filtered[key] = value;
			}
		}

		return filtered;
	}

	private bool IsSelectedColumn(string key, object? value, string columnName)
	{
		var resolver = Session.Settings.NameMatchResolver;
		var separator = columnName.IndexOf('/');
		if (separator < 0)
		{
			return resolver.IsMatch(key, columnName);
		}

		if (!resolver.IsMatch(key, columnName[..separator]))
		{
			return false;
		}

		var rest = columnName[(separator + 1)..];
		return value switch
		{
			IDictionary<string, object?> nested => nested.Any(x => IsSelectedColumn(x.Key, x.Value, rest)),
			IEnumerable<object?> items => items.OfType<IDictionary<string, object?>>().Any(nested => nested.Any(x => IsSelectedColumn(x.Key, x.Value, rest))),
			_ => false,
		};
	}
}
