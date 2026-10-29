namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public class TypeConverterTests
{
	private sealed record Point(double X, double Y);

	[Fact]
	public void DictionaryConverter_IsUsedForRegisteredType()
	{
		var converter = new TypeConverter();
		converter.RegisterTypeConverter<Point>(d => new Point((double)d["X"]!, (double)d["Y"]!));

		converter.HasDictionaryConverter(typeof(Point)).Should().BeTrue();
		converter.HasObjectConverter(typeof(Point)).Should().BeFalse();
		converter.Convert<Point>(new Dictionary<string, object?> { ["X"] = 1d, ["Y"] = 2d }).Should().Be(new Point(1, 2));
	}

	[Fact]
	public void ObjectConverter_IsUsedForRegisteredType()
	{
		var converter = new TypeConverter();
		converter.RegisterTypeConverter<Guid>(o => Guid.Parse((string)o));

		converter.HasObjectConverter(typeof(Guid)).Should().BeTrue();
		converter.Convert<Guid>("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35").Should().Be(Guid.Parse("58D6C94D-B18A-43C9-AC1B-0B5A5BF10C35"));
	}

	[Fact]
	public void RegisteringTwiceReplacesTheConverter()
	{
		var converter = new TypeConverter();
		converter.RegisterTypeConverter<int>(o => 1);
		converter.RegisterTypeConverter<int>(o => 2);

		converter.Convert<int>("x").Should().Be(2);
	}

	[Fact]
	public void ConvertingUnregisteredTypeThrows()
	{
		var converter = new TypeConverter();
		var act = () => converter.Convert("x", typeof(Point));
		act.Should().Throw<InvalidOperationException>();
	}
}
