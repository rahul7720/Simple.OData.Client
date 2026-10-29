using Microsoft.Spatial;
using Simple.OData.Client.Tests.Entities;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public class ValueConversionTests
{
	private readonly TypeCache _typeCache = TestTypeCache.Create();

	[Theory]
	[InlineData(1, typeof(int), typeof(byte))]
	[InlineData(1, typeof(int), typeof(byte?))]
	[InlineData(1, typeof(int), typeof(short))]
	[InlineData(1, typeof(int), typeof(short?))]
	[InlineData(1, typeof(int), typeof(long))]
	[InlineData(1, typeof(int), typeof(long?))]
	[InlineData(1, typeof(int), typeof(decimal))]
	[InlineData(1, typeof(int), typeof(decimal?))]
	[InlineData(1, typeof(int), typeof(float))]
	[InlineData(1, typeof(int), typeof(float?))]
	[InlineData(1, typeof(int), typeof(double))]
	[InlineData(1, typeof(int), typeof(double?))]
	[InlineData(1, typeof(double), typeof(byte))]
	[InlineData(1, typeof(double), typeof(short?))]
	[InlineData(1, typeof(double), typeof(long))]
	[InlineData(1, typeof(double), typeof(decimal?))]
	[InlineData(1, typeof(double), typeof(float))]
	[InlineData("Utc", typeof(string), typeof(DateTimeKind))]
	[InlineData("Utc", typeof(string), typeof(DateTimeKind?))]
	[InlineData("2014-02-01T12:00:00.123", typeof(DateTimeOffset), typeof(DateTime))]
	[InlineData("2014-02-01T12:00:00.123", typeof(DateTimeOffset), typeof(DateTime?))]
	[InlineData("2014-02-01T12:00:00.123", typeof(DateTime), typeof(DateTimeOffset))]
	[InlineData("2014-02-01T12:00:00.123", typeof(DateTime), typeof(DateTimeOffset?))]
	[InlineData("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35", typeof(string), typeof(Guid))]
	[InlineData("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35", typeof(string), typeof(Guid?))]
	[InlineData("0", typeof(string), typeof(TestEnum))]
	[InlineData("Something", typeof(string), typeof(TestEnum))]
	[InlineData("5", typeof(string), typeof(TestEnum?))]
	[InlineData("Nothing", typeof(string), typeof(TestEnum?))]
	public void TryConvert_RoundTrips(object value, Type sourceType, Type targetType)
	{
		var sourceValue = ChangeType(value, sourceType);
		_typeCache.TryConvert(sourceValue, targetType, out var targetValue).Should().BeTrue();
		ChangeType(targetValue!, targetType).Should().Be(ChangeType(sourceValue, targetType));

		sourceValue = ChangeType(value, targetType);
		_typeCache.TryConvert(sourceValue, sourceType, out targetValue).Should().BeTrue();
		ChangeType(targetValue!, sourceType).Should().Be(ChangeType(sourceValue, sourceType));
	}

	[Fact]
	public void TryConvert_SameTypeIsReturnedAsIs()
	{
		var source = GeographyPoint.Create(10, 10);
		_typeCache.TryConvert(source, typeof(GeographyPoint), out var result).Should().BeTrue();
		result.Should().BeSameAs(source);
	}

	[Theory]
	[InlineData("2014-02-01", typeof(DateOnly), typeof(DateTime))]
	[InlineData("2014-02-01", typeof(DateOnly), typeof(DateTime?))]
	[InlineData("2014-02-01", typeof(DateOnly), typeof(DateTimeOffset))]
	[InlineData("2014-02-01T00:00:00", typeof(DateTime), typeof(DateOnly))]
	[InlineData("2014-02-01T00:00:00Z", typeof(DateTimeOffset), typeof(DateOnly))]
	public void TryConvert_DateOnly(object value, Type sourceType, Type targetType)
	{
		var sourceValue = ChangeType(value, sourceType);
		_typeCache.TryConvert(sourceValue, targetType, out var targetValue).Should().BeTrue();
		ChangeType(targetValue!, targetType).Should().Be(ChangeType(sourceValue, targetType));
	}

	[Fact]
	public void TryConvert_TimeOnlyAndTimeSpanAndDurationStrings()
	{
		var time = new TimeOnly(13, 45, 30);
		_typeCache.TryConvert(time, typeof(TimeSpan), out var span).Should().BeTrue();
		span.Should().Be(new TimeSpan(13, 45, 30));
		_typeCache.TryConvert(new TimeSpan(13, 45, 30), typeof(TimeOnly), out var only).Should().BeTrue();
		only.Should().Be(time);
		_typeCache.TryConvert("PT1H30M", typeof(TimeSpan), out var duration).Should().BeTrue();
		duration.Should().Be(new TimeSpan(1, 30, 0));
	}

	[Fact]
	public void TryConvert_StringToByteArrayAndBack()
	{
		var bytes = new byte[] { 1, 2, 3 };
		_typeCache.TryConvert(Convert.ToBase64String(bytes), typeof(byte[]), out var result).Should().BeTrue();
		result.Should().BeEquivalentTo(bytes);
		_typeCache.TryConvert(bytes, typeof(string), out var text).Should().BeTrue();
		text.Should().Be(Convert.ToBase64String(bytes));
	}

	[Theory]
	[InlineData("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35", typeof(Guid?), typeof(DateTime))]
	[InlineData("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35", typeof(Guid), typeof(DateTime?))]
	public void TryConvert_ImpossibleConversionFails(object value, Type sourceType, Type targetType)
	{
		var sourceValue = ChangeType(value, sourceType);
		_typeCache.TryConvert(sourceValue, targetType, out _).Should().BeFalse();
	}

	[Fact]
	public void TryConvert_NullToValueTypeGivesDefault_NullToReferenceGivesNull()
	{
		_typeCache.TryConvert(null, typeof(int), out var i).Should().BeTrue();
		i.Should().Be(0);
		_typeCache.TryConvert(null, typeof(int?), out var ni).Should().BeTrue();
		ni.Should().BeNull();
		_typeCache.TryConvert(null, typeof(string), out var s).Should().BeTrue();
		s.Should().BeNull();
	}

	[Fact]
	public void TryConvert_CustomType_WithRegisteredObjectConverter()
	{
		_typeCache.Converter.RegisterTypeConverter<PrimitiveType>((object c) => new PrimitiveType(new Guid(c.ToString()!)));

		var source = Guid.NewGuid();
		_typeCache.TryConvert(source, typeof(PrimitiveType), out var converted).Should().BeTrue();
		((PrimitiveType)converted!).Value.Should().Be(source);

		_typeCache.TryConvert((Guid?)source, typeof(PrimitiveType?), out converted).Should().BeTrue();
		((PrimitiveType)converted!).Value.Should().Be(source);
	}

	[Fact]
	public void TryConvert_CustomType_WithComponentModelTypeConverter()
	{
		var source = Guid.NewGuid();
		_typeCache.TryConvert(source, typeof(PrimitiveType), out var converted).Should().BeTrue();
		((PrimitiveType)converted!).Value.Should().Be(source);

		_typeCache.TryConvert((Guid?)source, typeof(PrimitiveType?), out converted).Should().BeTrue();
		((PrimitiveType)converted!).Value.Should().Be(source);
	}

	[Fact]
	public void Convert_ThrowsFormatExceptionWhenConversionIsImpossible()
	{
		var act = () => _typeCache.Convert(Guid.NewGuid(), typeof(DateTime));
		act.Should().Throw<FormatException>();
	}

	[Fact]
	public void ConvertGeneric_ConvertsValue()
	{
		_typeCache.Convert<long>(5).Should().Be(5L);
		_typeCache.Convert<string>(null).Should().BeNull();
	}

	private static object ChangeType(object value, Type targetType)
	{
		if (targetType == typeof(string))
		{
			return value.ToString()!;
		}

		if (targetType == typeof(DateTime))
		{
			return DateTime.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
		}

		if (targetType == typeof(DateTimeOffset))
		{
			return DateTimeOffset.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
		}

		if (targetType == typeof(DateOnly))
		{
			return value switch
			{
				DateTime dt => DateOnly.FromDateTime(dt),
				DateTimeOffset dto => DateOnly.FromDateTime(dto.DateTime),
				_ => DateOnly.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture),
			};
		}

		if (targetType.IsEnum)
		{
			return Enum.Parse(targetType, value.ToString()!, true);
		}

		if (targetType == typeof(Guid))
		{
			return new Guid(value.ToString()!);
		}

		if (targetType == typeof(PrimitiveType))
		{
			return new PrimitiveType(new Guid(value.ToString()!));
		}

		if (Nullable.GetUnderlyingType(targetType) is { } underlying)
		{
			return ChangeType(value, underlying);
		}

		return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
	}

	private enum TestEnum
	{
		Nothing = 0,
		Something = 5,
	}
}
