using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit;

public class SmokeTests
{
	[Fact]
	public void ResourcesAreEmbedded()
	{
		var metadata = Resources.Read("Northwind4.xml");
		metadata.TrimStart('﻿').Should().StartWith("<?xml");

		Resources.Read("OpenTypeV401.json").Length.Should().BeGreaterThan(0);
	}
}
