using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Simple.OData.Client.TypeSystem;

/// <summary>
/// Compiles and caches constructor delegates.
/// </summary>
internal static class ExpressionActivator
{
	private static readonly ConcurrentDictionary<Type, Func<object>?> DefaultActivators = new();
	private static readonly ConcurrentDictionary<(Type Type, Type Parameter), Func<object, object>?> SingleArgumentActivators = new();

	/// <summary>Returns a delegate invoking the public parameterless constructor, or <see langword="null"/> when there is none.</summary>
	public static Func<object>? GetDefaultActivator(Type type) =>
		DefaultActivators.GetOrAdd(type, static t =>
		{
			if (t.IsAbstract || t.IsInterface)
			{
				return null;
			}

			if (t.IsValueType)
			{
				return Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(t), typeof(object))).Compile();
			}

			var constructor = t.GetConstructor(Type.EmptyTypes);
			return constructor is null
				? null
				: Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructor), typeof(object))).Compile();
		});

	/// <summary>Returns a delegate invoking the public constructor taking a single <paramref name="parameterType"/>, or <see langword="null"/>.</summary>
	public static Func<object, object>? GetActivator(Type type, Type parameterType) =>
		SingleArgumentActivators.GetOrAdd((type, parameterType), static key =>
		{
			var constructor = key.Type.GetConstructor([key.Parameter]);
			if (constructor is null)
			{
				return null;
			}

			var argument = Expression.Parameter(typeof(object), "argument");
			var body = Expression.Convert(Expression.New(constructor, Expression.Convert(argument, key.Parameter)), typeof(object));
			return Expression.Lambda<Func<object, object>>(body, argument).Compile();
		});
}
