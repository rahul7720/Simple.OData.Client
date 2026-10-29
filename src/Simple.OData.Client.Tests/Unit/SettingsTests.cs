using Simple.OData.Client.Tests.Entities;

namespace Simple.OData.Client.Tests.Unit;

public class SettingsTests
{
	[Fact]
	public void DefaultCtorAndBaseUri()
	{
		var settings = new ODataClientSettings { BaseUri = new Uri("http://localhost") };
		settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/");
	}

	[Fact]
	public void CtorWithBaseUri()
	{
		var settings = new ODataClientSettings(new Uri("http://localhost"));
		settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/");
	}

	[Fact]
	public void CtorWithHttpClient_HasNoBaseUri()
	{
		var settings = new ODataClientSettings(new HttpClient());
		settings.BaseUri.Should().BeNull();
	}

	[Fact]
	public void CtorWithHttpClientNoBaseAddressAndBaseUri()
	{
		var settings = new ODataClientSettings(new HttpClient()) { BaseUri = new Uri("http://localhost") };
		settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/");
	}

	[Fact]
	public void CtorWithHttpClientNoBaseAddressAndRelativeUri_Throws()
	{
		var act = () => new ODataClientSettings(new HttpClient(), new Uri("api", UriKind.Relative));
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void CtorWithHttpClientAndBaseAddress()
	{
		var settings = new ODataClientSettings(new HttpClient { BaseAddress = new Uri("http://localhost") });
		settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/");
	}

	[Fact]
	public void CtorWithHttpClientAndRelativeBaseAddress_Throws()
	{
		var act = () => new ODataClientSettings(new HttpClient { BaseAddress = new Uri("abc", UriKind.Relative) });
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void CtorWithHttpClientAndBaseAddressAndRelativeUrl()
	{
		var settings = new ODataClientSettings(new HttpClient { BaseAddress = new Uri("http://localhost") }, new Uri("api", UriKind.Relative));
		settings.BaseUri!.AbsoluteUri.Should().Be("http://localhost/api");
	}

	[Fact]
	public void CtorWithHttpClientAndBaseAddressAndAbsoluteUrl_Throws()
	{
		var act = () => new ODataClientSettings(new HttpClient { BaseAddress = new Uri("http://localhost") }, new Uri("http://localhost/api"));
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void SettingAbsoluteBaseUriWhenHttpClientHasBaseAddress_Throws()
	{
		var settings = new ODataClientSettings(new HttpClient { BaseAddress = new Uri("http://localhost") });
		var act = () => settings.BaseUri = new Uri("http://other");
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void TypeCache_IsPerSettingsInstanceAndNeedsNoBaseUri()
	{
		var settings = new ODataClientSettings();
		var other = new ODataClientSettings();

		settings.TypeCache.Should().BeSameAs(settings.TypeCache);
		settings.TypeCache.Should().NotBeSameAs(other.TypeCache);

		settings.TypeCache.Register<Animal>("Foo");
		settings.TypeCache.DynamicContainerName(typeof(Animal)).Should().Be("Foo");
		other.TypeCache.DynamicContainerName(typeof(Animal)).Should().BeNull();
	}

	[Fact]
	public void TypeCache_UsesConfiguredNameMatchResolver()
	{
		var settings = new ODataClientSettings { NameMatchResolver = ODataNameMatchResolver.Strict };
		settings.TypeCache.NameMatchResolver.Should().BeSameAs(ODataNameMatchResolver.Strict);
	}

	[Fact]
	public void TypeCache_CanBeReplaced()
	{
		var custom = new TypeCache(new TypeConverter(), ODataNameMatchResolver.Alphanumeric);
		var settings = new ODataClientSettings { TypeCache = custom };
		settings.TypeCache.Should().BeSameAs(custom);
	}

	[Fact]
	public void Defaults()
	{
		var settings = new ODataClientSettings();

		settings.ProtocolVersion.Should().Be(ODataProtocolVersion.V4);
		settings.MetadataLevel.Should().Be(ODataMetadataLevel.Minimal);
		settings.MetadataFormat.Should().Be(MetadataFormat.Xml);
		settings.BatchFormat.Should().Be(BatchFormat.Multipart);
		settings.BatchPayloadUriOption.Should().Be(BatchPayloadUriOption.AbsoluteUri);
		settings.PreferredUpdateMethod.Should().Be(ODataUpdateMethod.Patch);
		settings.BulkOperationMode.Should().Be(BulkOperationMode.ClientSide);
		settings.ReadUntypedAsString.Should().BeTrue();
		settings.Validations.Should().Be(ValidationKinds.ThrowOnDuplicatePropertyNames | ValidationKinds.ThrowIfTypeConflictsWithMetadata, "undeclared properties ($compute, $apply, server extensions) must not fail a read");
		settings.NameMatchResolver.Should().BeSameAs(ODataNameMatchResolver.NotStrict);
		settings.RequestTimeout.Should().Be(TimeSpan.Zero);
		settings.AsyncPollingInterval.Should().Be(TimeSpan.FromSeconds(1));
		settings.TimeProvider.Should().BeSameAs(TimeProvider.System);
		settings.KeyAsSegment.Should().BeFalse();
		settings.MaxPageSize.Should().BeNull();
		settings.TraceFilter.Should().Be(ODataTrace.None);
		settings.WebRequestExceptionMessageSource.Should().Be(WebRequestExceptionMessageSource.ReasonPhrase);
	}
}
