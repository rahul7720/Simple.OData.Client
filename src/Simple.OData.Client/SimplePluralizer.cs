using System.Collections.Frozen;

namespace Simple.OData.Client;

// Based on a gist by Juliën Hanssens: https://gist.github.com/hanssens/2835960

/// <summary>
/// A rule-based English pluralizer. Words containing non-ASCII characters are returned unchanged.
/// </summary>
internal sealed class SimplePluralizer : IPluralizer
{
	private sealed record Word(string Singular, string Plural);

	private readonly record struct SuffixRule(string SingularSuffix, string PluralSuffix)
	{
		public bool TryToPlural(string word, out string plural)
		{
			if (word.EndsWith(SingularSuffix, StringComparison.OrdinalIgnoreCase))
			{
				plural = string.Concat(word.AsSpan(0, word.Length - SingularSuffix.Length), PluralSuffix);
				return true;
			}

			plural = string.Empty;
			return false;
		}

		public bool TryToSingular(string word, out string singular)
		{
			if (word.EndsWith(PluralSuffix, StringComparison.OrdinalIgnoreCase))
			{
				singular = string.Concat(word.AsSpan(0, word.Length - PluralSuffix.Length), SingularSuffix);
				return true;
			}

			singular = string.Empty;
			return false;
		}
	}

	private static readonly (string Singular, string Plural, string AlternatePlural)[] SpecialWords =
	[
		("agendum", "agenda", ""), ("albino", "albinos", ""), ("alga", "algae", ""), ("alumna", "alumnae", ""),
		("apex", "apices", "apexes"), ("archipelago", "archipelagos", ""), ("bacterium", "bacteria", ""),
		("beef", "beefs", "beeves"), ("bison", "", ""), ("brother", "brothers", "brethren"),
		("candelabrum", "candelabra", ""), ("carp", "", ""), ("casino", "casinos", ""), ("child", "children", ""),
		("chassis", "", ""), ("chinese", "", ""), ("clippers", "", ""), ("cod", "", ""), ("codex", "codices", ""),
		("commando", "commandos", ""), ("corps", "", ""), ("cortex", "cortices", "cortexes"), ("cow", "cows", "kine"),
		("criterion", "criteria", ""), ("datum", "data", ""), ("debris", "", ""), ("diabetes", "", ""),
		("ditto", "dittos", ""), ("djinn", "", ""), ("dynamo", "", ""), ("elk", "", ""), ("embryo", "embryos", ""),
		("ephemeris", "ephemeris", "ephemerides"), ("erratum", "errata", ""), ("extremum", "extrema", ""),
		("fiasco", "fiascos", ""), ("fish", "fishes", "fish"), ("flounder", "", ""), ("focus", "focuses", "foci"),
		("fungus", "fungi", "funguses"), ("gallows", "", ""), ("genie", "genies", "genii"), ("ghetto", "ghettos", ""),
		("graffiti", "", ""), ("headquarters", "", ""), ("herpes", "", ""), ("homework", "", ""),
		("index", "indices", "indexes"), ("inferno", "infernos", ""), ("japanese", "", ""), ("jumbo", "jumbos", ""),
		("latex", "latices", "latexes"), ("lingo", "lingos", ""), ("mackerel", "", ""), ("macro", "macros", ""),
		("manifesto", "manifestos", ""), ("measles", "", ""), ("money", "moneys", "monies"),
		("mongoose", "mongooses", "mongoose"), ("mumps", "", ""), ("murex", "murecis", ""), ("mythos", "mythos", "mythoi"),
		("news", "", ""), ("octopus", "octopuses", "octopodes"), ("ovum", "ova", ""), ("ox", "ox", "oxen"),
		("person", "persons", "people"), ("photo", "photos", ""), ("pincers", "", ""), ("pliers", "", ""),
		("pro", "pros", ""), ("rabies", "", ""), ("radius", "radiuses", "radii"), ("rhino", "rhinos", ""),
		("salmon", "", ""), ("scissors", "", ""), ("series", "", ""), ("shears", "", ""), ("silex", "silices", ""),
		("simplex", "simplices", "simplexes"), ("soliloquy", "soliloquies", "soliloquy"), ("species", "", ""),
		("status", "statuses", ""), ("stratum", "strata", ""), ("swine", "", ""), ("trout", "", ""), ("tuna", "", ""),
		("vertebra", "vertebrae", ""), ("vertex", "vertices", "vertexes"), ("vortex", "vortices", "vortexes"),
	];

	private static readonly SuffixRule[] SuffixRules =
	[
		new("ch", "ches"), new("sh", "shes"), new("ss", "sses"), new("ay", "ays"), new("ey", "eys"), new("iy", "iys"),
		new("oy", "oys"), new("uy", "uys"), new("y", "ies"), new("ao", "aos"), new("eo", "eos"), new("io", "ios"),
		new("oo", "oos"), new("uo", "uos"), new("o", "oes"), new("cis", "ces"), new("sis", "ses"), new("xis", "xes"),
		new("louse", "lice"), new("mouse", "mice"), new("zoon", "zoa"), new("man", "men"), new("deer", "deer"),
		new("fish", "fish"), new("sheep", "sheep"), new("itis", "itis"), new("ois", "ois"), new("pox", "pox"),
		new("ox", "oxes"), new("foot", "feet"), new("goose", "geese"), new("tooth", "teeth"), new("alf", "alves"),
		new("elf", "elves"), new("olf", "olves"), new("arf", "arves"), new("leaf", "leaves"), new("nife", "nives"),
		new("life", "lives"), new("wife", "wives"),
	];

	private static readonly FrozenDictionary<string, Word> SpecialSingulars;
	private static readonly FrozenDictionary<string, Word> SpecialPlurals;

	static SimplePluralizer()
	{
		var singulars = new Dictionary<string, Word>(StringComparer.OrdinalIgnoreCase);
		var plurals = new Dictionary<string, Word>(StringComparer.OrdinalIgnoreCase);
		foreach (var (singular, plural, alternatePlural) in SpecialWords)
		{
			var word = new Word(singular, string.IsNullOrEmpty(plural) ? singular : plural);
			singulars.Add(singular, word);
			plurals.Add(word.Plural, word);
			if (!string.IsNullOrEmpty(alternatePlural))
			{
				plurals.Add(alternatePlural, word);
			}
		}

		SpecialSingulars = singulars.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		SpecialPlurals = plurals.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public string Pluralize(string word)
	{
		ArgumentNullException.ThrowIfNull(word);
		return AdjustCase(ToPluralCore(word), word);
	}

	/// <inheritdoc />
	public string Singularize(string word)
	{
		ArgumentNullException.ThrowIfNull(word);
		return AdjustCase(ToSingularCore(word), word);
	}

	private static bool IsAscii(string s) => System.Text.Ascii.IsValid(s);

	private static string ToPluralCore(string s)
	{
		if (s.Length == 0 || !IsAscii(s))
		{
			return s;
		}

		if (SpecialSingulars.TryGetValue(s, out var word))
		{
			return word.Plural;
		}

		foreach (var rule in SuffixRules)
		{
			if (rule.TryToPlural(s, out var plural))
			{
				return plural;
			}
		}

		return s + "s";
	}

	private static string ToSingularCore(string s)
	{
		if (s.Length == 0 || !IsAscii(s))
		{
			return s;
		}

		if (SpecialPlurals.TryGetValue(s, out var word))
		{
			return word.Singular;
		}

		foreach (var rule in SuffixRules)
		{
			if (rule.TryToSingular(s, out var singular))
			{
				return singular;
			}
		}

		return s.EndsWith('s') || s.EndsWith('S') ? s[..^1] : s;
	}

	private static string AdjustCase(string s, string template)
	{
		if (s.Length == 0)
		{
			return s;
		}

		var foundUpperOrLower = false;
		var allLower = true;
		var allUpper = true;
		var firstUpper = false;
		for (var i = 0; i < template.Length; i++)
		{
			if (char.IsUpper(template[i]))
			{
				if (i == 0)
				{
					firstUpper = true;
				}

				allLower = false;
				foundUpperOrLower = true;
			}
			else if (char.IsLower(template[i]))
			{
				allUpper = false;
				foundUpperOrLower = true;
			}
		}

		if (!foundUpperOrLower)
		{
			return s;
		}

		if (allLower)
		{
			return s.ToLowerInvariant();
		}

		if (allUpper)
		{
			return s.ToUpperInvariant();
		}

		if (firstUpper && !char.IsUpper(s[0]))
		{
			return string.Concat(char.ToUpperInvariant(s[0]).ToString(), s.AsSpan(1));
		}

		return s;
	}
}
