using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Simple.OData.Client;

/// <summary>
/// The query-building and execution members shared by bound and unbound fluent clients.
/// <typeparamref name="T"/> is the result type and <typeparamref name="TClient"/> the concrete client type returned by every builder member.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Established fluent API names.")]
public interface IFluentClient<T, TClient>
	where T : class
{
	// ---- request shape -----------------------------------------------------------------------------------------

	/// <summary>Names the property that receives dynamic (open type) properties when materializing results.</summary>
	TClient WithProperties(Expression<Func<T, IDictionary<string, object?>>> expression);

	/// <summary>Reads the named stream properties into the results.</summary>
	TClient WithMedia(IEnumerable<string> properties);

	/// <summary>Reads the named stream properties into the results.</summary>
	TClient WithMedia(params string[] properties);

	/// <summary>Reads the stream properties named by dynamic expressions into the results.</summary>
	TClient WithMedia(params ODataExpression[] properties);

	/// <summary>Reads the stream properties selected by a typed expression into the results.</summary>
	TClient WithMedia(Expression<Func<T, object>> properties);

	/// <summary>Addresses one entry by positional key value(s).</summary>
	TClient Key(params object?[] entryKey);

	/// <summary>Addresses one entry by positional key value(s).</summary>
	TClient Key(IEnumerable<object?> entryKey);

	/// <summary>Addresses one entry by named key values; alternate keys are recognised.</summary>
	TClient Key(IDictionary<string, object?> entryKey);

	/// <summary>Addresses one entry by the key properties of <paramref name="entryKey"/>.</summary>
	TClient Key(T entryKey);

	/// <summary>Adds a raw <c>$filter</c> expression.</summary>
	TClient Filter(string filter);

	/// <summary>Adds a dynamic <c>$filter</c> expression.</summary>
	TClient Filter(ODataExpression expression);

	/// <summary>Adds a typed <c>$filter</c> expression.</summary>
	TClient Filter(Expression<Func<T, bool>> expression);

	/// <summary>Adds a <c>$search</c> expression.</summary>
	TClient Search(string search);

	/// <summary>Calls a function bound to the current resource.</summary>
	TClient Function(string functionName);

	/// <summary>Calls a function bound to the current resource, with results of type <typeparamref name="TResult"/>.</summary>
	IBoundClient<TResult> Function<TResult>(string functionName) where TResult : class;

	/// <summary>Invokes an action bound to the current resource.</summary>
	TClient Action(string actionName);

	/// <summary>Adds <c>$skip</c>.</summary>
	TClient Skip(long count);

	/// <summary>Adds <c>$top</c>.</summary>
	TClient Top(long count);

	/// <summary>Adds <c>$index</c> (OData 4.01).</summary>
	TClient Index(long index);

	/// <summary>Adds <c>$expand</c> for every navigation property with the given options.</summary>
	TClient Expand(ODataExpandOptions expandOptions);

	/// <summary>Adds <c>$expand</c> paths; a path may carry inline options in parentheses.</summary>
	TClient Expand(IEnumerable<string> associations);

	/// <summary>Adds <c>$expand</c> paths with options.</summary>
	TClient Expand(ODataExpandOptions expandOptions, IEnumerable<string> associations);

	/// <summary>Adds <c>$expand</c> paths; a path may carry inline options in parentheses.</summary>
	TClient Expand(params string[] associations);

	/// <summary>Adds <c>$expand</c> paths with options.</summary>
	TClient Expand(ODataExpandOptions expandOptions, params string[] associations);

	/// <summary>Adds <c>$expand</c> for dynamic navigation expressions.</summary>
	TClient Expand(params ODataExpression[] associations);

	/// <summary>Adds <c>$expand</c> for dynamic navigation expressions with options.</summary>
	TClient Expand(ODataExpandOptions expandOptions, params ODataExpression[] associations);

	/// <summary>Adds <c>$expand</c> from a typed expression; nested <c>Select</c>/<c>Where</c>/<c>OrderBy</c>/<c>Take</c>/<c>Skip</c> become nested options.</summary>
	TClient Expand(Expression<Func<T, object>> expression);

	/// <summary>Adds <c>$expand</c> from a typed expression with options.</summary>
	TClient Expand(ODataExpandOptions expandOptions, Expression<Func<T, object>> expression);

	/// <summary>Adds <c>$select</c> columns.</summary>
	TClient Select(IEnumerable<string> columns);

	/// <summary>Adds <c>$select</c> columns.</summary>
	TClient Select(params string[] columns);

	/// <summary>Adds <c>$select</c> columns from dynamic expressions.</summary>
	TClient Select(params ODataExpression[] columns);

	/// <summary>Adds <c>$select</c> columns from a typed expression.</summary>
	TClient Select(Expression<Func<T, object>> expression);

	/// <summary>Adds <c>$orderby</c> columns.</summary>
	TClient OrderBy(IEnumerable<ODataOrderByColumn> columns);

	/// <summary>Adds ascending <c>$orderby</c> columns.</summary>
	TClient OrderBy(params string[] columns);

	/// <summary>Adds ascending <c>$orderby</c> columns from dynamic expressions.</summary>
	TClient OrderBy(params ODataExpression[] columns);

	/// <summary>Adds ascending <c>$orderby</c> columns from a typed expression.</summary>
	TClient OrderBy(Expression<Func<T, object>> expression);

	/// <summary>Adds further ascending <c>$orderby</c> columns from dynamic expressions.</summary>
	TClient ThenBy(params ODataExpression[] columns);

	/// <summary>Adds further ascending <c>$orderby</c> columns from a typed expression.</summary>
	TClient ThenBy(Expression<Func<T, object>> expression);

	/// <summary>Adds descending <c>$orderby</c> columns.</summary>
	TClient OrderByDescending(params string[] columns);

	/// <summary>Adds descending <c>$orderby</c> columns from dynamic expressions.</summary>
	TClient OrderByDescending(params ODataExpression[] columns);

	/// <summary>Adds descending <c>$orderby</c> columns from a typed expression.</summary>
	TClient OrderByDescending(Expression<Func<T, object>> expression);

	/// <summary>Adds further descending <c>$orderby</c> columns from dynamic expressions.</summary>
	TClient ThenByDescending(params ODataExpression[] columns);

	/// <summary>Adds further descending <c>$orderby</c> columns from a typed expression.</summary>
	TClient ThenByDescending(Expression<Func<T, object>> expression);

	/// <summary>Adds a raw <c>$compute</c> clause (<c>expression as Alias,…</c>).</summary>
	TClient Compute(string compute);

	/// <summary>Adds <c>$compute</c> from a typed projection (<c>x =&gt; new { Alias = x.Price * 2 }</c>).</summary>
	TClient Compute<TR>(Expression<Func<T, TR>> expression);

	/// <summary>Adds a raw <c>$apply</c> clause.</summary>
	TClient Apply(string apply);

	/// <summary>Adds <c>$apply</c> from a dynamic aggregation builder.</summary>
	TClient Apply(DynamicDataAggregation aggregation);

	/// <summary>Adds <c>$apply</c> from a typed aggregation pipeline; results are of the pipeline's output type.</summary>
	IBoundClient<TR> Apply<TR>(Func<IDataAggregation<T>, IDataAggregation<TR>> aggregation) where TR : class;

	/// <summary>Adds raw query options (<c>name=value&amp;…</c>).</summary>
	TClient QueryOptions(string queryOptions);

	/// <summary>Adds custom query options.</summary>
	TClient QueryOptions(IDictionary<string, object?> queryOptions);

	/// <summary>Adds custom query options from a dynamic expression (<c>x.Option == value &amp;&amp; …</c>).</summary>
	TClient QueryOptions(ODataExpression expression);

	/// <summary>Adds custom query options from a typed expression.</summary>
	TClient QueryOptions<TResult>(Expression<Func<TResult, bool>> expression);

	/// <summary>Adds a request header.</summary>
	TClient WithHeader(string name, string value);

	/// <summary>Adds request headers.</summary>
	TClient WithHeaders(IEnumerable<KeyValuePair<string, string>> headers);

	/// <summary>Sends <c>If-Match</c> with the given ETag.</summary>
	TClient WithIfMatch(string etag);

	/// <summary>Sends <c>If-None-Match</c> with the given ETag.</summary>
	TClient WithIfNoneMatch(string etag);

	/// <summary>Asks the service for pages of at most <paramref name="maxPageSize"/> entries (<c>Prefer: odata.maxpagesize</c>).</summary>
	TClient WithMaxPageSize(int maxPageSize);

	/// <summary>Asks the service for a delta link (<c>Prefer: odata.track-changes</c>).</summary>
	TClient TrackChanges();

	/// <summary>Addresses the media resource of the entry (<c>$value</c>).</summary>
	IMediaClient Media();

	/// <summary>Addresses a stream property of the entry.</summary>
	IMediaClient Media(string streamName);

	/// <summary>Addresses a stream property named by a dynamic expression.</summary>
	IMediaClient Media(ODataExpression expression);

	/// <summary>Addresses a stream property selected by a typed expression.</summary>
	IMediaClient Media(Expression<Func<T, object>> expression);

	/// <summary>Addresses the count of the collection (<c>/$count</c>).</summary>
	TClient Count();

	/// <summary>Asks for the total count alongside the results (<c>$count=true</c>).</summary>
	TClient WithCount();

	/// <summary>Navigates to a related collection or entry, named by <paramref name="linkName"/> or the type name of <typeparamref name="TResult"/>.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(string? linkName = null) where TResult : class;

	/// <summary>Navigates to a related entry.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, TResult>> expression) where TResult : class;

	/// <summary>Navigates to a related collection.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, IEnumerable<TResult>>> expression) where TResult : class;

	/// <summary>Navigates to a related collection.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, IList<TResult>>> expression) where TResult : class;

	/// <summary>Navigates to a related collection.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, ISet<TResult>>> expression) where TResult : class;

	/// <summary>Navigates to a related collection.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, HashSet<TResult>>> expression) where TResult : class;

	/// <summary>Navigates to a related collection.</summary>
	IBoundClient<TResult> NavigateTo<TResult>(Expression<Func<T, TResult[]>> expression) where TResult : class;

	/// <summary>Navigates to a related collection or entry with dictionary results.</summary>
	IBoundClient<IDictionary<string, object?>> NavigateTo(string linkName);

	/// <summary>Navigates to a related collection or entry named by a dynamic expression.</summary>
	IBoundClient<T> NavigateTo(ODataExpression expression);

	// ---- execution ---------------------------------------------------------------------------------------------

	/// <summary>Executes the function or action without reading a result.</summary>
	Task ExecuteAsync(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a single result.</summary>
	Task<T?> ExecuteAsSingleAsync(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a single result of type <typeparamref name="TResult"/>.</summary>
	Task<TResult?> ExecuteAsSingleAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a collection of results.</summary>
	Task<IReadOnlyList<T>> ExecuteAsEnumerableAsync(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a primitive result.</summary>
	Task<TResult?> ExecuteAsScalarAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a collection of primitive results.</summary>
	Task<TResult[]> ExecuteAsArrayAsync<TResult>(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads a collection of primitive results, capturing feed annotations.</summary>
	Task<TResult[]> ExecuteAsArrayAsync<TResult>(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads an array of results.</summary>
	Task<T[]> ExecuteAsArrayAsync(CancellationToken cancellationToken = default);

	/// <summary>Executes the function or action and reads an array of results, capturing feed annotations.</summary>
	Task<T[]> ExecuteAsArrayAsync(ODataFeedAnnotations annotations, CancellationToken cancellationToken = default);

	/// <summary>Formats the request URL relative to the service root, initializing metadata when needed.</summary>
	Task<string> GetCommandTextAsync(CancellationToken cancellationToken = default);
}
