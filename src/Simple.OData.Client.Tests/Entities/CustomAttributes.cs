namespace Simple.OData.Tests.Client;

[AttributeUsage(AttributeTargets.Property)]
public class NotMappedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class DataAttribute : Attribute
{
	public string Name { get; set; }
}

public class ColumnAttribute : DataAttribute
{
}
