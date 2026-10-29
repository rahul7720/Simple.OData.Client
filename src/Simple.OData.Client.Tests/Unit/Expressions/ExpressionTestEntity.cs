using System.Text.Json.Serialization;
using Microsoft.Spatial;
using Simple.OData.Client.Tests.Entities;

namespace Simple.OData.Client.Tests.Unit.Expressions;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class DataAttribute : Attribute
{
	public string? Name { get; set; }
	public string? PropertyName { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class DataMemberAttribute : Attribute
{
	public string? Name { get; set; }
	public string? PropertyName { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class OtherAttribute : Attribute
{
	public string? Name { get; set; }
	public string? PropertyName { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class JsonPropertyAttribute(string propertyName) : Attribute
{
	public string PropertyName { get; } = propertyName;
}

internal sealed class TestEntity
{
	public int ProductID { get; set; }
	public string? ProductName { get; set; }
	public Guid LinkID { get; set; }
	public decimal Price { get; set; }
	public Address? Address { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTimeOffset Updated { get; set; }
	public DateOnly ShipDate { get; set; }
	public TimeSpan Period { get; set; }
	public TestEntity? Nested { get; set; }
	public TestEntity[]? Collection { get; set; }
	public string[]? Tags { get; set; }
	public GeographyPoint? Location { get; set; }

	[Column(Name = "Name")]
	public string? MappedNameUsingColumnAttribute { get; set; }

	[Data(Name = "Name", PropertyName = "OtherName")]
	public string? MappedNameUsingDataAttribute { get; set; }

	[DataMember(Name = "Name", PropertyName = "OtherName")]
	public string? MappedNameUsingDataMemberAttribute { get; set; }

	[Other(Name = "Name", PropertyName = "OtherName")]
	public string? MappedNameUsingOtherAttribute { get; set; }

	[DataMember(Name = "Name", PropertyName = "OtherName")]
	[Other(Name = "OtherName", PropertyName = "OtherName")]
	public string? MappedNameUsingDataMemberAndOtherAttribute { get; set; }

	[JsonProperty("Name")]
	public string? MappedNameUsingJsonPropertyAttribute { get; set; }

	[JsonPropertyName("Name")]
	public string? MappedNameUsingJsonPropertyNameAttribute { get; set; }
}
