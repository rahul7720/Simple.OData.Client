using Simple.OData.Client.TypeSystem;

namespace Simple.OData.Client.Tests.Unit.TypeSystem;

public class PropertyAccessorTests
{
	private sealed class Sample
	{
		public string? Name { get; set; }
		public int Count { get; private set; } = 3;
		public string ReadOnly => "ro";
	}

	[Fact]
	public void GetAndSetPublicProperty()
	{
		var accessor = new PropertyAccessor(typeof(Sample).GetProperty(nameof(Sample.Name))!, "Name");
		var instance = new Sample();

		accessor.SetValue(instance, "abc");

		accessor.GetValue(instance).Should().Be("abc");
		accessor.CanWrite.Should().BeTrue();
		accessor.PropertyType.Should().Be(typeof(string));
	}

	[Fact]
	public void SetPropertyWithPrivateSetter()
	{
		var accessor = new PropertyAccessor(typeof(Sample).GetProperty(nameof(Sample.Count))!, "Count");
		var instance = new Sample();

		accessor.CanWrite.Should().BeTrue();
		accessor.SetValue(instance, 42);

		instance.Count.Should().Be(42);
	}

	[Fact]
	public void ReadOnlyPropertyCannotBeWritten()
	{
		var accessor = new PropertyAccessor(typeof(Sample).GetProperty(nameof(Sample.ReadOnly))!, "ReadOnly");

		accessor.CanWrite.Should().BeFalse();
		accessor.GetValue(new Sample()).Should().Be("ro");
		var act = () => accessor.SetValue(new Sample(), "x");
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void MappedNameIsExposed()
	{
		var accessor = new PropertyAccessor(typeof(Sample).GetProperty(nameof(Sample.Name))!, "mapped_name");
		accessor.Name.Should().Be("Name");
		accessor.MappedName.Should().Be("mapped_name");
	}
}
