using System.Net;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class DeltaReaderTests
{
	[Fact]
	public async Task ReadsDeltaPayload()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml");
		var b = session.BaseUri;
		var body = $$$"""
			{"@odata.context":"{{{b}}}$metadata#Products/$delta","@odata.deltaLink":"{{{b}}}Products?$deltatoken=123","value":[
			 {"@odata.id":"{{{b}}}Products(1)","ProductID":1,"ProductName":"Changed"},
			 {"@odata.context":"{{{b}}}$metadata#Products/$deletedEntity","id":"{{{b}}}Products(2)","reason":"deleted"},
			 {"@odata.context":"{{{b}}}$metadata#Products/$link","source":"{{{b}}}Products(1)","relationship":"Category","target":"{{{b}}}Categories(3)"},
			 {"@odata.context":"{{{b}}}$metadata#Products/$deletedLink","source":"{{{b}}}Products(1)","relationship":"Category","target":"{{{b}}}Categories(2)"}
			]}
			""";
		var delta = await new ResponseReader(session).ReadDeltaAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.OK, body), null, CancellationToken.None);
		delta.Changed.Should().ContainSingle().Which["ProductName"].Should().Be("Changed");
		delta.Deleted.Should().ContainSingle();
		delta.Deleted[0].Id.Should().EndWith("Products(2)");
		delta.Deleted[0].Reason.Should().Be("deleted");
		delta.AddedLinks.Should().ContainSingle().Which.Relationship.Should().Be("Category");
		delta.AddedLinks[0].Target.AbsoluteUri.Should().EndWith("Categories(3)");
		delta.DeletedLinks.Should().ContainSingle().Which.Target.AbsoluteUri.Should().EndWith("Categories(2)");
		delta.DeltaLink!.Query.Should().Contain("$deltatoken=123");
		delta.NextPageLink.Should().BeNull();
	}

	[Fact]
	public async Task ReadsFourOhOneDeltaPayload()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml", s => s.ProtocolVersion = ODataProtocolVersion.V401);
		var b = session.BaseUri;
		var body = $$$"""
			{"@context":"{{{b}}}$metadata#Products/$delta","@deltaLink":"{{{b}}}Products?$deltatoken=9","value":[
			 {"ProductID":1,"ProductName":"Changed"},
			 {"@removed":{"reason":"changed"},"@id":"{{{b}}}Products(2)","ProductID":2}
			]}
			""";
		var delta = await new ResponseReader(session).ReadDeltaAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.OK, body), new EntityCollection("Products"), CancellationToken.None);
		delta.Changed.Should().ContainSingle().Which["ProductID"].Should().Be(1);
		delta.Deleted.Should().ContainSingle();
		delta.Deleted[0].Reason.Should().Be("changed");
		delta.Deleted[0].Id.Should().EndWith("Products(2)");
		delta.DeltaLink!.Query.Should().Contain("$deltatoken=9");
	}

	[Fact]
	public async Task GenericReadIgnoresDeletedEntriesButKeepsDeltaLink()
	{
		var session = MetadataClientFactory.CreateSession("Northwind4.xml");
		var b = session.BaseUri;
		var body = $$$"""{"@odata.context":"{{{b}}}$metadata#Products/$delta","@odata.deltaLink":"{{{b}}}Products?$deltatoken=1","value":[{"ProductID":1},{"@odata.context":"{{{b}}}$metadata#Products/$deletedEntity","id":"{{{b}}}Products(2)","reason":"deleted"}]}""";
		var response = await new ResponseReader(session).ReadAsync(FakeHttpHandler.CreateResponse(HttpStatusCode.OK, body), null, CancellationToken.None);
		response.AsEntries(false).Should().ContainSingle();
		response.Feed!.Annotations!.DeltaLink.Should().NotBeNull();
	}
}
