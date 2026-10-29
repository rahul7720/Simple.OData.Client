using System.Linq.Expressions;
using System.Text;
using Simple.OData.Client.Expressions;

namespace Simple.OData.Client;

public partial class ODataExpression
{
	internal string Format(ExpressionContext context)
	{
		if (context.IsQueryOption && _operator is not (ExpressionType.Default or ExpressionType.And or ExpressionType.Equal))
		{
			throw new InvalidOperationException("Invalid custom query option.");
		}

		if (_operator == ExpressionType.Default && !IsValueConversion)
		{
			if (Reference is not null)
			{
				return FormatReference(context);
			}

			return Function is not null ? FormatFunction(context) : FormatValue(context);
		}

		if (IsValueConversion)
		{
			return FormatConversion(context);
		}

		if (_operator is ExpressionType.Not or ExpressionType.Negate)
		{
			var operand = FormatExpression(_left, context);
			var op = FormatOperator(context);
			return NeedsGrouping(_left) ? $"{op} ({operand})" : $"{op} {operand}";
		}

		var left = FormatExpression(_left, context);
		var right = FormatExpression(_right, context);
		var operatorText = FormatOperator(context);
		if (context.IsQueryOption)
		{
			return $"{left}{operatorText}{right}";
		}

		if (NeedsGrouping(_left))
		{
			left = $"({left})";
		}

		if (NeedsGrouping(_right))
		{
			right = $"({right})";
		}

		return $"{left} {operatorText} {right}";
	}

	private string FormatConversion(ExpressionContext context)
	{
		var expression = (ODataExpression)Value!;
		if (expression.Reference is null && expression.Function is null && !expression.IsValueConversion && expression._operator == ExpressionType.Default)
		{
			if (expression.Value is not null && expression.Value.GetType().IsEnum)
			{
				return FormatExpression(new ODataExpression(expression.Value), context);
			}

			if (_conversionType is not null && context.Session.TypeCache.TryConvert(expression.Value, _conversionType, out var converted))
			{
				return FormatExpression(new ODataExpression(converted), context);
			}
		}

		return FormatExpression(expression, context);
	}

	private static string FormatExpression(ODataExpression? expression, ExpressionContext context)
	{
		if (expression is null)
		{
			return string.IsNullOrEmpty(context.ScopeQualifier) ? "null" : context.ScopeQualifier;
		}

		return expression.Format(context);
	}

	private string FormatReference(ExpressionContext context)
	{
		if (_functionCaller is { Function.FunctionName: ODataLiteral.Cast })
		{
			return $"{_functionCaller.Format(context)}/{Reference!.Replace('.', '/')}";
		}

		var elementNames = Reference!.Split(PropertySeparators);
		var segmentNames = BuildReferencePath([], context.EntityCollection, elementNames, 0, context);
		return FormatScope(string.Join('/', segmentNames), context);
	}

	private string FormatFunction(ExpressionContext context)
	{
		var function = Function!;
		var operatorText = FunctionToOperatorMapping.TryFormat(context, _functionCaller, function);
		if (operatorText is not null)
		{
			return operatorText;
		}

		if (FunctionMapping.TryGet(function.FunctionName, function.ArgumentCount, out var mapping))
		{
			if (_functionCaller is null && function.ArgumentCount == 0)
			{
				return $"{mapping.MappedName}()";
			}

			var mapped = mapping.Map(_functionCaller, function.Arguments);
			var formattedArguments = string.Join(',', mapped.Arguments.Select(x => FormatExpression(x, context)));
			return $"{mapped.FunctionName}({formattedArguments})";
		}

		if (string.Equals(function.FunctionName, ODataLiteral.Any, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(function.FunctionName, ODataLiteral.All, StringComparison.OrdinalIgnoreCase))
		{
			return FormatAnyAllFunction(context);
		}

		if (string.Equals(function.FunctionName, ODataLiteral.IsOf, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(function.FunctionName, ODataLiteral.Cast, StringComparison.OrdinalIgnoreCase))
		{
			return FormatIsOfCastFunction(context);
		}

		if (string.Equals(function.FunctionName, "get_Item", StringComparison.Ordinal) && function.ArgumentCount == 1)
		{
			return FormatArrayIndexFunction(context);
		}

		if (string.Equals(function.FunctionName, nameof(Enum.HasFlag), StringComparison.Ordinal) && function.ArgumentCount == 1)
		{
			return FormatEnumHasFlagFunction(context);
		}

		if (string.Equals(function.FunctionName, nameof(ToString), StringComparison.Ordinal) && function.ArgumentCount == 0)
		{
			return FormatToStringFunction(context);
		}

		throw new NotSupportedException($"The function {function.FunctionName} is not supported or called with wrong number of arguments");
	}

	private string FormatAnyAllFunction(ExpressionContext context)
	{
		var function = Function!;
		var navigationPath = FormatCallerReference();
		EntityCollection? entityCollection = null;
		string formattedNavigationPath;
		if (context.EntityCollection is not null && context.Metadata.HasNavigationProperty(context.EntityCollection.Name, navigationPath))
		{
			entityCollection = context.Metadata.NavigateToCollection(context.EntityCollection, navigationPath);
			formattedNavigationPath = context.Metadata.GetNavigationPropertyExactName(context.EntityCollection.Name, navigationPath);
		}
		else if (context.EntityCollection is not null && context.Metadata.HasStructuralProperty(context.EntityCollection.Name, navigationPath))
		{
			// a collection of primitive or complex values
			entityCollection = context.EntityCollection;
			formattedNavigationPath = context.Metadata.GetStructuralPropertyExactName(context.EntityCollection.Name, navigationPath);
		}
		else
		{
			formattedNavigationPath = navigationPath;
		}

		string formattedArguments;
		if (function.ArgumentCount == 0 || function.Arguments[0] is null)
		{
			formattedArguments = string.Empty;
		}
		else
		{
			var variable = context.NextLambdaVariable();
			var nestedContext = context.Nested(entityCollection, variable);
			formattedArguments = $"{variable}:{FormatExpression(function.Arguments[0], nestedContext)}";
		}

		return FormatScope($"{formattedNavigationPath}/{function.FunctionName.ToLowerInvariant()}({formattedArguments})", context);
	}

	private string FormatIsOfCastFunction(ExpressionContext context)
	{
		var function = Function!;
		var builder = new StringBuilder();
		var target = function.Arguments[0];
		if (target is not null && !target.IsNull)
		{
			builder.Append(FormatExpression(target, context)).Append(',');
		}

		builder.Append(FormatExpression(function.Arguments[^1], context.Detached()));
		return $"{function.FunctionName.ToLowerInvariant()}({builder})";
	}

	private string FormatEnumHasFlagFunction(ExpressionContext context)
	{
		var value = FormatExpression(Function!.Arguments[0], context.Detached());
		return $"{FormatCallerReference()} has {value}";
	}

	private string FormatArrayIndexFunction(ExpressionContext context)
	{
		var propertyName = FormatExpression(Function!.Arguments[0], context.Detached()).Trim('\'');
		return _functionCaller!.Reference == context.DynamicPropertiesContainerName
			? propertyName
			: $"{FormatCallerReference()}/{propertyName}";
	}

	private string FormatToStringFunction(ExpressionContext context) =>
		_functionCaller!.Reference is not null ? _functionCaller.Format(context) : _functionCaller.FormatValue(context);

	private string FormatValue(ExpressionContext context)
	{
		if (Value is ODataExpression expression)
		{
			return expression.Format(context);
		}

		if (Value is Type type)
		{
			var typeName = context.Metadata.GetQualifiedTypeName(context.Session.TypeCache.GetMappedName(type));
			return UriLiteralFormatter.Format(context.Session, typeName, escapeDataString: false);
		}

		return UriLiteralFormatter.Format(context.Session, Value, escapeDataString: false);
	}

	private string FormatOperator(ExpressionContext context) => _operator switch
	{
		ExpressionType.And => context.IsQueryOption ? "&" : "and",
		ExpressionType.Or => "or",
		ExpressionType.Not => "not",
		ExpressionType.Equal => context.IsQueryOption ? "=" : "eq",
		ExpressionType.NotEqual => "ne",
		ExpressionType.GreaterThan => "gt",
		ExpressionType.GreaterThanOrEqual => "ge",
		ExpressionType.LessThan => "lt",
		ExpressionType.LessThanOrEqual => "le",
		ExpressionType.Add => "add",
		ExpressionType.Subtract => "sub",
		ExpressionType.Multiply => "mul",
		ExpressionType.Divide => "div",
		ExpressionType.Modulo => "mod",
		ExpressionType.Negate => "-",
		_ => throw new NotSupportedException($"Operator {_operator} is not supported."),
	};

	private List<string> BuildReferencePath(List<string> segmentNames, EntityCollection? entityCollection, string[] elementNames, int index, ExpressionContext context)
	{
		while (index < elementNames.Length)
		{
			var objectName = elementNames[index];
			if (entityCollection is not null)
			{
				var metadata = context.Metadata;
				if (metadata.HasStructuralProperty(entityCollection.Name, objectName))
				{
					segmentNames.Add(metadata.GetStructuralPropertyExactName(entityCollection.Name, objectName));
					entityCollection = null;
				}
				else if (metadata.HasNavigationProperty(entityCollection.Name, objectName))
				{
					segmentNames.Add(metadata.GetNavigationPropertyExactName(entityCollection.Name, objectName));
					var linkName = metadata.GetNavigationPropertyPartnerTypeName(entityCollection.Name, objectName);
					entityCollection = metadata.GetEntityCollection(linkName);
				}
				else if (FunctionMapping.TryGet(objectName, 0, out var mapping) && _functionCaller is not null)
				{
					segmentNames.Add($"{mapping.MappedName}({FormatCallerReference()})");
					entityCollection = null;
				}
				else if (metadata.IsOpenType(entityCollection.Name))
				{
					segmentNames.Add(objectName);
					entityCollection = null;
				}
				else
				{
					throw new UnresolvableObjectException(objectName, $"Invalid referenced object [{objectName}]");
				}
			}
			else if (FunctionMapping.TryGet(objectName, 0, out var mapping) && _functionCaller is not null && index == 0)
			{
				segmentNames.Add($"{mapping.MappedName}({FormatCallerReference()})");
			}
			else
			{
				segmentNames.Add(objectName);
			}

			index++;
		}

		return segmentNames;
	}

	private string FormatCallerReference() => _functionCaller!.Reference!.Replace('.', '/');

	private static int GetPrecedence(ExpressionType op) => op switch
	{
		ExpressionType.Not or ExpressionType.Negate => 1,
		ExpressionType.Modulo or ExpressionType.Multiply or ExpressionType.Divide => 2,
		ExpressionType.Add or ExpressionType.Subtract => 3,
		ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual => 4,
		ExpressionType.Equal or ExpressionType.NotEqual => 5,
		ExpressionType.And => 6,
		ExpressionType.Or => 7,
		_ => 0,
	};

	private bool NeedsGrouping(ODataExpression? expression)
	{
		if (_operator == ExpressionType.Default || expression is null || expression._operator == ExpressionType.Default)
		{
			return false;
		}

		return GetPrecedence(_operator) < GetPrecedence(expression._operator);
	}

	private static string FormatScope(string text, ExpressionContext context) =>
		string.IsNullOrEmpty(context.ScopeQualifier) ? text : $"{context.ScopeQualifier}/{text}";
}
