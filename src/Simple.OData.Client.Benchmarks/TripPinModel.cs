namespace Simple.OData.Client.Benchmarks;

public enum PersonGender
{
	Male,
	Female,
	Unknown,
}

public class City
{
	public string CountryRegion { get; set; }
	public string Name { get; set; }
	public string Region { get; set; }
}

public class Location
{
	public string Address { get; set; }
	public City City { get; set; }
}

public class Trip
{
	public int TripId { get; set; }
	public Guid ShareId { get; set; }
	public string Name { get; set; }
	public float Budget { get; set; }
	public string Description { get; set; }
	public IList<string> Tags { get; set; }
	public DateTimeOffset StartsAt { get; set; }
	public DateTimeOffset EndsAt { get; set; }
}

public class Person
{
	public string UserName { get; set; }
	public string FirstName { get; set; }
	public string LastName { get; set; }
	public string[] Emails { get; set; }
	public IList<Location> AddressInfo { get; set; }
	public PersonGender Gender { get; set; }
	public long Concurrency { get; set; }
	public IList<Person> Friends { get; set; }
	public IList<Trip> Trips { get; set; }
}
