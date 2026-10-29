using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace Simple.OData.Client.Metadata;

/// <summary>
/// Precomputed, name-tolerant lookups over an <see cref="IEdmModel"/>. Built once per model and resolver.
/// </summary>
internal sealed class EdmMetadataIndex
{
	private readonly IEdmModel _model;
	private readonly INameMatchResolver _resolver;
	private readonly NameIndex<IEdmEntitySet> _entitySets;
	private readonly NameIndex<IEdmSingleton> _singletons;
	private readonly NameIndex<IEdmEntityType> _entityTypes;
	private readonly NameIndex<IEdmComplexType> _complexTypes;
	private readonly NameIndex<IEdmEnumType> _enumTypes;
	private readonly NameIndex<IEdmFunctionImport> _functionImports;
	private readonly NameIndex<IEdmActionImport> _actionImports;
	private readonly NameIndex<IEdmFunction> _functions;
	private readonly NameIndex<IEdmAction> _actions;
	private readonly FrozenDictionary<string, IEdmSchemaType> _typesByFullName;
	private readonly ConcurrentDictionary<IEdmStructuredType, StructuredTypeIndex> _structuredTypes = new();
	private readonly ConcurrentDictionary<IEdmEntityType, IReadOnlyList<IEdmEntityType>> _derivedTypes = new();

	public EdmMetadataIndex(IEdmModel model, INameMatchResolver resolver)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(resolver);
		_model = model;
		_resolver = resolver;

		var containers = model.AllEntityContainers().ToArray();
		_entitySets = new NameIndex<IEdmEntitySet>(containers.SelectMany(x => x.EntitySets()), x => x.Name, resolver);
		_singletons = new NameIndex<IEdmSingleton>(containers.SelectMany(x => x.Singletons()), x => x.Name, resolver);
		_functionImports = new NameIndex<IEdmFunctionImport>(containers.SelectMany(x => x.OperationImports().OfType<IEdmFunctionImport>()), x => x.Name, resolver);
		_actionImports = new NameIndex<IEdmActionImport>(containers.SelectMany(x => x.OperationImports().OfType<IEdmActionImport>()), x => x.Name, resolver);

		var schemaElements = model.SchemaElements.ToArray();
		_entityTypes = new NameIndex<IEdmEntityType>(schemaElements.OfType<IEdmEntityType>(), x => x.Name, resolver);
		_complexTypes = new NameIndex<IEdmComplexType>(schemaElements.OfType<IEdmComplexType>(), x => x.Name, resolver);
		_enumTypes = new NameIndex<IEdmEnumType>(schemaElements.OfType<IEdmEnumType>(), x => x.Name, resolver);
		_functions = new NameIndex<IEdmFunction>(schemaElements.OfType<IEdmFunction>(), x => x.Name, resolver);
		_actions = new NameIndex<IEdmAction>(schemaElements.OfType<IEdmAction>(), x => x.Name, resolver);

		var byFullName = new Dictionary<string, IEdmSchemaType>(StringComparer.Ordinal);
		foreach (var schemaType in schemaElements.OfType<IEdmSchemaType>())
		{
			byFullName.TryAdd(schemaType.FullName(), schemaType);
		}

		_typesByFullName = byFullName.ToFrozenDictionary(StringComparer.Ordinal);
	}

	public IEdmModel Model => _model;

	public INameMatchResolver Resolver => _resolver;

	public bool TryFindEntitySet(string name, out IEdmEntitySet entitySet) => _entitySets.TryFind(name, out entitySet);

	public bool TryFindSingleton(string name, out IEdmSingleton singleton) => _singletons.TryFind(name, out singleton);

	public bool TryFindNavigationSource(string name, out IEdmNavigationSource navigationSource)
	{
		if (TryFindEntitySet(name, out var entitySet))
		{
			navigationSource = entitySet;
			return true;
		}

		if (TryFindSingleton(name, out var singleton))
		{
			navigationSource = singleton;
			return true;
		}

		navigationSource = null!;
		return false;
	}

	public bool TryFindEntityType(string name, out IEdmEntityType entityType)
	{
		if (_typesByFullName.TryGetValue(name, out var schemaType) && schemaType is IEdmEntityType byFullName)
		{
			entityType = byFullName;
			return true;
		}

		return _entityTypes.TryFind(name, out entityType);
	}

	public bool TryFindComplexType(string name, out IEdmComplexType complexType)
	{
		if (_typesByFullName.TryGetValue(name, out var schemaType) && schemaType is IEdmComplexType byFullName)
		{
			complexType = byFullName;
			return true;
		}

		return _complexTypes.TryFind(name, out complexType);
	}

	public bool TryFindEnumType(string name, out IEdmEnumType enumType)
	{
		if (_typesByFullName.TryGetValue(name, out var schemaType) && schemaType is IEdmEnumType byFullName)
		{
			enumType = byFullName;
			return true;
		}

		return _enumTypes.TryFind(name, out enumType);
	}

	public bool TryFindSchemaType(string name, out IEdmSchemaType schemaType)
	{
		if (_typesByFullName.TryGetValue(name, out schemaType!))
		{
			return true;
		}

		if (TryFindEntityType(name, out var entityType))
		{
			schemaType = entityType;
			return true;
		}

		if (TryFindComplexType(name, out var complexType))
		{
			schemaType = complexType;
			return true;
		}

		if (TryFindEnumType(name, out var enumType))
		{
			schemaType = enumType;
			return true;
		}

		schemaType = null!;
		return false;
	}

	public bool TryFindFunction(string name, out IEdmFunction function)
	{
		if (_functionImports.TryFind(name, out var import))
		{
			function = import.Function;
			return true;
		}

		return _functions.TryFind(name, out function);
	}

	public bool TryFindFunctionImport(string name, out IEdmFunctionImport import) => _functionImports.TryFind(name, out import);

	public bool TryFindAction(string name, out IEdmAction action)
	{
		if (_actionImports.TryFind(name, out var import))
		{
			action = import.Action;
			return true;
		}

		return _actions.TryFind(name, out action);
	}

	public bool TryFindActionImport(string name, out IEdmActionImport import) => _actionImports.TryFind(name, out import);

	public IEnumerable<IEdmAction> FindActions(string name) => _actions.FindAll(name);

	public IEnumerable<IEdmFunction> FindFunctions(string name) => _functions.FindAll(name);

	public bool TryFindStructuralProperty(IEdmStructuredType type, string name, out IEdmStructuralProperty property) =>
		StructuredType(type).StructuralProperties.TryFind(name, out property);

	public bool TryFindNavigationProperty(IEdmStructuredType type, string name, out IEdmNavigationProperty property) =>
		StructuredType(type).NavigationProperties.TryFind(name, out property);

	public IReadOnlyList<IEdmStructuralProperty> StructuralProperties(IEdmStructuredType type) => StructuredType(type).StructuralProperties.Items;

	public IReadOnlyList<IEdmNavigationProperty> NavigationProperties(IEdmStructuredType type) => StructuredType(type).NavigationProperties.Items;

	public IReadOnlyList<string> DeclaredKeyNames(IEdmEntityType entityType) => StructuredType(entityType).KeyNames;

	public IReadOnlyList<IReadOnlyList<string>> AlternateKeyNames(IEdmEntityType entityType) => StructuredType(entityType).AlternateKeyNames;

	public IReadOnlyList<IEdmEntityType> DerivedTypes(IEdmEntityType entityType) =>
		_derivedTypes.GetOrAdd(entityType, t => _model.FindAllDerivedTypes(t).OfType<IEdmEntityType>().ToArray());

	public bool RequiresOptimisticConcurrency(IEdmVocabularyAnnotatable annotatable) =>
		_model.FindVocabularyAnnotations(annotatable).Any(x => x.Term.Name == "OptimisticConcurrency");

	private StructuredTypeIndex StructuredType(IEdmStructuredType type) =>
		_structuredTypes.GetOrAdd(type, t => new StructuredTypeIndex(_model, t, _resolver));

	/// <summary>
	/// Reads the <c>AlternateKeys</c> annotations (community and core vocabularies). The property references are path
	/// expressions in CSDL XML, but some services (ASP.NET Core OData's CSDL JSON) write them as plain strings.
	/// </summary>
	private static IReadOnlyList<string>[] ReadAlternateKeys(IEdmModel model, IEdmEntityType entityType)
	{
		var keys = new List<IReadOnlyList<string>>();
		foreach (var annotation in model.FindVocabularyAnnotations(entityType).Where(x => x.Term.Name == "AlternateKeys"))
		{
			if (annotation.Value is not IEdmCollectionExpression alternateKeys)
			{
				continue;
			}

			foreach (var alternateKey in alternateKeys.Elements.OfType<IEdmRecordExpression>())
			{
				var names = alternateKey.Properties.FirstOrDefault(x => x.Name == "Key")?.Value is IEdmCollectionExpression refs
					? refs.Elements.OfType<IEdmRecordExpression>()
						.Select(x => PropertyRefName(x.Properties.FirstOrDefault(p => p.Name == "Name")?.Value))
						.Where(x => !string.IsNullOrEmpty(x))
						.Select(x => x!)
						.ToArray()
					: [];
				if (names.Length > 0)
				{
					keys.Add(names);
				}
			}
		}

		return [.. keys];
	}

	private static string? PropertyRefName(IEdmExpression? expression) => expression switch
	{
		IEdmPathExpression path => string.Join('/', path.PathSegments),
		IEdmStringConstantExpression text => text.Value,
		_ => null,
	};

	private sealed class StructuredTypeIndex
	{
		public StructuredTypeIndex(IEdmModel model, IEdmStructuredType type, INameMatchResolver resolver)
		{
			StructuralProperties = new NameIndex<IEdmStructuralProperty>(type.StructuralProperties(), x => x.Name, resolver);
			NavigationProperties = new NameIndex<IEdmNavigationProperty>(type.NavigationProperties(), x => x.Name, resolver);
			if (type is IEdmEntityType entityType)
			{
				KeyNames = entityType.KeyOwner().DeclaredKey?.Select(x => x.Name).ToArray() ?? [];
				AlternateKeyNames = ReadAlternateKeys(model, entityType);
			}
			else
			{
				KeyNames = [];
				AlternateKeyNames = [];
			}
		}

		public NameIndex<IEdmStructuralProperty> StructuralProperties { get; }

		public NameIndex<IEdmNavigationProperty> NavigationProperties { get; }

		public IReadOnlyList<string> KeyNames { get; }

		public IReadOnlyList<IReadOnlyList<string>> AlternateKeyNames { get; }
	}

	/// <summary>
	/// Exact-first lookup of named elements. Lenient resolvers consult precomputed homogenized and singular/plural
	/// variants; a custom resolver falls back to a memoized scan.
	/// </summary>
	private sealed class NameIndex<T>
		where T : class
	{
		private readonly Func<T, string> _nameOf;
		private readonly INameMatchResolver _resolver;
		private readonly FrozenDictionary<string, T> _exact;
		private readonly FrozenDictionary<string, T[]> _variants;
		private readonly ConcurrentDictionary<string, T?> _scanCache = new(StringComparer.Ordinal);

		public NameIndex(IEnumerable<T> items, Func<T, string> nameOf, INameMatchResolver resolver)
		{
			_nameOf = nameOf;
			_resolver = resolver;
			Items = items.ToArray();

			var exact = new Dictionary<string, T>(StringComparer.Ordinal);
			var variants = new Dictionary<string, List<T>>(StringComparer.Ordinal);
			var pluralizer = resolver is BestMatchResolver ? Pluralizers.Cached : null;
			foreach (var item in Items)
			{
				var name = nameOf(item);
				exact.TryAdd(name, item);
				var homogenized = name.Homogenize();
				Add(variants, homogenized, item);
				if (pluralizer is not null)
				{
					Add(variants, pluralizer.Singularize(homogenized), item);
					Add(variants, pluralizer.Pluralize(homogenized), item);
				}
			}

			_exact = exact.ToFrozenDictionary(StringComparer.Ordinal);
			_variants = variants.ToFrozenDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
		}

		public IReadOnlyList<T> Items { get; }

		public bool TryFind(string name, out T item)
		{
			var found = Find(name);
			item = found!;
			return found is not null;
		}

		public IEnumerable<T> FindAll(string name)
		{
			var shortName = ODataNameMatchResolver.StripNamespace(name);
			return Items.Where(x => _resolver.IsMatch(_nameOf(x), shortName));
		}

		private T? Find(string name)
		{
			if (_exact.TryGetValue(name, out var item))
			{
				return item;
			}

			var shortName = ODataNameMatchResolver.StripNamespace(name);
			if (!ReferenceEquals(shortName, name) && _exact.TryGetValue(shortName, out item))
			{
				return item;
			}

			return _resolver switch
			{
				ExactMatchResolver { AlphanumericComparison: false, Comparison: StringComparison.Ordinal } => null,
				ExactMatchResolver { AlphanumericComparison: false } => _scanCache.GetOrAdd(name, Scan),
				ExactMatchResolver => FindVariant(shortName.Homogenize(), strictFirst: false),
				BestMatchResolver => FindBestMatch(shortName),
				_ => _scanCache.GetOrAdd(name, Scan),
			};
		}

		private T? FindBestMatch(string shortName)
		{
			var homogenized = shortName.Homogenize();
			var match = FindVariant(homogenized, strictFirst: true);
			if (match is not null)
			{
				return match;
			}

			var pluralizer = Pluralizers.Cached;
			return FindVariant(pluralizer.Singularize(homogenized), strictFirst: true)
				?? FindVariant(pluralizer.Pluralize(homogenized), strictFirst: true);
		}

		private T? FindVariant(string key, bool strictFirst)
		{
			if (!_variants.TryGetValue(key, out var candidates))
			{
				return null;
			}

			if (strictFirst && candidates.Length > 1)
			{
				foreach (var candidate in candidates)
				{
					if (_nameOf(candidate).Homogenize() == key)
					{
						return candidate;
					}
				}
			}

			return candidates[0];
		}

		private T? Scan(string name)
		{
			var shortName = ODataNameMatchResolver.StripNamespace(name);
			return Items.BestMatch(_nameOf, shortName, _resolver);
		}

		private static void Add(Dictionary<string, List<T>> variants, string key, T item)
		{
			if (!variants.TryGetValue(key, out var list))
			{
				list = [];
				variants.Add(key, list);
			}

			if (!list.Contains(item))
			{
				list.Add(item);
			}
		}
	}
}
