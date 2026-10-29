using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace Simple.OData.Client.Metadata;

/// <summary>
/// The <see cref="IMetadata"/> implementation over an <see cref="EdmMetadataIndex"/>.
/// </summary>
internal sealed class MetadataResolver : IMetadata
{
	private readonly IEdmModel _model;
	private readonly INameMatchResolver _resolver;

	public MetadataResolver(IEdmModel model, INameMatchResolver resolver, bool ignoreUnmappedProperties, bool unqualifiedNameCall)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(resolver);
		_model = model;
		_resolver = resolver;
		Index = new EdmMetadataIndex(model, resolver);
		IgnoreUnmappedProperties = ignoreUnmappedProperties;
		UnqualifiedNameCall = unqualifiedNameCall;
	}

	public EdmMetadataIndex Index { get; }

	public IEdmModel Model => _model;

	public bool IgnoreUnmappedProperties { get; }

	public bool UnqualifiedNameCall { get; }

	// ---- collections -------------------------------------------------------------------------------------------

	public EntityCollection GetEntityCollection(string collectionPath)
	{
		ArgumentNullException.ThrowIfNull(collectionPath);
		var segments = collectionPath.Split('/');
		if (segments.Length == 1)
		{
			return new EntityCollection(GetEntityCollectionExactName(Utils.ExtractCollectionName(collectionPath)));
		}

		if (SegmentsIncludeTypeSpecification(segments))
		{
			var baseCollection = GetEntityCollection(Utils.ExtractCollectionName(segments[^2]));
			return GetDerivedEntityCollection(baseCollection, Utils.ExtractCollectionName(segments[^1]));
		}

		var lastSegment = Utils.ExtractCollectionName(segments[^1]);
		return TryGetEntityCollectionExactName(lastSegment, out var exactName)
			? new EntityCollection(exactName)
			: NavigateToCollection(collectionPath);
	}

	public EntityCollection GetDerivedEntityCollection(EntityCollection baseCollection, string entityTypeName)
	{
		ArgumentNullException.ThrowIfNull(baseCollection);
		return new EntityCollection(GetDerivedEntityTypeExactName(baseCollection.Name, entityTypeName), baseCollection);
	}

	public EntityCollection NavigateToCollection(string path)
	{
		ArgumentNullException.ThrowIfNull(path);
		var segments = GetCollectionPathSegments(path);
		return IsSingleSegmentWithTypeSpecification(segments)
			? GetEntityCollection(path)
			: NavigateToCollection(GetEntityCollection(segments[0]), segments.AsSpan(1).ToArray());
	}

	public EntityCollection NavigateToCollection(EntityCollection rootCollection, string path)
	{
		ArgumentNullException.ThrowIfNull(rootCollection);
		ArgumentNullException.ThrowIfNull(path);
		return NavigateToCollection(rootCollection, GetCollectionPathSegments(path));
	}

	private EntityCollection NavigateToCollection(EntityCollection rootCollection, string[] segments)
	{
		if (segments.Length == 0)
		{
			return rootCollection;
		}

		if (IsSingleSegmentWithTypeSpecification(segments))
		{
			var navigated = NavigateToCollection(rootCollection, [segments[0]]);
			return GetDerivedEntityCollection(navigated, segments[1]);
		}

		if (segments.Length == 1 && segments[0].Contains('.'))
		{
			return GetDerivedEntityCollection(rootCollection, segments[0]);
		}

		var navigationProperty = GetNavigationProperty(rootCollection.Name, segments[0]);
		var targetCollection = GetNavigationTargetCollection(rootCollection, navigationProperty);
		return segments.Length == 1
			? targetCollection
			: NavigateToCollection(targetCollection, segments.AsSpan(1).ToArray());
	}

	public string GetEntityCollectionExactName(string collectionName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		return TryGetEntityCollectionExactName(collectionName, out var name)
			? name
			: throw new UnresolvableObjectException(collectionName, $"Entity collection [{collectionName}] not found");
	}

	private bool TryGetEntityCollectionExactName(string collectionName, out string exactName)
	{
		if (TryFindEntitySet(collectionName, out var entitySet))
		{
			exactName = entitySet.Name;
			return true;
		}

		if (TryFindSingleton(collectionName, out var singleton))
		{
			exactName = singleton.Name;
			return true;
		}

		if (Index.TryFindEntityType(collectionName, out var entityType))
		{
			exactName = entityType.Name;
			return true;
		}

		exactName = string.Empty;
		return false;
	}

	public bool IsSingleton(string collectionName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		return !TryFindEntitySet(collectionName, out _) && TryFindSingleton(collectionName, out _);
	}

	public bool EntityCollectionRequiresOptimisticConcurrencyCheck(string collectionName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		IEdmVocabularyAnnotatable? annotatable = null;
		IEdmEntityType? entityType = null;
		if (TryFindEntitySet(collectionName, out var entitySet))
		{
			annotatable = entitySet;
			entityType = entitySet.EntityType;
		}
		else if (TryFindSingleton(collectionName, out var singleton))
		{
			annotatable = singleton;
			entityType = singleton.EntityType;
		}
		else if (Index.TryFindEntityType(collectionName, out var type))
		{
			annotatable = type;
			entityType = type;
		}

		if (annotatable is null)
		{
			return false;
		}

		return Index.RequiresOptimisticConcurrency(annotatable)
			|| (entityType is not null && !ReferenceEquals(entityType, annotatable) && Index.RequiresOptimisticConcurrency(entityType));
	}

	// ---- types -------------------------------------------------------------------------------------------------

	public string GetEntityTypeExactName(string collectionName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		return GetEntityType(collectionName).Name;
	}

	public string GetDerivedEntityTypeExactName(string collectionName, string entityTypeName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		ArgumentNullException.ThrowIfNull(entityTypeName);

		var baseType = FindEntityTypeForCollection(collectionName);
		if (baseType is not null)
		{
			var derived = Index.DerivedTypes(baseType).BestMatch(x => x.Name, ODataNameMatchResolver.StripNamespace(entityTypeName), _resolver);
			if (derived is not null)
			{
				return derived.Name;
			}

			if (Index.TryFindEntityType(entityTypeName, out var exact) && exact.IsOrInheritsFrom(baseType))
			{
				return exact.Name;
			}
		}
		else if (Index.TryFindEntityType(entityTypeName, out var entityType))
		{
			return entityType.Name;
		}

		throw new UnresolvableObjectException(entityTypeName, $"Entity type [{entityTypeName}] not found");
	}

	public string GetLinkedCollectionName(string instanceTypeName, string typeName, out bool isSingleton)
	{
		ArgumentNullException.ThrowIfNull(instanceTypeName);
		ArgumentNullException.ThrowIfNull(typeName);
		isSingleton = false;

		foreach (var candidate in new[] { instanceTypeName, typeName })
		{
			if (TryFindEntitySet(candidate, out var entitySet))
			{
				return entitySet.Name;
			}

			if (TryFindSingleton(candidate, out var singleton))
			{
				isSingleton = true;
				return singleton.Name;
			}

			if (Index.TryFindEntityType(candidate, out var entityType))
			{
				return FindEntitySetForType(entityType)?.Name ?? entityType.Name;
			}
		}

		throw new UnresolvableObjectException(typeName, $"Linked collection for type [{instanceTypeName}] not found");
	}

	public string GetQualifiedTypeName(string typeOrCollectionName)
	{
		ArgumentNullException.ThrowIfNull(typeOrCollectionName);
		var entityType = FindEntityTypeForCollection(typeOrCollectionName);
		if (entityType is not null)
		{
			return entityType.FullName();
		}

		if (Index.TryFindComplexType(typeOrCollectionName, out var complexType))
		{
			return complexType.FullName();
		}

		if (Index.TryFindEnumType(typeOrCollectionName, out var enumType))
		{
			return enumType.FullName();
		}

		throw new UnresolvableObjectException(typeOrCollectionName, $"Type [{typeOrCollectionName}] not found");
	}

	public bool IsOpenType(string collectionName) => GetEntityType(collectionName).IsOpen;

	public bool IsTypeWithId(string typeName)
	{
		ArgumentNullException.ThrowIfNull(typeName);
		var entityType = FindEntityTypeForCollection(typeName);
		return entityType is not null && entityType.Key().Any();
	}

	public bool HasStream(string collectionName) => GetEntityType(collectionName).HasStream;

	// ---- structural properties ---------------------------------------------------------------------------------

	public IReadOnlyList<string> GetStructuralPropertyNames(string collectionName) =>
		Index.StructuralProperties(GetEntityType(collectionName)).Select(x => x.Name).ToArray();

	public bool HasStructuralProperty(string collectionName, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Index.TryFindStructuralProperty(GetEntityType(collectionName), propertyName, out _);
	}

	public string GetStructuralPropertyExactName(string collectionName, string propertyName) =>
		GetStructuralProperty(GetEntityType(collectionName), propertyName).Name;

	public string GetStructuralPropertyPath(string collectionName, params string[] propertyNames)
	{
		ArgumentNullException.ThrowIfNull(propertyNames);
		if (propertyNames.Length == 0)
		{
			throw new ArgumentException("At least one property name is required.", nameof(propertyNames));
		}

		IEdmStructuredType current = GetEntityType(collectionName);
		var segments = new string[propertyNames.Length];
		var count = 0;
		foreach (var propertyName in propertyNames)
		{
			var property = GetStructuralProperty(current, propertyName);
			segments[count++] = property.Name;
			var next = property.Type.AsStructuredTypeOrElement();
			if (next is null)
			{
				break;
			}

			current = next;
		}

		return string.Join('/', segments, 0, count);
	}

	public IReadOnlyList<string> GetDeclaredKeyPropertyNames(string collectionName) => Index.DeclaredKeyNames(GetEntityType(collectionName));

	public IReadOnlyList<IReadOnlyList<string>> GetAlternateKeyPropertyNames(string collectionName) => Index.AlternateKeyNames(GetEntityType(collectionName));

	// ---- navigation properties ---------------------------------------------------------------------------------

	public IReadOnlyList<string> GetNavigationPropertyNames(string collectionName) =>
		Index.NavigationProperties(GetEntityType(collectionName)).Select(x => x.Name).ToArray();

	public bool HasNavigationProperty(string collectionName, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Index.TryFindNavigationProperty(GetEntityType(collectionName), propertyName, out _);
	}

	public string GetNavigationPropertyExactName(string collectionName, string propertyName) => GetNavigationProperty(collectionName, propertyName).Name;

	public string GetNavigationPropertyPartnerTypeName(string collectionName, string propertyName)
	{
		var navigationProperty = GetNavigationProperty(collectionName, propertyName);
		return navigationProperty.Type.AsEntityTypeOrElement()?.Name
			?? throw new UnresolvableObjectException(propertyName, $"No association found for [{propertyName}].");
	}

	public string GetNavigationPropertyTargetSetName(string collectionName, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		var navigationProperty = GetNavigationProperty(collectionName, propertyName);
		return GetNavigationTargetCollection(new EntityCollection(GetEntityCollectionExactName(Utils.ExtractCollectionName(collectionName))), navigationProperty).Name;
	}

	public bool IsNavigationPropertyCollection(string collectionName, string propertyName) => GetNavigationProperty(collectionName, propertyName).Type.IsCollection();

	public bool IsNavigationPropertyContained(string collectionName, string propertyName) => GetNavigationProperty(collectionName, propertyName).ContainsTarget;

	// ---- operations --------------------------------------------------------------------------------------------

	public string GetFunctionFullName(string functionName)
	{
		ArgumentNullException.ThrowIfNull(functionName);
		if (Index.TryFindFunctionImport(functionName, out var import))
		{
			return import.Name;
		}

		var function = GetFunction(functionName);
		return function.IsBound && !UnqualifiedNameCall ? function.ShortQualifiedName() : function.Name;
	}

	public EntityCollection? GetFunctionReturnCollection(string functionName)
	{
		var function = GetFunction(functionName);
		var entityType = function.ReturnType?.AsEntityTypeOrElement();
		return entityType is null ? null : new EntityCollection(entityType.Name);
	}

	public string GetActionFullName(string actionName)
	{
		ArgumentNullException.ThrowIfNull(actionName);
		if (Index.TryFindActionImport(actionName, out var import))
		{
			return import.Name;
		}

		var action = GetAction(actionName);
		return action.IsBound && !UnqualifiedNameCall ? action.ShortQualifiedName() : action.Name;
	}

	public EntityCollection? GetActionReturnCollection(string actionName)
	{
		var action = GetAction(actionName);
		var entityType = action.ReturnType?.AsEntityTypeOrElement();
		return entityType is null ? null : new EntityCollection(entityType.Name);
	}

	// ---- entries -----------------------------------------------------------------------------------------------

	public EntryDetails ParseEntryDetails(string collectionName, IDictionary<string, object?> entryData, string? contentId = null)
	{
		ArgumentNullException.ThrowIfNull(entryData);
		var entityType = GetEntityType(collectionName);
		var details = new EntryDetails();

		foreach (var item in entryData)
		{
			if (Index.TryFindStructuralProperty(entityType, item.Key, out _))
			{
				details.AddProperty(item.Key, item.Value);
			}
			else if (Index.TryFindNavigationProperty(entityType, item.Key, out var navigationProperty))
			{
				if (navigationProperty.Type.IsCollection())
				{
					switch (item.Value)
					{
						case null:
							details.AddLink(item.Key, null, contentId);
							break;
						case IEnumerable<object?> collection:
							foreach (var element in collection)
							{
								details.AddLink(item.Key, element, contentId);
							}

							break;
						default:
							details.AddLink(item.Key, item.Value, contentId);
							break;
					}
				}
				else
				{
					details.AddLink(item.Key, item.Value, contentId);
				}
			}
			else if (entityType.IsOpen)
			{
				details.HasOpenTypeProperties = true;
				details.AddProperty(item.Key, item.Value);
			}
			else if (!IgnoreUnmappedProperties)
			{
				throw new UnresolvableObjectException(item.Key, $"No property or association found for [{item.Key}].");
			}
		}

		return details;
	}

	// ---- internal lookups --------------------------------------------------------------------------------------

	internal IEdmEntityType GetEntityType(string collectionName)
	{
		ArgumentNullException.ThrowIfNull(collectionName);
		return FindEntityTypeForCollection(collectionName)
			?? throw new UnresolvableObjectException(collectionName, $"Entity type [{collectionName}] not found");
	}

	internal IEdmEntityType? FindEntityTypeForCollection(string collectionName)
	{
		if (collectionName.Contains('/'))
		{
			var segments = GetCollectionPathSegments(collectionName);
			if (SegmentsIncludeTypeSpecification(segments) && Index.TryFindEntityType(segments[^1], out var derivedType))
			{
				return derivedType;
			}

			var collection = NavigateToCollection(collectionName);
			return FindEntityTypeByCollectionOrTypeName(collection.Name);
		}

		return FindEntityTypeByCollectionOrTypeName(collectionName);
	}

	private IEdmEntityType? FindEntityTypeByCollectionOrTypeName(string name)
	{
		if (TryFindEntitySet(name, out var entitySet))
		{
			return entitySet.EntityType;
		}

		if (TryFindSingleton(name, out var singleton))
		{
			return singleton.EntityType;
		}

		return Index.TryFindEntityType(name, out var entityType) ? entityType : null;
	}

	internal IEdmNavigationSource? FindNavigationSource(string collectionName)
	{
		var name = Utils.ExtractCollectionName(collectionName);
		return Index.TryFindNavigationSource(name, out var source) ? source : null;
	}

	internal IEdmEntitySet? FindEntitySetForType(IEdmEntityType entityType)
	{
		IEdmEntitySet? inherited = null;
		foreach (var container in _model.AllEntityContainers())
		{
			foreach (var entitySet in container.EntitySets())
			{
				if (entitySet.EntityType == entityType)
				{
					return entitySet;
				}

				if (inherited is null && entityType.InheritsFrom(entitySet.EntityType))
				{
					inherited = entitySet;
				}
			}
		}

		return inherited;
	}

	private EntityCollection GetNavigationTargetCollection(EntityCollection rootCollection, IEdmNavigationProperty navigationProperty)
	{
		var rootName = rootCollection.BaseEntityCollection?.Name ?? rootCollection.Name;
		if (Index.TryFindNavigationSource(rootName, out var source))
		{
			var target = source.FindNavigationTarget(navigationProperty);
			if (target is not null and not IEdmUnknownEntitySet)
			{
				return new EntityCollection(target.Name);
			}
		}

		var targetType = navigationProperty.Type.AsEntityTypeOrElement()
			?? throw new UnresolvableObjectException(navigationProperty.Name, $"No association found for [{navigationProperty.Name}].");
		var targetSet = FindEntitySetForType(targetType);
		return new EntityCollection(targetSet?.Name ?? GetEntityCollectionExactName(targetType.Name));
	}

	private IEdmStructuralProperty GetStructuralProperty(IEdmStructuredType type, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Index.TryFindStructuralProperty(type, propertyName, out var property)
			? property
			: throw new UnresolvableObjectException(propertyName, $"Structural property [{propertyName}] not found");
	}

	private IEdmNavigationProperty GetNavigationProperty(string collectionName, string propertyName)
	{
		ArgumentNullException.ThrowIfNull(propertyName);
		return Index.TryFindNavigationProperty(GetEntityType(collectionName), propertyName, out var property)
			? property
			: throw new UnresolvableObjectException(propertyName, $"Association [{propertyName}] not found");
	}

	internal IEdmFunction GetFunction(string functionName)
	{
		ArgumentNullException.ThrowIfNull(functionName);
		return Index.TryFindFunction(functionName, out var function)
			? function
			: throw new UnresolvableObjectException(functionName, $"Function [{functionName}] not found");
	}

	internal IEdmAction GetAction(string actionName)
	{
		ArgumentNullException.ThrowIfNull(actionName);
		return Index.TryFindAction(actionName, out var action)
			? action
			: throw new UnresolvableObjectException(actionName, $"Action [{actionName}] not found");
	}

	private bool TryFindEntitySet(string name, out IEdmEntitySet entitySet)
	{
		if (Index.TryFindEntitySet(name, out entitySet))
		{
			return true;
		}

		var slash = name.IndexOf('/');
		return slash > 0 && Index.TryFindEntitySet(name[..slash], out entitySet);
	}

	private bool TryFindSingleton(string name, out IEdmSingleton singleton)
	{
		if (Index.TryFindSingleton(name, out singleton))
		{
			return true;
		}

		var slash = name.IndexOf('/');
		return slash > 0 && Index.TryFindSingleton(name[..slash], out singleton);
	}

	internal static string[] GetCollectionPathSegments(string path)
	{
		var segments = path.Split('/');
		for (var i = 0; i < segments.Length; i++)
		{
			var parenthesis = segments[i].IndexOf('(');
			if (parenthesis >= 0)
			{
				segments[i] = segments[i][..parenthesis];
			}
		}

		return segments;
	}

	private static bool SegmentsIncludeTypeSpecification(string[] segments) => segments.Length > 0 && segments[^1].Contains('.');

	private static bool IsSingleSegmentWithTypeSpecification(string[] segments) => segments.Length == 2 && SegmentsIncludeTypeSpecification(segments);
}
