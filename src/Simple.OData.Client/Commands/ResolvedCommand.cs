using Simple.OData.Client.Expressions;

namespace Simple.OData.Client.Commands;

/// <summary>
/// A <see cref="FluentCommand"/> whose names have been resolved against metadata and whose key values or
/// key-like filters have been interpreted.
/// </summary>
public sealed class ResolvedCommand
{
	private readonly Session _session;
	private IReadOnlyDictionary<string, object?>? _keyValues;

	internal ResolvedCommand(FluentCommand command, Session session)
	{
		ArgumentNullException.ThrowIfNull(command);
		_session = session ?? throw new ArgumentNullException(nameof(session));
		Details = new CommandDetails(command.Details);

		ResolveCollectionName();
		ResolveDerivedCollectionName();
		ResolveLinkName();
		ResolveEntityCollection();
		ResolveKeys();
		ResolveFilter();
		ResolveEntryData();
	}

	internal CommandDetails Details { get; }

	internal Session Session => _session;

	/// <summary>The resolved entity collection, or <see langword="null"/> for unbound operations.</summary>
	public EntityCollection? EntityCollection { get; private set; }

	/// <summary>The collection path including a qualified derived type segment when present.</summary>
	public string QualifiedEntityCollectionName
	{
		get
		{
			var collection = EntityCollection ?? throw new InvalidOperationException("The command does not target an entity collection.");
			return collection.BaseEntityCollection is null
				? collection.Name
				: $"{collection.BaseEntityCollection.Name}/{_session.Metadata.GetQualifiedTypeName(collection.Name)}";
		}
	}

	/// <summary>Whether the command addresses a singleton, which is updated and navigated without a key.</summary>
	public bool IsSingleton => EntityCollection is { } collection && _session.Metadata.IsSingleton((collection.BaseEntityCollection ?? collection).Name);

	/// <summary>The property that holds dynamic (open type) properties.</summary>
	public string? DynamicPropertiesContainerName => Details.DynamicPropertiesContainerName;

	/// <summary>The key values by declared key property name, or <see langword="null"/> when the command has no key.</summary>
	public IReadOnlyDictionary<string, object?>? KeyValues => _keyValues ??= BuildKeyValues();

	/// <summary>The key values recognised from the filter expression or named key, or <see langword="null"/>.</summary>
	public IReadOnlyDictionary<string, object?>? FilterAsKey => Details.NamedKeyValues;

	/// <summary>The request payload with the dynamic properties container flattened into it.</summary>
	public IDictionary<string, object?> CommandData
	{
		get
		{
			if (Details.EntryData is null)
			{
				return new Dictionary<string, object?>();
			}

			var containerName = Details.DynamicPropertiesContainerName;
			if (string.IsNullOrEmpty(containerName))
			{
				return Details.EntryData;
			}

			var entryData = new Dictionary<string, object?>(Details.EntryData.Count);
			foreach (var (name, value) in Details.EntryData)
			{
				if (string.Equals(name, containerName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				entryData[name] = value;
			}

			if (Details.EntryData.TryGetValue(containerName, out var dynamicProperties) && dynamicProperties is not null)
			{
				if (dynamicProperties is not IDictionary<string, object?> container)
				{
					throw new InvalidOperationException($"Property {containerName} must implement IDictionary<string, object?>.");
				}

				foreach (var (name, value) in container)
				{
					entryData[name] = value;
				}
			}

			return entryData;
		}
	}

	/// <summary>Requests the total count alongside the results.</summary>
	public ResolvedCommand WithCount()
	{
		Details.IncludeCount = true;
		return this;
	}

	/// <summary>Targets every member of the addressed collection (<c>/$each</c>, OData 4.01).</summary>
	internal ResolvedCommand Each()
	{
		Details.UseEachSegment = true;
		return this;
	}

	/// <summary>Formats the command as a relative request URL.</summary>
	public string Format() => new CommandUriBuilder(_session).Format(this);

	/// <inheritdoc />
	public override string ToString() => Format();

	private void ResolveCollectionName()
	{
		if (Details.CollectionName is null && Details.CollectionExpression is not null)
		{
			var collectionName = Details.CollectionExpression.AsString(_session);
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
		}
	}

	private void ResolveDerivedCollectionName()
	{
		if (Details.DerivedCollectionName is null && Details.DerivedCollectionExpression is not null)
		{
			Details.DerivedCollectionName = Details.DerivedCollectionExpression.AsString(_session);
		}
	}

	private void ResolveLinkName()
	{
		if (Details.LinkName is null && Details.LinkExpression is not null)
		{
			Details.LinkName = Details.LinkExpression.AsString(_session);
		}
	}

	private void ResolveEntityCollection()
	{
		EntityCollection? collection = null;
		if (!string.IsNullOrEmpty(Details.LinkName))
		{
			var parentDetails = Details.Parent ?? throw new InvalidOperationException("A navigation link requires a parent command.");
			var parent = new FluentCommand(parentDetails).Resolve(_session);
			var parentCollection = parent.EntityCollection ?? throw new InvalidOperationException("The parent command does not target an entity collection.");
			var typeName = _session.Metadata.GetNavigationPropertyPartnerTypeName(parentCollection.Name, Details.LinkName);
			collection = _session.Metadata.GetEntityCollection(typeName);
		}
		else if (!string.IsNullOrEmpty(Details.CollectionName))
		{
			collection = _session.Metadata.GetEntityCollection(Details.CollectionName);
		}

		if (collection is not null && !string.IsNullOrEmpty(Details.DerivedCollectionName))
		{
			collection = _session.Metadata.GetDerivedEntityCollection(collection, Details.DerivedCollectionName);
		}

		EntityCollection = collection;
	}

	private void ResolveKeys()
	{
		var namedKeyValues = Details.NamedKeyValues as IDictionary<string, object?>;
		if (namedKeyValues is null && Details.KeyValues is [{ } single] && !IsKeyLiteral(single))
		{
			namedKeyValues = _session.TypeCache.ToDictionary(single);
		}

		if (namedKeyValues is null || EntityCollection is null)
		{
			return;
		}

		if (NamedKeyValuesMatchAnyKey(namedKeyValues, out var matchingKey, out var isAlternateKey))
		{
			Details.NamedKeyValues = ToDictionary(matchingKey);
			Details.IsAlternateKey = isAlternateKey;
		}
		else if (Utils.NamedKeyValuesContainKeyNames(namedKeyValues, _session.Settings.NameMatchResolver, DeclaredKeyNames(), out var containedKey))
		{
			Details.NamedKeyValues = ToDictionary(containedKey);
			Details.IsAlternateKey = false;
		}
		else
		{
			throw new InvalidOperationException(
				$"The values [{string.Join(", ", namedKeyValues.Keys)}] do not match the key or any alternate key of '{EntityCollection.Name}'.");
		}

		Details.KeyValues = null;
	}

	private void ResolveFilter()
	{
		var expression = Details.FilterExpression;
		if (expression is null)
		{
			return;
		}

		var isAlternateKey = false;
		var keyFromFilter = Details.Filter is null && !Details.HasKey && !IsCollectionQuery() ? TryInterpretFilterExpressionAsKey(expression, out isAlternateKey) : null;
		if (keyFromFilter is not null)
		{
			Details.NamedKeyValues = keyFromFilter;
			Details.IsAlternateKey = isAlternateKey;
			Details.KeyValues = null;
			Details.TopCount = -1;
		}
		else
		{
			var entityCollection = EntityCollection;
			if (Details.HasFunction)
			{
				entityCollection = _session.Metadata.GetFunctionReturnCollection(Details.FunctionName!) ?? entityCollection;
			}

			var text = expression.Format(new ExpressionContext(_session, entityCollection, null, DynamicPropertiesContainerName));
			Details.Filter = string.IsNullOrEmpty(Details.Filter) ? text : $"({Details.Filter}) and ({text})";
		}

		if (expression.HasTypeConstraint(Details.DerivedCollectionName))
		{
			Details.DerivedCollectionName = null;
		}
	}

	private void ResolveEntryData()
	{
		if (Details.EntryValue is { } value)
		{
			Details.EntryData = _session.TypeCache.ToDictionary(value);
			Details.BatchEntries?.GetOrAdd(value, Details.EntryData);
		}
	}

	private Dictionary<string, object?>? BuildKeyValues()
	{
		if (!Details.HasKey)
		{
			return null;
		}

		IEnumerable<KeyValuePair<string, object?>> pairs;
		if (Details.KeyValues is { } positional)
		{
			var collection = EntityCollection ?? throw new InvalidOperationException("Key values require an entity collection.");
			var keyNames = _session.Metadata.GetDeclaredKeyPropertyNames(collection.Name);
			if (keyNames.Count != positional.Count)
			{
				throw new InvalidOperationException(
					$"Entity set '{collection.Name}' has {keyNames.Count} key properties but {positional.Count} key value(s) were supplied.");
			}

			pairs = keyNames.Zip(positional, static (name, value) => new KeyValuePair<string, object?>(name, value));
		}
		else
		{
			pairs = Details.NamedKeyValues!;
		}

		var result = new Dictionary<string, object?>();
		foreach (var (name, value) in pairs)
		{
			result[name] = value is ODataExpression expression ? expression.Value : value;
		}

		return result;
	}

	/// <summary>
	/// Whether the command addresses the collection as a whole (ordering, paging, counting, searching, aggregating),
	/// in which case a key-like filter must stay a <c>$filter</c> instead of collapsing into a key segment.
	/// </summary>
	private bool IsCollectionQuery() =>
		Details.ComputeCount || Details.IncludeCount || Details.OrderByColumns.Count > 0 || Details.SkipCount >= 0 || Details.TopCount > 1
		|| Details.HasSearch || Details.ApplyClauses.Count > 0 || Details.HasCompute || Details.Index is not null;

	private Dictionary<string, object?>? TryInterpretFilterExpressionAsKey(ODataExpression expression, out bool isAlternateKey)
	{
		isAlternateKey = false;
		if (EntityCollection is null)
		{
			return null;
		}

		var lookupColumns = new Dictionary<string, object?>();
		if (!expression.ExtractLookupColumns(lookupColumns))
		{
			return null;
		}

		return NamedKeyValuesMatchAnyKey(lookupColumns, out var matchingKey, out isAlternateKey) ? ToDictionary(matchingKey) : null;
	}

	private bool NamedKeyValuesMatchAnyKey(IDictionary<string, object?> namedKeyValues, out List<KeyValuePair<string, object?>> matchingKey, out bool isAlternateKey)
	{
		isAlternateKey = false;
		var resolver = _session.Settings.NameMatchResolver;
		if (Utils.NamedKeyValuesMatchKeyNames(namedKeyValues, resolver, DeclaredKeyNames(), out var primary))
		{
			matchingKey = primary!;
			return true;
		}

		foreach (var alternateKey in _session.Metadata.GetAlternateKeyPropertyNames(EntityCollection!.Name))
		{
			if (Utils.NamedKeyValuesMatchKeyNames(namedKeyValues, resolver, alternateKey, out var alternate))
			{
				matchingKey = alternate!;
				isAlternateKey = true;
				return true;
			}
		}

		matchingKey = [];
		return false;
	}

	private IReadOnlyList<string> DeclaredKeyNames() => _session.Metadata.GetDeclaredKeyPropertyNames(EntityCollection!.Name);

	private static Dictionary<string, object?> ToDictionary(List<KeyValuePair<string, object?>>? pairs)
	{
		var result = new Dictionary<string, object?>();
		if (pairs is not null)
		{
			foreach (var (name, value) in pairs)
			{
				result[name] = value;
			}
		}

		return result;
	}

	/// <summary>A single positional key value is a literal; anything else is an object carrying key properties.</summary>
	private static bool IsKeyLiteral(object value) =>
		value is string or byte[] or ODataExpression || value.GetType().IsValueType;
}
