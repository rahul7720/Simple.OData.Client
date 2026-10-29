using System.Linq.Expressions;
using Simple.OData.Client.Commands;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client.Aggregation;

/// <summary>A chain of <c>$apply</c> transformations; builders appended to it continue the pipeline.</summary>
internal abstract class DataAggregationBuilder : IApplyBuilder
{
	private DataAggregationBuilder? _next;

	protected List<IDataAggregationClause> Clauses { get; } = [];

	/// <inheritdoc />
	public string Build(ResolvedCommand command, Session session)
	{
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(session);
		var context = new ExpressionContext(session, null, null, command.DynamicPropertiesContainerName);
		var parts = new List<string>();
		for (var builder = this; builder is not null; builder = builder._next)
		{
			foreach (var clause in builder.Clauses)
			{
				var text = clause.Format(context);
				if (!string.IsNullOrEmpty(text))
				{
					parts.Add(text);
				}
			}
		}

		return string.Join('/', parts);
	}

	internal void Append(DataAggregationBuilder next)
	{
		var last = this;
		while (last._next is not null)
		{
			last = last._next;
		}

		last._next = next;
	}

	protected void AddFilter(string filter)
	{
		if (Clauses.LastOrDefault() is FilterClause existing)
		{
			existing.Append(filter);
		}
		else
		{
			Clauses.Add(new FilterClause(filter));
		}
	}

	protected void AddFilter(ODataExpression filter)
	{
		if (Clauses.LastOrDefault() is FilterClause existing)
		{
			existing.Append(filter);
		}
		else
		{
			Clauses.Add(new FilterClause(filter));
		}
	}
}

/// <inheritdoc cref="IDataAggregation{T}" />
internal sealed class DataAggregationBuilder<T> : DataAggregationBuilder, IDataAggregation<T>
	where T : class
{
	public IDataAggregation<T> Filter(Expression<Func<T, bool>> filter)
	{
		ArgumentNullException.ThrowIfNull(filter);
		AddFilter(ODataExpression.FromLinqExpression(filter));
		return this;
	}

	public IDataAggregation<TR> Aggregate<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> aggregation)
		where TR : class
	{
		ArgumentNullException.ThrowIfNull(aggregation);
		Clauses.Add(ExtractAggregation(aggregation.Body));
		return Next<TR>();
	}

	public IDataAggregation<TR> GroupBy<TR>(Expression<Func<T, IAggregationFunction<T>, TR>> groupBy)
		where TR : class
	{
		ArgumentNullException.ThrowIfNull(groupBy);
		List<ApplyColumn> columns = [];
		AggregationClauseCollection? aggregation = null;
		if (groupBy.Body is MemberExpression member)
		{
			columns.Add(new ApplyColumn(member));
		}
		else
		{
			aggregation = ExtractAggregation(groupBy.Body);
			ExtractGroupByColumns(groupBy.Body, columns);
		}

		Clauses.Add(new GroupByClause(columns, aggregation));
		return Next<TR>();
	}

	public IDataAggregation<TR> Compute<TR>(Expression<Func<T, TR>> compute)
		where TR : class
	{
		ArgumentNullException.ThrowIfNull(compute);
		Clauses.Add(new ComputeClause(ComputeExpression.Extract(compute)));
		return Next<TR>();
	}

	public IDataAggregation<T> TopCount(int count, Expression<Func<T, object>> propertyPath) => AddTopBottom(top: true, count, propertyPath);

	public IDataAggregation<T> BottomCount(int count, Expression<Func<T, object>> propertyPath) => AddTopBottom(top: false, count, propertyPath);

	private DataAggregationBuilder<T> AddTopBottom(bool top, int count, Expression<Func<T, object>> propertyPath)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
		ArgumentNullException.ThrowIfNull(propertyPath);
		Clauses.Add(new TopBottomCountClause(top, count, new ApplyColumn(propertyPath.Body)));
		return this;
	}

	private DataAggregationBuilder<TR> Next<TR>()
		where TR : class
	{
		var next = new DataAggregationBuilder<TR>();
		Append(next);
		return next;
	}

	private static AggregationClauseCollection ExtractAggregation(Expression body)
	{
		var collection = new AggregationClauseCollection();
		switch (body)
		{
			case NewExpression { Members: { } members } newExpression:
				for (var index = 0; index < Math.Min(members.Count, newExpression.Arguments.Count); index++)
				{
					AddAggregation(collection, members[index].Name, newExpression.Arguments[index]);
				}

				break;

			case MemberInitExpression memberInit:
				foreach (var assignment in memberInit.Bindings.OfType<MemberAssignment>())
				{
					AddAggregation(collection, assignment.Member.Name, assignment.Expression);
				}

				break;

			default:
				throw new ArgumentException("The expression body must be an anonymous type or an object initializer.", nameof(body));
		}

		return collection;
	}

	private static void AddAggregation(AggregationClauseCollection collection, string alias, Expression expression)
	{
		if (expression is MethodCallExpression { Method: { DeclaringType: { } declaringType } method } call && declaringType == typeof(IAggregationFunction<T>))
		{
			var column = call.Arguments.Count > 0 ? new ApplyColumn(call.Arguments[0]) : (ApplyColumn?)null;
			collection.Clauses.Add(new AggregationClause(alias, method.Name, column));
		}
	}

	private static void ExtractGroupByColumns(Expression body, List<ApplyColumn> columns)
	{
		switch (body)
		{
			case NewExpression newExpression:
				foreach (var argument in newExpression.Arguments)
				{
					ExtractGroupByColumns(argument, columns);
				}

				break;

			case MemberInitExpression memberInit:
				foreach (var assignment in memberInit.Bindings.OfType<MemberAssignment>())
				{
					ExtractGroupByColumns(assignment.Expression, columns);
				}

				break;

			case MemberExpression member:
				columns.Add(new ApplyColumn(member));
				break;

			case UnaryExpression { NodeType: ExpressionType.Convert } convert:
				ExtractGroupByColumns(convert.Operand, columns);
				break;

			default:
				// aggregation calls are collected separately; anything else is not a grouping property
				break;
		}
	}
}
