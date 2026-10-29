using System.Buffers;
using Microsoft.OData;
using Microsoft.OData.Buffers;

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
			// the client advertises OData-MaxVersion: 4.01, so a 4.01 response (with prefix-less control information) must be readable
			MaxProtocolVersion = ODataVersion.V401,
			BaseUri = session.BaseUri,
			Validations = ToODataLib(session.Settings.Validations),
			EnableMessageStreamDisposal = false,
			EnableReadingODataAnnotationWithoutPrefix = true,
			EnableReadingKeyAsSegment = session.Settings.KeyAsSegment,
			ShouldIncludeAnnotation = session.Settings.IncludeAnnotationsInResults ? ODataUtils.CreateAnnotationFilter("*") : null,
			ArrayPool = SharedCharArrayPool.Instance,
			MessageQuotas = new ODataMessageQuotas
			{
				MaxReceivedMessageSize = long.MaxValue,
				MaxPartsPerBatch = 10_000,
				MaxOperationsPerChangeset = 10_000,
				MaxNestingDepth = 200,
			},
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

	/// <summary>Lets ODataLib rent character buffers from the shared array pool.</summary>
	private sealed class SharedCharArrayPool : ICharArrayPool
	{
		public static SharedCharArrayPool Instance { get; } = new();

		public char[] Rent(int minSize) => ArrayPool<char>.Shared.Rent(minSize);

		public void Return(char[] array) => ArrayPool<char>.Shared.Return(array);
	}
}
