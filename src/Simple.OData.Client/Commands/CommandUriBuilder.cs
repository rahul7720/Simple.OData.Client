using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client.Commands;

/// <summary>Renders a <see cref="ResolvedCommand"/> as a relative OData request URL.</summary>
internal sealed class CommandUriBuilder(Session session) : IUriValueFormatter
{
	private static readonly SearchValues<char> ReservedUriCharacters = SearchValues.Create("!*'();:@&=+$,/?#[] ");

	private IMetadata Metadata => session.Metadata;

	/// <summary>Formats the resource path and query options of <paramref name="command"/>.</summary>
	public string Format(ResolvedCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		var details = command.Details;
		if (details.HasFunction && details.HasAction)
		{
			throw new InvalidOperationException("OData function and action may not be combined.");
		}

		var path = new StringBuilder();
		var queryClauses = new List<string>();

		if (!string.IsNullOrEmpty(details.CollectionName))
		{
			path.Append(Metadata.GetEntityCollectionExactName(details.CollectionName));
		}
		else if (!string.IsNullOrEmpty(details.LinkName))
		{
			var parentDetails = details.Parent ?? throw new InvalidOperationException("A navigation link requires a parent command.");
			var parent = new FluentCommand(parentDetails).Resolve(session);
			var parentCollection = parent.EntityCollection ?? throw new InvalidOperationException("The parent command does not target an entity collection.");
			path.Append(Format(parent)).Append('/').Append(Metadata.GetNavigationPropertyExactName(parentCollection.Name, details.LinkName));
		}

		if (details.HasKey)
		{
			AppendKey(path, command.KeyValues!, details.IsAlternateKey);
		}

		if (!string.IsNullOrEmpty(details.MediaName))
		{
			path.Append('/').Append(FormatMediaSegment(command.EntityCollection, details.MediaName));
		}
		else
		{
			if (details.HasFunction || details.HasAction)
			{
				if (path.Length > 0)
				{
					path.Append('/');
				}

				if (details.HasFunction)
				{
					path.Append(Metadata.GetFunctionFullName(details.FunctionName!));
					AppendFunctionParameters(path, command.CommandData, queryClauses);
				}
				else
				{
					path.Append(Metadata.GetActionFullName(details.ActionName!));
				}
			}

			if (!string.IsNullOrEmpty(details.DerivedCollectionName))
			{
				path.Append('/').Append(Metadata.GetQualifiedTypeName(details.DerivedCollectionName));
			}
		}

		if (details.UseEachSegment)
		{
			path.Append('/').Append(ODataLiteral.Each);
		}

		if (details.ComputeCount)
		{
			path.Append('/').Append(ODataLiteral.Count);
		}

		AppendQueryClauses(command, queryClauses);
		if (queryClauses.Count > 0)
		{
			path.Append('?').AppendJoin('&', queryClauses);
		}

		return path.ToString();
	}

	/// <summary>Resolves each segment of a navigation path to its exact metadata name.</summary>
	public string FormatNavigationPath(EntityCollection collection, string path)
	{
		ArgumentNullException.ThrowIfNull(collection);
		ArgumentException.ThrowIfNullOrEmpty(path);
		var separator = path.IndexOf('/');
		var first = separator < 0 ? path : path[..separator];
		if (!Metadata.HasNavigationProperty(collection.Name, first))
		{
			return path;
		}

		var exactName = Metadata.GetNavigationPropertyExactName(collection.Name, first);
		return separator < 0
			? exactName
			: $"{exactName}/{FormatNavigationPath(NavigationTarget(collection, exactName), path[(separator + 1)..])}";
	}

	/// <summary>Formats key values as a parenthesized key predicate.</summary>
	public string ConvertKeyValuesToUriLiteral(IReadOnlyDictionary<string, object?> key, bool skipKeyNameForSingleValue)
	{
		ArgumentNullException.ThrowIfNull(key);
		var builder = new StringBuilder("(");
		if (key.Count == 1 && skipKeyNameForSingleValue)
		{
			builder.Append(ConvertValueToUriLiteral(key.Values.First(), true));
		}
		else
		{
			var first = true;
			foreach (var (name, value) in key)
			{
				if (!first)
				{
					builder.Append(',');
				}

				builder.Append(name).Append('=').Append(ConvertValueToUriLiteral(value, true));
				first = false;
			}
		}

		return builder.Append(')').ToString();
	}

	/// <inheritdoc />
	public string ConvertValueToUriLiteral(object? value, bool escapeDataString) => UriLiteralFormatter.Format(session, value, escapeDataString);

	private static string EscapeUnescapedString(string text) =>
		text.AsSpan().ContainsAny(ReservedUriCharacters) ? Uri.EscapeDataString(text) : text;

	private void AppendKey(StringBuilder path, IReadOnlyDictionary<string, object?> key, bool isAlternateKey)
	{
		if (!isAlternateKey && key.Count == 1 && session.Settings.KeyAsSegment)
		{
			var value = key.Values.First();
			path.Append('/').Append(value is string text ? Uri.EscapeDataString(text) : ConvertValueToUriLiteral(value, true));
			return;
		}

		path.Append(ConvertKeyValuesToUriLiteral(key, !isAlternateKey));
	}

	private string FormatMediaSegment(EntityCollection? collection, string mediaName)
	{
		if (mediaName == ReservedNames.MediaEntity)
		{
			return ODataLiteral.Value;
		}

		return collection is not null && Metadata.HasStructuralProperty(collection.Name, mediaName)
			? Metadata.GetStructuralPropertyExactName(collection.Name, mediaName)
			: mediaName;
	}

	private void AppendFunctionParameters(StringBuilder path, IDictionary<string, object?> parameters, List<string> queryClauses)
	{
		path.Append('(');
		var first = true;
		var aliasIndex = 0;
		foreach (var (name, value) in parameters)
		{
			if (!first)
			{
				path.Append(',');
			}

			path.Append(name).Append('=');
			if (value is IEnumerable items and not (string or byte[] or IDictionary))
			{
				var alias = "@p" + (++aliasIndex).ToString(CultureInfo.InvariantCulture);
				var literals = items.Cast<object?>().Select(x => ConvertValueToUriLiteral(x, true));
				queryClauses.Add($"{alias}=[{string.Join(',', literals)}]");
				path.Append(alias);
			}
			else
			{
				path.Append(ConvertValueToUriLiteral(value, true));
			}

			first = false;
		}

		path.Append(')');
	}

	private void AppendQueryClauses(ResolvedCommand command, List<string> queryClauses)
	{
		var details = command.Details;
		if (details.Filter is not null)
		{
			queryClauses.Add($"{ODataLiteral.Filter}={EscapeUnescapedString(details.Filter)}");
		}

		if (details.Search is not null)
		{
			queryClauses.Add($"{ODataLiteral.Search}={EscapeUnescapedString(details.Search)}");
		}

		if (details.QueryOptions is not null)
		{
			queryClauses.Add(details.QueryOptions);
		}

		if (details.QueryOptionsExpression is not null)
		{
			queryClauses.Add(details.QueryOptionsExpression.Format(new ExpressionContext(session, isQueryOption: true)));
		}

		if (details.QueryOptionsKeyValues is not null)
		{
			foreach (var (name, value) in details.QueryOptionsKeyValues)
			{
				queryClauses.Add($"{name}={ConvertValueToUriLiteral(value, true)}");
			}
		}

		if (details.SkipCount >= 0)
		{
			queryClauses.Add($"{ODataLiteral.Skip}={details.SkipCount.ToString(CultureInfo.InvariantCulture)}");
		}

		if (details.TopCount >= 0)
		{
			queryClauses.Add($"{ODataLiteral.Top}={details.TopCount.ToString(CultureInfo.InvariantCulture)}");
		}

		if (details.Index is { } index)
		{
			queryClauses.Add($"{ODataLiteral.Index}={index.ToString(CultureInfo.InvariantCulture)}");
		}

		var resultCollection = details.HasFunction
			? Metadata.GetFunctionReturnCollection(details.FunctionName!)
			: details.HasAction
				? Metadata.GetActionReturnCollection(details.ActionName!)
				: command.EntityCollection;

		if (details.HasCompute)
		{
			queryClauses.Add($"{ODataLiteral.Compute}={EscapeUnescapedString(FormatCompute(command, resultCollection))}");
		}

		if (details.ApplyClauses.Count > 0)
		{
			var applyText = string.Join('/', details.ApplyClauses
				.Select(apply => apply switch
				{
					IApplyBuilder builder => builder.Build(command, session),
					string text => text,
					_ => throw new InvalidOperationException($"Unsupported $apply source '{apply.GetType()}'."),
				})
				.Where(static text => !string.IsNullOrEmpty(text)));

			if (applyText.Length > 0)
			{
				queryClauses.Add($"{ODataLiteral.Apply}={EscapeUnescapedString(applyText)}");
			}
		}

		if (resultCollection is not null)
		{
			FormatExpandSelectOrderBy(queryClauses, resultCollection, command);
		}

		if (details.IncludeCount)
		{
			queryClauses.Add($"{ODataLiteral.Count}={ODataLiteral.True}");
		}
	}

	private string FormatCompute(ResolvedCommand command, EntityCollection? resultCollection)
	{
		var details = command.Details;
		var parts = new List<string>(details.ComputeExpressions.Count + 1);
		if (!string.IsNullOrEmpty(details.ComputeText))
		{
			parts.Add(details.ComputeText);
		}

		foreach (var (alias, expression) in details.ComputeExpressions)
		{
			var context = new ExpressionContext(session, resultCollection, null, command.DynamicPropertiesContainerName);
			parts.Add($"{expression.Format(context)} {ODataLiteral.As} {alias}");
		}

		return string.Join(',', parts);
	}

	private void FormatExpandSelectOrderBy(List<string> queryClauses, EntityCollection resultCollection, ResolvedCommand command)
	{
		var details = command.Details;
		if (details.ExpandAssociations.Count > 0)
		{
			var merged = details.ExpandAssociations
				.GroupBy(x => x)
				.Select(group =>
				{
					var main = group.Key.Association;
					foreach (var (association, _) in group.Skip(1))
					{
						main = MergeExpandAssociations(main, association);
					}

					return (Association: main, group.Key.Options);
				});

			var formatted = string.Join(',', merged.Select(x => FormatExpansionSegment(x.Association, resultCollection, x.Options, command, rootLevel: true)));
			queryClauses.Add($"{ODataLiteral.Expand}={formatted}");
		}

		var expandedFirstSegments = details.ExpandAssociations.Select(x => FirstSegment(x.Association.Name)).ToList();
		var selectColumns = SelectPathSegmentColumns(details.SelectColumns, resultCollection, expandedFirstSegments);
		if (selectColumns.Count > 0)
		{
			queryClauses.Add($"{ODataLiteral.Select}={string.Join(',', selectColumns.Select(x => FormatSelectItem(x, resultCollection)))}");
		}

		var orderByColumns = details.OrderByColumns
			.Where(column => !details.ExpandAssociations.Any(x => IsInnerCollectionOrderBy(x.Association.Name, resultCollection, column.Name)))
			.ToList();
		if (orderByColumns.Count > 0)
		{
			queryClauses.Add($"{ODataLiteral.OrderBy}={string.Join(',', orderByColumns.Select(x => EscapeUnescapedString(FormatOrderByItem(x, resultCollection))))}");
		}
	}

	private string FormatExpansionSegment(ODataExpandAssociation association, EntityCollection collection, ODataExpandOptions options, ResolvedCommand command, bool rootLevel)
	{
		if (rootLevel)
		{
			var root = command.Details.SelectColumns.Aggregate(association, static (current, path) => MergeExpandAssociations(current, ODataExpandAssociation.From(path)));
			association = command.Details.OrderByColumns
				.Where(column => IsInnerCollectionOrderBy(root.Name, collection, column.Name))
				.Aggregate(root, MergeOrderByColumns);
		}

		var name = association.Name;
		EntityCollection? target = null;
		if (name != ODataLiteral.Star && Metadata.HasNavigationProperty(collection.Name, name))
		{
			name = Metadata.GetNavigationPropertyExactName(collection.Name, name);
			target = TryNavigationTarget(collection, name);
		}

		var text = new StringBuilder(name);
		if (options.ExpandMode == ODataExpandMode.ByReference)
		{
			text.Append('/').Append(ODataLiteral.Ref);
		}

		if (association.RawOptions is not null)
		{
			return text.Append('(').Append(association.RawOptions).Append(')').ToString();
		}

		var clauses = new List<string>();
		if (name != ODataLiteral.Star)
		{
			if (association.FilterExpression is not null)
			{
				var context = new ExpressionContext(session, target, null, command.DynamicPropertiesContainerName);
				clauses.Add($"{ODataLiteral.Filter}={EscapeUnescapedString(association.FilterExpression.Format(context))}");
			}

			var nested = new List<string>();
			var selected = new List<string>();
			var expandAll = false;
			foreach (var child in association.ExpandAssociations)
			{
				if (child.Name == ODataLiteral.Star)
				{
					expandAll = true;
				}
				else if (target is not null && Metadata.HasNavigationProperty(target.Name, child.Name))
				{
					nested.Add(FormatExpansionSegment(child, target, ODataExpandOptions.ByValue(), command, rootLevel: false));
				}
				else
				{
					selected.Add(target is not null ? FormatSelectItem(child.Name, target) : child.Name);
				}
			}

			if (selected.Count > 0)
			{
				clauses.Add($"{ODataLiteral.Select}={string.Join(',', selected)}");
			}

			if (association.OrderByColumns.Count > 0)
			{
				var columns = association.OrderByColumns.Select(x => EscapeUnescapedString(target is not null ? FormatOrderByItem(x, target) : x.ToString()));
				clauses.Add($"{ODataLiteral.OrderBy}={string.Join(',', columns)}");
			}

			if (association.Top is { } top)
			{
				clauses.Add($"{ODataLiteral.Top}={top.ToString(CultureInfo.InvariantCulture)}");
			}

			if (association.Skip is { } skip)
			{
				clauses.Add($"{ODataLiteral.Skip}={skip.ToString(CultureInfo.InvariantCulture)}");
			}

			if (association.Count)
			{
				clauses.Add($"{ODataLiteral.Count}={ODataLiteral.True}");
			}

			if (!string.IsNullOrEmpty(association.Search))
			{
				clauses.Add($"{ODataLiteral.Search}={EscapeUnescapedString(association.Search)}");
			}

			AppendLevels(clauses, options);

			if (expandAll)
			{
				clauses.Add($"{ODataLiteral.Expand}={ODataLiteral.Star}");
			}
			else if (nested.Count > 0)
			{
				clauses.Add($"{ODataLiteral.Expand}={string.Join(',', nested)}");
			}
		}
		else
		{
			AppendLevels(clauses, options);
		}

		if (clauses.Count > 0)
		{
			text.Append('(').AppendJoin(';', clauses).Append(')');
		}

		return text.ToString();
	}

	private static void AppendLevels(List<string> clauses, ODataExpandOptions options)
	{
		if (options.Levels > 1)
		{
			clauses.Add($"{ODataLiteral.Levels}={options.Levels.ToString(CultureInfo.InvariantCulture)}");
		}
		else if (options.Levels == 0)
		{
			clauses.Add($"{ODataLiteral.Levels}={ODataLiteral.Max}");
		}
	}

	private static ODataExpandAssociation MergeExpandAssociations(ODataExpandAssociation first, ODataExpandAssociation second)
	{
		if (first.Name != second.Name && first.Name != ODataLiteral.Star)
		{
			return first;
		}

		var result = first.Clone();
		result.OrderByColumns.AddRange(second.OrderByColumns.Except(first.OrderByColumns));
		result.ExpandAssociations.Clear();
		foreach (var group in first.ExpandAssociations.Concat(second.ExpandAssociations).GroupBy(x => x))
		{
			var main = group.Key;
			foreach (var association in group.Skip(1))
			{
				main = MergeExpandAssociations(main, association);
			}

			result.ExpandAssociations.Add(main);
		}

		return result;
	}

	private static ODataExpandAssociation MergeOrderByColumns(ODataExpandAssociation association, ODataOrderByColumn column)
	{
		var segments = column.Name.Split('/');
		if (segments.Length < 2 || segments[0] != association.Name)
		{
			return association;
		}

		var result = association.Clone();
		MergeOrderByColumns(result, segments, column.Descending, 1);
		return result;
	}

	private static void MergeOrderByColumns(ODataExpandAssociation association, string[] segments, bool descending, int index)
	{
		if (segments.Length == index + 1)
		{
			association.OrderByColumns.Add(new ODataOrderByColumn(segments[index], descending));
			return;
		}

		var nested = association.ExpandAssociations.FirstOrDefault(x => x.Name == segments[index]);
		if (nested is not null)
		{
			MergeOrderByColumns(nested, segments, descending, index + 1);
		}
	}

	private List<string> SelectPathSegmentColumns(List<string> columns, EntityCollection collection, List<string> expandedPaths)
	{
		var expanded = expandedPaths.Contains(ODataLiteral.Star)
			? Metadata.GetNavigationPropertyNames(collection.Name).ToHashSet(StringComparer.Ordinal)
			: expandedPaths.ToHashSet(StringComparer.Ordinal);

		return columns.Where(x => !expanded.Contains(FirstSegment(x))).ToList();
	}

	private bool IsInnerCollectionOrderBy(string expandPath, EntityCollection collection, string orderByColumn)
	{
		var expandFirst = FirstSegment(expandPath);
		if (expandFirst != FirstSegment(orderByColumn) || !Metadata.HasNavigationProperty(collection.Name, expandFirst))
		{
			return false;
		}

		var associationName = Metadata.GetNavigationPropertyExactName(collection.Name, expandFirst);
		if (Metadata.IsNavigationPropertyCollection(collection.Name, associationName))
		{
			return true;
		}

		var expandRest = SkipFirstSegment(expandPath);
		var orderByRest = SkipFirstSegment(orderByColumn);
		if (expandRest is null || orderByRest is null || FirstSegment(orderByRest) != FirstSegment(expandRest))
		{
			return false;
		}

		return IsInnerCollectionOrderBy(expandRest, NavigationTarget(collection, associationName), orderByRest);
	}

	private string FormatSelectItem(string path, EntityCollection collection)
	{
		var separator = path.IndexOf('/');
		if (separator < 0)
		{
			return Metadata.HasStructuralProperty(collection.Name, path)
				? Metadata.GetStructuralPropertyExactName(collection.Name, path)
				: Metadata.HasNavigationProperty(collection.Name, path)
					? Metadata.GetNavigationPropertyExactName(collection.Name, path)
					: path;
		}

		var first = path[..separator];
		if (!Metadata.HasNavigationProperty(collection.Name, first))
		{
			return path;
		}

		var associationName = Metadata.GetNavigationPropertyExactName(collection.Name, first);
		return $"{associationName}/{FormatSelectItem(path[(separator + 1)..], NavigationTarget(collection, associationName))}";
	}

	private string FormatOrderByItem(ODataOrderByColumn column, EntityCollection collection)
	{
		var path = column.Name;
		var suffix = column.Descending ? " desc" : string.Empty;
		var separator = path.IndexOf('/');
		if (separator < 0)
		{
			var name = Metadata.HasStructuralProperty(collection.Name, path)
				? Metadata.GetStructuralPropertyExactName(collection.Name, path)
				: Metadata.HasNavigationProperty(collection.Name, path)
					? Metadata.GetNavigationPropertyExactName(collection.Name, path)
					: path;
			return name + suffix;
		}

		var first = path[..separator];
		if (Metadata.HasNavigationProperty(collection.Name, first))
		{
			var associationName = Metadata.GetNavigationPropertyExactName(collection.Name, first);
			var rest = new ODataOrderByColumn(path[(separator + 1)..], column.Descending);
			return $"{associationName}/{FormatOrderByItem(rest, NavigationTarget(collection, associationName))}";
		}

		if (Metadata.HasStructuralProperty(collection.Name, first))
		{
			return Metadata.GetStructuralPropertyPath(collection.Name, path.Split('/')) + suffix;
		}

		throw new UnresolvableObjectException(first, $"Property path [{first}] not found");
	}

	private EntityCollection NavigationTarget(EntityCollection collection, string navigationPropertyName) =>
		Metadata.GetEntityCollection(Metadata.GetNavigationPropertyPartnerTypeName(collection.Name, navigationPropertyName));

	private EntityCollection? TryNavigationTarget(EntityCollection collection, string navigationPropertyName)
	{
		try
		{
			return NavigationTarget(collection, navigationPropertyName);
		}
		catch (UnresolvableObjectException)
		{
			return null;
		}
	}

	private static string FirstSegment(string path)
	{
		var separator = path.IndexOf('/');
		return separator < 0 ? path : path[..separator];
	}

	private static string? SkipFirstSegment(string path)
	{
		var separator = path.IndexOf('/');
		return separator < 0 ? null : path[(separator + 1)..];
	}
}
