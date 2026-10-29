using System.Collections.Concurrent;

namespace Simple.OData.Client.Tests;

/// <summary>The public OData V4 services and the session roots they redirect to.</summary>
internal static class LiveServices
{
	public const string NorthwindReadOnly = "https://services.odata.org/V4/Northwind/Northwind.svc/";

	/// <summary>Redirects to a per-session read-write copy of the OData reference service.</summary>
	public const string ODataReadWrite = "https://services.odata.org/V4/(S(readwrite))/OData/OData.svc/";

	/// <summary>Redirects to a per-session read-write TripPin service.</summary>
	public const string TripPinReadWrite = "https://services.odata.org/TripPinRESTierService/";

	private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromSeconds(60) };

	private static readonly ConcurrentDictionary<string, Lazy<Task<Uri>>> Sessions = new(StringComparer.Ordinal);

	/// <summary>Resolves the service root, following the redirect that assigns a session key; one session per service per test run.</summary>
	public static Task<Uri> ResolveAsync(string serviceUrl) =>
		Sessions.GetOrAdd(serviceUrl, url => new Lazy<Task<Uri>>(() => ResolveCoreAsync(url))).Value;

	private static async Task<Uri> ResolveCoreAsync(string serviceUrl)
	{
		using var response = await Http.GetAsync(new Uri(serviceUrl));
		response.EnsureSuccessStatusCode();
		var effective = response.RequestMessage?.RequestUri ?? new Uri(serviceUrl);
		var text = effective.AbsoluteUri;
		return new Uri(text.EndsWith('/') ? text : text + "/");
	}
}

/// <summary>Creates clients bound to a live service.</summary>
public abstract class LiveTestBase
{
	protected static async Task<ODataClient> CreateClientAsync(string serviceUrl, Action<ODataClientSettings>? configure = null)
	{
		var root = await LiveServices.ResolveAsync(serviceUrl);
		var settings = new ODataClientSettings(root)
		{
			IgnoreResourceNotFoundException = true,
			IgnoreUnmappedProperties = true,
		};
		configure?.Invoke(settings);
		return new ODataClient(settings);
	}
}
