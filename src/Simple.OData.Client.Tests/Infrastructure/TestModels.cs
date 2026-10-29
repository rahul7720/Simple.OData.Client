namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>Small inline CSDL models for serialization tests.</summary>
internal static class TestModels
{
	public const string SerializationCsdl = """
		<edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
		  <edmx:DataServices>
		    <Schema Namespace="NS" xmlns="http://docs.oasis-open.org/odata/ns/edm">
		      <EnumType Name="Color"><Member Name="Red" Value="1"/><Member Name="Blue" Value="2"/></EnumType>
		      <ComplexType Name="Address"><Property Name="City" Type="Edm.String"/><Property Name="Zip" Type="Edm.String"/></ComplexType>
		      <EntityType Name="Thing">
		        <Key><PropertyRef Name="Id"/></Key>
		        <Property Name="Id" Type="Edm.Int32" Nullable="false"/>
		        <Property Name="Name" Type="Edm.String"/>
		        <Property Name="Tags" Type="Collection(Edm.String)"/>
		        <Property Name="Location" Type="NS.Address"/>
		        <Property Name="Addresses" Type="Collection(NS.Address)"/>
		        <Property Name="Color" Type="NS.Color"/>
		        <Property Name="When" Type="Edm.Date"/>
		        <Property Name="At" Type="Edm.TimeOfDay"/>
		        <Property Name="Span" Type="Edm.Duration"/>
		        <Property Name="Price" Type="Edm.Decimal"/>
		        <Property Name="Big" Type="Edm.Int64"/>
		        <NavigationProperty Name="Parent" Type="NS.Thing"/>
		        <NavigationProperty Name="Children" Type="Collection(NS.Thing)"/>
		      </EntityType>
		      <Action Name="Process">
		        <Parameter Name="Name" Type="Edm.String"/>
		        <Parameter Name="Count" Type="Edm.Int32"/>
		        <Parameter Name="Tags" Type="Collection(Edm.String)"/>
		        <Parameter Name="Item" Type="NS.Address"/>
		        <Parameter Name="Color" Type="NS.Color"/>
		        <Parameter Name="Thing" Type="NS.Thing"/>
		        <Parameter Name="Things" Type="Collection(NS.Thing)"/>
		        <ReturnType Type="NS.Thing"/>
		      </Action>
		      <Action Name="Reset" IsBound="true">
		        <Parameter Name="bindingParameter" Type="Collection(NS.Thing)"/>
		      </Action>
		      <Function Name="Top"><ReturnType Type="NS.Thing"/></Function>
		      <EntityContainer Name="Container">
		        <EntitySet Name="Things" EntityType="NS.Thing">
		          <NavigationPropertyBinding Path="Parent" Target="Things"/>
		          <NavigationPropertyBinding Path="Children" Target="Things"/>
		        </EntitySet>
		        <ActionImport Name="Process" Action="NS.Process"/>
		        <FunctionImport Name="Top" Function="NS.Top"/>
		      </EntityContainer>
		    </Schema>
		  </edmx:DataServices>
		</edmx:Edmx>
		""";

	public static Session SerializationSession(Action<ODataClientSettings>? configure = null)
	{
		var settings = new ODataClientSettings { BaseUri = MetadataClientFactory.UniqueBaseUri("serialization/"), MetadataDocument = SerializationCsdl };
		configure?.Invoke(settings);
		return new Session(settings);
	}
}
