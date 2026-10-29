namespace Simple.OData.Tests.Client;

public class TypeWithIndexer
{
	public string Name { get; set; }

	public char this[int index] => Name[index];
}
