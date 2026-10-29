using System.Text.Json;
using Simple.OData.Client.Serialization;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Serialization;

public sealed class RequestWriterActionTests
{
	private static async Task<JsonElement> JsonAsync(ODataRequest request) =>
		JsonDocument.Parse(await request.RequestMessage.Content!.ReadAsStringAsync()).RootElement;

	[Fact]
	public async Task ActionWithDerivedEntityParameterWritesTypeAndBinding()
	{
		var writer = new RequestWriter(MetadataClientFactory.CreateSession("ActionWithDerivedType.xml"), null);
		var request = await writer.CreateActionRequestAsync("CancelSalesOrder", "CancelSalesOrder", null, new Dictionary<string, object?>
		{
			["Status"] = 936140011,
			["OrderClose"] = new Dictionary<string, object?>
			{
				["salesorderid"] = new Dictionary<string, object?> { ["salesorderid"] = new Guid("5AEEC23F-DB4F-E911-A978-000D3A3611E1") },
				["subject"] = "Close sales order",
				["@odata.type"] = "Microsoft.Dynamics.CRM.orderclose",
			},
		}, true);
		request.Method.Should().Be("POST");
		var json = await JsonAsync(request);
		json.GetProperty("Status").GetInt32().Should().Be(936140011);
		var orderClose = json.GetProperty("OrderClose");
		orderClose.GetProperty("@odata.type").GetString().Should().Be("#Microsoft.Dynamics.CRM.orderclose");
		orderClose.GetProperty("subject").GetString().Should().Be("Close sales order");
		// the CRM metadata declares no entity sets, so the reference falls back to the entity type name
		orderClose.GetProperty("salesorderid@odata.bind").GetString().Should().EndWith("/salesorder(5aeec23f-db4f-e911-a978-000d3a3611e1)");
	}

	[Fact]
	public async Task UnboundActionWritesEveryParameterKind()
	{
		var writer = new RequestWriter(TestModels.SerializationSession(), null);
		var request = await writer.CreateActionRequestAsync("Process", "Process", null, new Dictionary<string, object?>
		{
			["Name"] = "x",
			["Count"] = 3,
			["Tags"] = new List<string> { "a", "b" },
			["Item"] = new { City = "Berlin", Zip = "10115" },
			["Color"] = "Red",
			["Thing"] = new { Id = 1, Name = "t", Location = new { City = "Rome" } },
			["Things"] = new object[] { new { Id = 2 }, new Dictionary<string, object?> { ["Id"] = 3, ["Name"] = "three" } },
		}, true);
		request.Method.Should().Be("POST");
		request.ResultRequired.Should().BeTrue();
		var json = await JsonAsync(request);
		json.GetProperty("Name").GetString().Should().Be("x");
		json.GetProperty("Count").GetInt32().Should().Be(3);
		json.GetProperty("Tags").EnumerateArray().Select(x => x.GetString()).Should().Equal("a", "b");
		json.GetProperty("Item").GetProperty("City").GetString().Should().Be("Berlin");
		json.GetProperty("Color").GetString().Should().Be("Red");
		json.GetProperty("Thing").GetProperty("Id").GetInt32().Should().Be(1);
		json.GetProperty("Thing").GetProperty("Location").GetProperty("City").GetString().Should().Be("Rome");
		json.GetProperty("Things").GetArrayLength().Should().Be(2);
		json.GetProperty("Things")[1].GetProperty("Name").GetString().Should().Be("three");
	}

	[Fact]
	public async Task ActionParameterNamesAreResolvedAndUnknownOnesRejected()
	{
		var writer = new RequestWriter(TestModels.SerializationSession(), null);
		var json = await JsonAsync(await writer.CreateActionRequestAsync("Process", "Process", null, new Dictionary<string, object?> { ["name"] = "x" }, false));
		json.GetProperty("Name").GetString().Should().Be("x");

		var act = () => writer.CreateActionRequestAsync("Process", "Process", null, new Dictionary<string, object?> { ["Nope"] = 1 }, false);
		await act.Should().ThrowAsync<UnresolvableObjectException>();
	}
}
