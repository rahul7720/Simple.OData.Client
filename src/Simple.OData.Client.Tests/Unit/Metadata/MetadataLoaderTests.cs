using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Validation;
using Simple.OData.Client.Metadata;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Metadata;

public class MetadataLoaderTests
{
	private const string CsdlJson = """
		{"$Version":"4.01","NS":{"P":{"$Kind":"EntityType","$Key":["Id"],"Id":{"$Type":"Edm.Int32"}},"C":{"$Kind":"EntityContainer","Ps":{"$Collection":true,"$Type":"NS.P"}}},"$EntityContainer":"NS.C"}
		""";

	private const string EdmxV1 = """
		<edmx:Edmx Version="1.0" xmlns:edmx="http://schemas.microsoft.com/ado/2007/06/edmx"><edmx:DataServices m:DataServiceVersion="3.0" xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"><Schema Namespace="NS" xmlns="http://schemas.microsoft.com/ado/2009/11/edm" /></edmx:DataServices></edmx:Edmx>
		""";

	[Fact]
	public void BuiltInVocabularyReferencesResolve()
	{
		// TripPin references the Core vocabulary through edmx:Reference; its terms resolve without any extra download
		var model = MetadataLoader.Parse(Resources.Read("TripPin.xml"));
		var computed = model.VocabularyAnnotations.Select(x => x.Term).First(x => x.FullName() == "Org.OData.Core.V1.Computed");
		computed.IsBad().Should().BeFalse();
		computed.Type.FullName().Should().Be("Org.OData.Core.V1.Tag", "the term type comes from the referenced Core vocabulary");
	}

	[Fact]
	public void ParsesCsdlXml()
	{
		var document = Resources.Read("Northwind4.xml");

		MetadataLoader.Detect(document).Should().Be(MetadataFormat.Xml);
		MetadataLoader.GetVersion(document).Should().Be("4.0");
		var model = MetadataLoader.Parse(document);
		model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
		model.FindDeclaredType("NorthwindModel.Product").Should().BeAssignableTo<IEdmEntityType>();
	}

	[Fact]
	public void ParsesCsdlJson()
	{
		MetadataLoader.Detect(CsdlJson).Should().Be(MetadataFormat.Json);
		MetadataLoader.GetVersion(CsdlJson).Should().Be("4.01");
		var model = MetadataLoader.Parse(CsdlJson);
		model.EntityContainer.FindEntitySet("Ps").Should().NotBeNull();
	}

	[Fact]
	public void ParsesWithLeadingByteOrderMarkAndWhitespace()
	{
		var document = "﻿\n  " + Resources.Read("TripPin.xml");
		MetadataLoader.Parse(document).EntityContainer.FindSingleton("Me").Should().NotBeNull();
	}

	[Fact]
	public void RejectsPreV4Documents()
	{
		var act = () => MetadataLoader.Parse(EdmxV1);
		act.Should().Throw<InvalidOperationException>().WithMessage("*1.0*");
	}

	[Fact]
	public void RejectsGarbage()
	{
		var act = () => MetadataLoader.Parse("not metadata");
		act.Should().Throw<InvalidOperationException>();
	}
}
