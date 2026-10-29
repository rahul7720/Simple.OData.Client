namespace Simple.OData.Client.Tests.Unit;

public class PluralizerTests
{
	private readonly SimplePluralizer _pluralizer = new();

	[Theory]
	[InlineData("Person", "Persons")]
	[InlineData("Day", "Days")]
	[InlineData("Dummy", "Dummies")]
	[InlineData("Access", "Accesses")]
	[InlineData("Life", "Lives")]
	[InlineData("Codex", "Codices")]
	[InlineData("Status", "Statuses")]
	public void PluralizeWord(string word, string expectedResult)
	{
		_pluralizer.Pluralize(word).Should().Be(expectedResult);
	}

	[Theory]
	[InlineData("Persons", "Person")]
	[InlineData("People", "Person")]
	[InlineData("Days", "Day")]
	[InlineData("Dummies", "Dummy")]
	[InlineData("Accesses", "Access")]
	[InlineData("Lives", "Life")]
	[InlineData("Codices", "Codex")]
	[InlineData("Statuses", "Status")]
	public void SingularizeWord(string word, string expectedResult)
	{
		_pluralizer.Singularize(word).Should().Be(expectedResult);
	}

	[Theory]
	[InlineData("Språk", "Språk")]
	[InlineData("Catalog_Контрагенты", "Catalog_Контрагенты")]
	public void PluralizeWordWithNonEnglishCharacters(string word, string expectedResult)
	{
		_pluralizer.Pluralize(word).Should().Be(expectedResult);
	}

	[Theory]
	[InlineData("Gårds", "Gårds")]
	[InlineData("Catalog_Контрагенты", "Catalog_Контрагенты")]
	public void SingularizeWordWithNonEnglishCharacters(string word, string expectedResult)
	{
		_pluralizer.Singularize(word).Should().Be(expectedResult);
	}

	[Fact]
	public void CachedPluralizerDelegatesAndCaches()
	{
		var pluralizer = Pluralizers.Cached;
		pluralizer.Pluralize("Category").Should().Be("Categories");
		pluralizer.Pluralize("Category").Should().Be("Categories");
		pluralizer.Singularize("Categories").Should().Be("Category");
	}

	[Fact]
	public void CustomPluralizerUsesDelegates()
	{
		IPluralizer pluralizer = new Pluralizer(w => w + "s", w => w.TrimEnd('s'));
		pluralizer.Pluralize("Car").Should().Be("Cars");
		pluralizer.Singularize("Cars").Should().Be("Car");
	}

	[Theory]
	[InlineData("Person", "person")]
	[InlineData("Day", "day")]
	[InlineData("Språk", "språk")]
	[InlineData("Person_123", "person123")]
	[InlineData("Språk_123", "språk123")]
	[InlineData("Catalog_Контрагенты_123", "catalogконтрагенты123")]
	public void HomogenizeWord(string word, string expectedResult)
	{
		word.Homogenize().Should().Be(expectedResult);
	}
}
