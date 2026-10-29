using System.Linq.Expressions;

#pragma warning disable CA2225 // Operator overloads have named alternates: the expression tree API is operator based by design.
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations

namespace Simple.OData.Client;

public partial class ODataExpression
{
	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(bool value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(byte value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(sbyte value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(short value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(ushort value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(int value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(uint value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(long value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(ulong value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(float value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(double value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(decimal value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(DateTime value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(DateTimeOffset value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(DateOnly value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(TimeOnly value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(TimeSpan value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(Guid value) => FromValue(value);

	/// <summary>Wraps a constant.</summary>
	public static implicit operator ODataExpression(string? value) => FromValue(value);

	/// <summary><c>not</c></summary>
	public static ODataExpression operator !(ODataExpression expr) => new(expr, null, ExpressionType.Not);

	/// <summary>Negation (<c>-</c>).</summary>
	public static ODataExpression operator -(ODataExpression expr) => new(expr, null, ExpressionType.Negate);

	/// <summary><c>eq</c></summary>
	public static ODataExpression operator ==(ODataExpression? expr1, ODataExpression? expr2) => new(expr1, expr2, ExpressionType.Equal);

	/// <summary><c>ne</c></summary>
	public static ODataExpression operator !=(ODataExpression? expr1, ODataExpression? expr2) => new(expr1, expr2, ExpressionType.NotEqual);

	/// <summary><c>and</c></summary>
	public static ODataExpression operator &(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.And);

	/// <summary><c>or</c></summary>
	public static ODataExpression operator |(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Or);

	/// <summary><c>gt</c></summary>
	public static ODataExpression operator >(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.GreaterThan);

	/// <summary><c>lt</c></summary>
	public static ODataExpression operator <(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.LessThan);

	/// <summary><c>ge</c></summary>
	public static ODataExpression operator >=(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.GreaterThanOrEqual);

	/// <summary><c>le</c></summary>
	public static ODataExpression operator <=(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.LessThanOrEqual);

	/// <summary><c>add</c></summary>
	public static ODataExpression operator +(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Add);

	/// <summary><c>sub</c></summary>
	public static ODataExpression operator -(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Subtract);

	/// <summary><c>mul</c></summary>
	public static ODataExpression operator *(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Multiply);

	/// <summary><c>div</c></summary>
	public static ODataExpression operator /(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Divide);

	/// <summary><c>mod</c></summary>
	public static ODataExpression operator %(ODataExpression expr1, ODataExpression expr2) => new(expr1, expr2, ExpressionType.Modulo);

	/// <summary>Always false so that <c>&amp;&amp;</c> and <c>||</c> build expression nodes instead of short-circuiting.</summary>
	public static bool operator true(ODataExpression expr) => false;

	/// <summary>Always false so that <c>&amp;&amp;</c> and <c>||</c> build expression nodes instead of short-circuiting.</summary>
	public static bool operator false(ODataExpression expr) => false;
}

public sealed partial class ODataExpression<T>
{
	/// <summary><c>not</c></summary>
	public static ODataExpression<T> operator !(ODataExpression<T> expr) => new(new ODataExpression(expr, null, ExpressionType.Not));

	/// <summary><c>and</c></summary>
	public static ODataExpression<T> operator &(ODataExpression<T> expr1, ODataExpression<T> expr2) => new(new ODataExpression(expr1, expr2, ExpressionType.And));

	/// <summary><c>or</c></summary>
	public static ODataExpression<T> operator |(ODataExpression<T> expr1, ODataExpression<T> expr2) => new(new ODataExpression(expr1, expr2, ExpressionType.Or));

	/// <summary>Always false so that <c>&amp;&amp;</c> and <c>||</c> build expression nodes instead of short-circuiting.</summary>
	public static bool operator true(ODataExpression<T> expr) => false;

	/// <summary>Always false so that <c>&amp;&amp;</c> and <c>||</c> build expression nodes instead of short-circuiting.</summary>
	public static bool operator false(ODataExpression<T> expr) => false;
}
