namespace Simple.OData.Client.Tests.Unit;

public class NameMatchResolverTests
{
	[Theory]
	[InlineData("Products", "Products", true)]
	[InlineData("NorthwindModel.Products", "Products", true)]
	[InlineData("Products", "products", false)]
	[InlineData("Products", "Product", false)]
	[InlineData("Order_Details", "OrderDetails", false)]
	public void StrictMatchesExactNamesOnly(string actual, string requested, bool expected)
	{
		ODataNameMatchResolver.Strict.IsMatch(actual, requested).Should().Be(expected);
	}

	[Theory]
	[InlineData("Order_Details", "OrderDetails", true)]
	[InlineData("Order_Details", "orderdetails", true)]
	[InlineData("Products", "Product", false)]
	public void AlphanumericIgnoresPunctuationAndCase(string actual, string requested, bool expected)
	{
		ODataNameMatchResolver.Alphanumeric.IsMatch(actual, requested).Should().Be(expected);
		ODataNameMatchResolver.AlphanumericCaseInsensitive.IsMatch(actual, requested).Should().Be(expected);
	}

	[Theory]
	[InlineData("Products", "Product", true)]
	[InlineData("Product", "Products", true)]
	[InlineData("Categories", "Category", true)]
	[InlineData("Order_Details", "OrderDetail", true)]
	[InlineData("Products", "Customers", false)]
	public void NotStrictMatchesSingularAndPluralForms(string actual, string requested, bool expected)
	{
		ODataNameMatchResolver.NotStrict.IsMatch(actual, requested).Should().Be(expected);
	}

	[Fact]
	public void ResolversAreSingletons()
	{
		ODataNameMatchResolver.Strict.Should().BeSameAs(ODataNameMatchResolver.Strict);
		ODataNameMatchResolver.NotStrict.Should().BeOfType<BestMatchResolver>();
	}
}
