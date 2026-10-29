namespace Simple.OData.Client;

/// <summary>
/// Name-tolerant access to the service's Entity Data Model.
/// Collection names accept entity set names, singleton names, entity type names and navigation paths.
/// </summary>
public interface IMetadata
{
	/// <summary>Resolves a collection path (<c>Products</c>, <c>Transports/NS.Ships</c>, <c>Orders(1)/Order_Details</c>) to an <see cref="EntityCollection"/>.</summary>
	EntityCollection GetEntityCollection(string collectionPath);

	/// <summary>Returns the derived-type projection of <paramref name="baseCollection"/>.</summary>
	EntityCollection GetDerivedEntityCollection(EntityCollection baseCollection, string entityTypeName);

	/// <summary>Follows a navigation path from its first segment and returns the target collection.</summary>
	EntityCollection NavigateToCollection(string path);

	/// <summary>Follows a navigation path from <paramref name="rootCollection"/> and returns the target collection.</summary>
	EntityCollection NavigateToCollection(EntityCollection rootCollection, string path);

	/// <summary>Returns the exact entity set, singleton or type name for a loosely spelled name.</summary>
	string GetEntityCollectionExactName(string collectionName);

	/// <summary>Whether the name refers to a singleton.</summary>
	bool IsSingleton(string collectionName);

	/// <summary>Whether the entity set, singleton or type carries a <c>Core.OptimisticConcurrency</c> annotation.</summary>
	bool EntityCollectionRequiresOptimisticConcurrencyCheck(string collectionName);

	/// <summary>Returns the exact entity type name for a collection or type name.</summary>
	string GetEntityTypeExactName(string collectionName);

	/// <summary>Returns the exact name of a type derived from the collection's type.</summary>
	string GetDerivedEntityTypeExactName(string collectionName, string entityTypeName);

	/// <summary>Returns the entity set (or singleton) name that holds instances of the given CLR or entity type name.</summary>
	string GetLinkedCollectionName(string instanceTypeName, string typeName, out bool isSingleton);

	/// <summary>Returns the namespace-qualified name of an entity, complex or enum type (collection names resolve to their entity type).</summary>
	string GetQualifiedTypeName(string typeOrCollectionName);

	/// <summary>Whether the collection's entity type is open.</summary>
	bool IsOpenType(string collectionName);

	/// <summary>Whether the type is an entity type with a key.</summary>
	bool IsTypeWithId(string typeName);

	/// <summary>Whether the collection's entity type is a media entity (<c>HasStream</c>).</summary>
	bool HasStream(string collectionName);

	/// <summary>The structural property names of the collection's type.</summary>
	IReadOnlyList<string> GetStructuralPropertyNames(string collectionName);

	/// <summary>Whether the collection's type has the structural property.</summary>
	bool HasStructuralProperty(string collectionName, string propertyName);

	/// <summary>Returns the exact name of a structural property.</summary>
	string GetStructuralPropertyExactName(string collectionName, string propertyName);

	/// <summary>Returns the exact <c>/</c>-separated path through complex properties.</summary>
	string GetStructuralPropertyPath(string collectionName, params string[] propertyNames);

	/// <summary>The declared key property names of the collection's type (walking base types).</summary>
	IReadOnlyList<string> GetDeclaredKeyPropertyNames(string collectionName);

	/// <summary>The alternate keys of the collection's type (<c>OData.Community.Keys.V1</c> / <c>Core.AlternateKeys</c>), each as a list of key aliases.</summary>
	IReadOnlyList<IReadOnlyList<string>> GetAlternateKeyPropertyNames(string collectionName);

	/// <summary>The navigation property names of the collection's type.</summary>
	IReadOnlyList<string> GetNavigationPropertyNames(string collectionName);

	/// <summary>Whether the collection's type has the navigation property.</summary>
	bool HasNavigationProperty(string collectionName, string propertyName);

	/// <summary>Returns the exact name of a navigation property.</summary>
	string GetNavigationPropertyExactName(string collectionName, string propertyName);

	/// <summary>Returns the entity type name targeted by a navigation property.</summary>
	string GetNavigationPropertyPartnerTypeName(string collectionName, string propertyName);

	/// <summary>Returns the entity set (or contained set) name targeted by a navigation property.</summary>
	string GetNavigationPropertyTargetSetName(string collectionName, string propertyName);

	/// <summary>Whether the navigation property is a collection.</summary>
	bool IsNavigationPropertyCollection(string collectionName, string propertyName);

	/// <summary>Whether the navigation property is a containment navigation (<c>ContainsTarget</c>).</summary>
	bool IsNavigationPropertyContained(string collectionName, string propertyName);

	/// <summary>Returns the name used to invoke a function: the import name, or the qualified name for bound functions.</summary>
	string GetFunctionFullName(string functionName);

	/// <summary>Returns the entity collection a function returns, or <see langword="null"/> for non-entity results.</summary>
	EntityCollection? GetFunctionReturnCollection(string functionName);

	/// <summary>Returns the name used to invoke an action: the import name, or the qualified name for bound actions.</summary>
	string GetActionFullName(string actionName);

	/// <summary>Returns the entity collection an action returns, or <see langword="null"/> for non-entity results.</summary>
	EntityCollection? GetActionReturnCollection(string actionName);

	/// <summary>Splits entry data into structural properties and navigation links.</summary>
	EntryDetails ParseEntryDetails(string collectionName, IDictionary<string, object?> entryData, string? contentId = null);
}
