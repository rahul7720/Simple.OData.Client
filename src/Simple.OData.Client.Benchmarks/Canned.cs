using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace Simple.OData.Client.Benchmarks;

/// <summary>Embedded TripPin documents and a handler that serves them without touching the network.</summary>
public static class Canned
{
	public static readonly Uri ServiceRoot = new("http://services.odata.org/V4/(S(4taa1h2202lz2pi2bpqff3uy))/TripPinServiceRW/");

	public static string Metadata { get; } = Read("TripPin.xml");

	public static string PeopleFeed { get; } = Read("TripPin_result_20.json");

	public static ODataClient CreateClient() =>
		new(new ODataClientSettings(new HttpClient(new CannedHandler()) { BaseAddress = ServiceRoot }));

	private static string Read(string name)
	{
		using var stream = typeof(Canned).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing resource {name}");
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	private sealed class CannedHandler : HttpMessageHandler
	{
		private static readonly byte[] MetadataBytes = System.Text.Encoding.UTF8.GetBytes(Metadata);
		private static readonly byte[] FeedBytes = System.Text.Encoding.UTF8.GetBytes(PeopleFeed);

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var isMetadata = request.RequestUri!.AbsolutePath.EndsWith("$metadata", StringComparison.Ordinal);
			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				RequestMessage = request,
				Content = new ByteArrayContent(isMetadata ? MetadataBytes : FeedBytes),
			};
			response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(isMetadata ? "application/xml" : "application/json;odata.metadata=minimal");
			response.Headers.TryAddWithoutValidation("OData-Version", "4.0");
			return Task.FromResult(response);
		}
	}
}
