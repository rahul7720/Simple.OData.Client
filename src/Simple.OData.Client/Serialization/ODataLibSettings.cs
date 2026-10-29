using Microsoft.OData;

namespace Simple.OData.Client.Serialization;

/// <summary>Builds ODataLib reader and writer settings from the client settings.</summary>
internal static class ODataLibSettings
{
	private const string JsonMinimalMetadata = "application/json;odata.metadata=minimal";

	public static ODataVersion Version(Session session) =>
		session.Settings.ProtocolVersion == ODataProtocolVersion.V401 ? ODataVersion.V401 : ODataVersion.V4;

	public static ODataMessageWriterSettings CreateWriterSettings(Session session, ODataFormat? format = null)
	{
		ArgumentNullException.ThrowIfNull(session);
		var settings = new ODataMessageWriterSettings
		{
			Version = Version(session),
			BaseUri = session.BaseUri,
			ODataUri = new ODataUri { ServiceRoot = session.BaseUri },
			Validations = ToODataLib(session.Settings.Validations),
			EnableMessageStreamDisposal = false,
		};

		if (format is not null)
		{
			settings.SetContentType(format);
		}
		else
		{
			settings.SetContentType(session.Settings.Ieee754Compatible ? JsonMinimalMetadata + ";IEEE754Compatible=true" : JsonMinimalMetadata, "utf-8");
		}

		return settings;
	}

	public static ODataMessageReaderSettings CreateReaderSettings(Session session)
	{
		ArgumentNullException.ThrowIfNull(session);
		return new ODataMessageReaderSettings
		{
			Version = Version(session),
			BaseUri = session.BaseUri,
			Validations = ToODataLib(session.Settings.Validations),
			EnableMessageStreamDisposal = false,
			ShouldIncludeAnnotation = session.Settings.IncludeAnnotationsInResults ? ODataUtils.CreateAnnotationFilter("*") : null,
		};
	}

	private static Microsoft.OData.ValidationKinds ToODataLib(ValidationKinds validations)
	{
		if (validations == ValidationKinds.All)
		{
			return Microsoft.OData.ValidationKinds.All;
		}

		var result = Microsoft.OData.ValidationKinds.None;
		if (validations.HasFlag(ValidationKinds.ThrowOnDuplicatePropertyNames))
		{
			result |= Microsoft.OData.ValidationKinds.ThrowOnDuplicatePropertyNames;
		}

		if (validations.HasFlag(ValidationKinds.ThrowOnUndeclaredPropertyForNonOpenType))
		{
			result |= Microsoft.OData.ValidationKinds.ThrowOnUndeclaredPropertyForNonOpenType;
		}

		if (validations.HasFlag(ValidationKinds.ThrowIfTypeConflictsWithMetadata))
		{
			result |= Microsoft.OData.ValidationKinds.ThrowIfTypeConflictsWithMetadata;
		}

		return result;
	}
}
