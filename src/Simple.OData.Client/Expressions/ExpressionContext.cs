namespace Simple.OData.Client.Expressions;

/// <summary>
/// State for formatting one expression tree: the session, the entity collection the expression is evaluated against,
/// the lambda variable in scope and a counter shared across the whole tree so lambda variables get unique names.
/// </summary>
internal sealed class ExpressionContext
{
	private readonly LambdaCounter _counter;

	public ExpressionContext(ISession session, EntityCollection? entityCollection = null, string? scopeQualifier = null, string? dynamicPropertiesContainerName = null, bool isQueryOption = false)
		: this(session, entityCollection, scopeQualifier, dynamicPropertiesContainerName, isQueryOption, new LambdaCounter())
	{
	}

	private ExpressionContext(ISession session, EntityCollection? entityCollection, string? scopeQualifier, string? dynamicPropertiesContainerName, bool isQueryOption, LambdaCounter counter)
	{
		ArgumentNullException.ThrowIfNull(session);
		Session = session;
		EntityCollection = entityCollection;
		ScopeQualifier = scopeQualifier;
		DynamicPropertiesContainerName = dynamicPropertiesContainerName;
		IsQueryOption = isQueryOption;
		_counter = counter;
	}

	public ISession Session { get; }

	public EntityCollection? EntityCollection { get; }

	public string? ScopeQualifier { get; }

	public string? DynamicPropertiesContainerName { get; }

	public bool IsQueryOption { get; }

	public IMetadata Metadata => Session.Metadata;

	/// <summary>A context for a nested lambda body, sharing this context's variable counter.</summary>
	public ExpressionContext Nested(EntityCollection? entityCollection, string scopeQualifier) =>
		new(Session, entityCollection, scopeQualifier, DynamicPropertiesContainerName, false, _counter);

	/// <summary>A context without entity collection or scope (for type names and function arguments evaluated on their own).</summary>
	public ExpressionContext Detached() => new(Session, null, null, DynamicPropertiesContainerName, false, _counter);

	/// <summary>Returns the next lambda variable name (<c>x1</c>, <c>x2</c>, …).</summary>
	public string NextLambdaVariable() => "x" + _counter.Next().ToString(System.Globalization.CultureInfo.InvariantCulture);

	private sealed class LambdaCounter
	{
		private int _value;

		public int Next() => ++_value;
	}
}
