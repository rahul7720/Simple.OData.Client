using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Batch;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Simple.OData.Client.Tests.TestServer.Data;
using Simple.OData.Client.Tests.TestServer.Infrastructure;

namespace Simple.OData.Client.Tests.TestServer;

/// <summary>
/// An in-process OData V4.01 Northwind service hosted on <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>.
/// Requests are sent through <see cref="CreateClient"/> or <see cref="Handler"/>; no socket is opened.
/// </summary>
public sealed class NorthwindTestServer : IAsyncDisposable
{
	private readonly WebApplication _app;

	private NorthwindTestServer(WebApplication app) => _app = app;

	/// <summary>The service root.</summary>
	public static Uri BaseAddress { get; } = new("http://localhost/odata/");

	/// <summary>The EDM model served by the host.</summary>
	public IEdmModel Model => _app.Services.GetRequiredService<IEdmModel>();

	/// <summary>A handler that dispatches requests into the host.</summary>
	public HttpMessageHandler Handler => _app.GetTestServer().CreateHandler();

	public static async Task<NorthwindTestServer> StartAsync()
	{
		var builder = WebApplication.CreateBuilder(new WebApplicationOptions
		{
			ApplicationName = typeof(NorthwindTestServer).Assembly.GetName().Name,
			EnvironmentName = "Testing",
		});
		builder.WebHost.UseTestServer();
		builder.Logging.ClearProviders();
		if (Environment.GetEnvironmentVariable("ODATA_TESTSERVER_LOG") == "1")
		{
			builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Debug);
		}

		var model = NorthwindEdmModel.Create();
		builder.Services.AddSingleton(model);
		builder.Services.AddSingleton<NorthwindData>();
		builder.Services.AddSingleton<AsyncMonitorStore>();
		builder.Services.AddSingleton<SelfClient>();
		builder.Services.AddSingleton<RequestRecorder>();
		builder.Services
			.AddControllers(options => options.Filters.Add<AggregationResultFilter>())
			.AddApplicationPart(typeof(NorthwindTestServer).Assembly)
			.AddOData(options =>
			{
				options.EnableQueryFeatures(null);
				options.EnableContinueOnErrorHeader = true;
				options.RouteOptions.EnableKeyAsSegment = true;
				options.RouteOptions.EnableKeyInParenthesis = true;
				options.RouteOptions.EnableQualifiedOperationCall = true;
				options.RouteOptions.EnableUnqualifiedOperationCall = true;
				options.AddRouteComponents("odata", model, ODataVersion.V401, services =>
				{
					services.AddSingleton<ODataBatchHandler>(new DefaultODataBatchHandler());
					services.AddSingleton<ISearchBinder, NorthwindSearchBinder>();
					services.AddSingleton<ODataUriResolver>(new AlternateKeysODataUriResolver(model) { EnableCaseInsensitive = true });
				});
			});

		var app = builder.Build();
		app.UseODataRouteDebug();
		RequestRecorder.Use(app, x => x.UseODataBatching());
		app.UseOmitValues();
		app.UseRouting();
		app.MapControllers();
		app.MapODataServiceDocument("odata", model);
		app.MapODataMetadata("odata/$metadata", model);
		AsyncMonitorEndpoint.Map(app);
		await app.StartAsync().ConfigureAwait(false);
		app.Services.GetRequiredService<SelfClient>().Client = app.GetTestServer().CreateClient();
		return new NorthwindTestServer(app);
	}

	/// <summary>Creates an <see cref="HttpClient"/> whose base address is the service root.</summary>
	public HttpClient CreateClient()
	{
		var client = _app.GetTestServer().CreateClient();
		client.BaseAddress = BaseAddress;
		return client;
	}

	/// <summary>Restores the seed data.</summary>
	public void ResetData() => _app.Services.GetRequiredService<NorthwindData>().Reset();

	/// <summary>The requests received since the last <see cref="ClearRequests"/>, batch sub-requests included.</summary>
	public IReadOnlyList<RecordedRequest> Requests => _app.Services.GetRequiredService<RequestRecorder>().Requests;

	/// <summary>Forgets the recorded requests.</summary>
	public void ClearRequests() => _app.Services.GetRequiredService<RequestRecorder>().Clear();

	public async ValueTask DisposeAsync()
	{
		await _app.StopAsync().ConfigureAwait(false);
		await _app.DisposeAsync().ConfigureAwait(false);
	}
}
