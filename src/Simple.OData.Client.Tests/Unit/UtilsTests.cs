namespace Simple.OData.Client.Tests.Unit;

public class UtilsTests
{
	[Theory]
	[InlineData("http://company.com", "", "http://company.com/")]
	[InlineData("http://company.com", null, "http://company.com/")]
	[InlineData("http://company.com", "$metadata", "http://company.com/$metadata")]
	[InlineData("http://company.com/", "$metadata", "http://company.com/$metadata")]
	[InlineData("https://company.com", "$metadata", "https://company.com/$metadata")]
	[InlineData("http://company.com?client=100", "$metadata", "http://company.com/$metadata?client=100")]
	[InlineData("http://company.com/feed?client=100", "$metadata", "http://company.com/feed/$metadata?client=100")]
	[InlineData("http://company.com", "Products", "http://company.com/Products")]
	[InlineData("http://company.com/", "Products", "http://company.com/Products")]
	[InlineData("http://company.com/feed", "Products", "http://company.com/feed/Products")]
	[InlineData("http://company.com", "Products?$filter=Id%20eq%201", "http://company.com/Products?$filter=Id%20eq%201")]
	[InlineData("http://company.com/feed", "Products?$filter=Id%20eq%201", "http://company.com/feed/Products?$filter=Id%20eq%201")]
	[InlineData("http://company.com?client=100", "Products?$filter=Id%20eq%201", "http://company.com/Products?$filter=Id%20eq%201&client=100")]
	[InlineData("http://company.com/feed?client=100", "Products?$filter=Id%20eq%201", "http://company.com/feed/Products?$filter=Id%20eq%201&client=100")]
	public void CreateAbsoluteUri(string baseUri, string? relativePath, string expected)
	{
		var actual = Utils.CreateAbsoluteUri(new Uri(baseUri), relativePath);

		actual.AbsoluteUri.Should().Be(expected);
	}

	[Fact]
	public void CreateAbsoluteUri_KeepsBasePathAndMergesQuery()
	{
		var actual = Utils.CreateAbsoluteUri(new Uri("https://host/svc/v2/?sap-client=100"), "Products?$top=1");

		actual.AbsoluteUri.Should().Be("https://host/svc/v2/Products?$top=1&sap-client=100");
	}

	[Fact]
	public void CreateAbsoluteUri_ReturnsAbsoluteRelativePathUnchanged()
	{
		var actual = Utils.CreateAbsoluteUri(new Uri("https://host/svc/"), "https://other/svc/Products?$skiptoken=5");

		actual.AbsoluteUri.Should().Be("https://other/svc/Products?$skiptoken=5");
	}

	[Theory]
	[InlineData("Products", "Products")]
	[InlineData("Products(1)", "Products")]
	[InlineData("Products(1)/Category", "Products")]
	[InlineData("Products?$filter=Id eq 1", "Products")]
	[InlineData("http://localhost/odata/Products(1)/Category", "Category")]
	public void ExtractCollectionName(string commandText, string expected)
	{
		Utils.ExtractCollectionName(commandText).Should().Be(expected);
	}

	[Fact]
	public void EnsureStartsWith_PrefixesOnlyWhenMissing()
	{
		"bar".EnsureStartsWith("foo").Should().Be("foobar");
		"foobar".EnsureStartsWith("foo").Should().Be("foobar");
	}

	[Fact]
	public void BestMatch_PrefersStrictMatchOverPluralizedMatch()
	{
		var names = new[] { "Product", "Products" };

		names.BestMatch(x => x, "Products", ODataNameMatchResolver.NotStrict).Should().Be("Products");
		names.BestMatch(x => x, "Product", ODataNameMatchResolver.NotStrict).Should().Be("Product");
		names.BestMatch(x => x, "Customers", ODataNameMatchResolver.NotStrict).Should().BeNull();
	}
}
