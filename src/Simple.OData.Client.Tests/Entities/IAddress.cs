namespace Simple.OData.Tests.Client;

public interface IAddress
{
	AddressType Type { get; set; }

	string City { get; set; }

	string Region { get; set; }

	string PostalCode { get; set; }

	string Country { get; set; }
}
