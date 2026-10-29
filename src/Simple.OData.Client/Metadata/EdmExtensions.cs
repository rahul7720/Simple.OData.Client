using Microsoft.OData.Edm;

namespace Simple.OData.Client.Metadata;

/// <summary>
/// Convenience members over the Edm object model.
/// </summary>
internal static class EdmExtensions
{
	extension(IEdmType type)
	{
		/// <summary>The type itself, or the element type of a collection type.</summary>
		public IEdmType ElementTypeOrSelf() => type is IEdmCollectionType collection ? collection.ElementType.Definition : type;

		/// <summary>The entity type represented by the type or its collection element, if any.</summary>
		public IEdmEntityType? AsEntityTypeOrElement() => type.ElementTypeOrSelf() as IEdmEntityType;

		/// <summary>The structured type represented by the type or its collection element, if any.</summary>
		public IEdmStructuredType? AsStructuredTypeOrElement() => type.ElementTypeOrSelf() as IEdmStructuredType;
	}

	extension(IEdmTypeReference typeReference)
	{
		public IEdmType ElementTypeOrSelf() => typeReference.Definition.ElementTypeOrSelf();

		public IEdmEntityType? AsEntityTypeOrElement() => typeReference.Definition.AsEntityTypeOrElement();

		public IEdmStructuredType? AsStructuredTypeOrElement() => typeReference.Definition.AsStructuredTypeOrElement();
	}

	extension(IEdmEntityType entityType)
	{
		/// <summary>The type in the inheritance chain that declares the key.</summary>
		public IEdmEntityType KeyOwner()
		{
			var current = entityType;
			while (current.DeclaredKey is null && current.BaseEntityType() is { } baseType)
			{
				current = baseType;
			}

			return current;
		}

		/// <summary>Whether this type is <paramref name="other"/> or derives from it.</summary>
		public bool IsOrInheritsFrom(IEdmEntityType other) => entityType == other || entityType.InheritsFrom(other);
	}

	extension(IEdmModel model)
	{
		/// <summary>All entity containers of the model and the models it references.</summary>
		public IEnumerable<IEdmEntityContainer> AllEntityContainers()
		{
			var seen = new HashSet<IEdmEntityContainer>();
			if (model.EntityContainer is not null && seen.Add(model.EntityContainer))
			{
				yield return model.EntityContainer;
			}

			foreach (var container in model.SchemaElements.OfType<IEdmEntityContainer>())
			{
				if (seen.Add(container))
				{
					yield return container;
				}
			}

			foreach (var referenced in model.ReferencedModels)
			{
				foreach (var container in referenced.SchemaElements.OfType<IEdmEntityContainer>())
				{
					if (seen.Add(container))
					{
						yield return container;
					}
				}
			}
		}
	}
}
