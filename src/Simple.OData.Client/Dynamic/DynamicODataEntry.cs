using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Simple.OData.Client;

/// <summary>
/// An <see cref="ODataEntry"/> whose properties are accessed through C# <see langword="dynamic"/>; nested entries
/// are returned as <see cref="DynamicODataEntry"/> instances and the entry converts to any CLR type on assignment.
/// </summary>
public sealed class DynamicODataEntry : ODataEntry, IDynamicMetaObjectProvider
{
	private static readonly MethodInfo GetEntryValueMethod = Method(nameof(GetEntryValue));
	private static readonly MethodInfo ConvertToMethod = Method(nameof(ConvertTo));

	private readonly ITypeCache _typeCache;

	internal DynamicODataEntry(IDictionary<string, object?> entry, ITypeCache typeCache)
		: base(entry)
	{
		_typeCache = typeCache ?? throw new ArgumentNullException(nameof(typeCache));
	}

	/// <inheritdoc />
	public DynamicMetaObject GetMetaObject(Expression parameter) => new MetaObject(parameter, this);

	private static MethodInfo Method(string name) =>
		typeof(DynamicODataEntry).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new MissingMethodException(nameof(DynamicODataEntry), name);

	private object? GetEntryValue(string propertyName) => TryGetValue(propertyName, out var value) ? Wrap(value) : null;

	private object? ConvertTo(Type type) => _typeCache.ToObject(AsDictionary(), type);

	private object? Wrap(object? value) => value switch
	{
		IDictionary<string, object?> entry => new DynamicODataEntry(entry, _typeCache),
		string or byte[] => value,
		IEnumerable<object?> items => items.Select(Wrap).ToList(),
		_ => value,
	};

	private sealed class MetaObject(Expression parameter, DynamicODataEntry value) : DynamicMetaObject(parameter, BindingRestrictions.Empty, value)
	{
		private Expression Self => Expression.Convert(Expression, LimitType);

		private BindingRestrictions TypeRestrictions => BindingRestrictions.GetTypeRestriction(Expression, LimitType);

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder) =>
			new(Expression.Call(Self, GetEntryValueMethod, Expression.Constant(binder.Name)), TypeRestrictions);

		public override DynamicMetaObject BindConvert(ConvertBinder binder)
		{
			if (binder.Type.IsAssignableFrom(typeof(DynamicODataEntry)))
			{
				return new DynamicMetaObject(Expression.Convert(Self, binder.Type), TypeRestrictions);
			}

			var converted = Expression.Call(Self, ConvertToMethod, Expression.Constant(binder.Type, typeof(Type)));
			return new DynamicMetaObject(Expression.Convert(converted, binder.Type), TypeRestrictions);
		}
	}
}
