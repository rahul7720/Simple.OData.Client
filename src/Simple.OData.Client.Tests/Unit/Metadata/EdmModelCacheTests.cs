using Simple.OData.Client.Metadata;
using Simple.OData.Client.Tests.Infrastructure;

namespace Simple.OData.Client.Tests.Unit.Metadata;

public class EdmModelCacheTests
{
	[Fact]
	public async Task GetOrAddAsync_LoadsOnceForConcurrentCallers()
	{
		var key = Guid.NewGuid().ToString();
		var loads = 0;
		var document = Resources.Read("Northwind4.xml");

		var tasks = Enumerable.Range(0, 20).Select(_ => EdmModelCache.GetOrAddAsync(key, async ct =>
		{
			Interlocked.Increment(ref loads);
			await Task.Delay(20, ct);
			return document;
		}, MetadataLoader.Parse, CancellationToken.None).AsTask());

		var results = await Task.WhenAll(tasks);

		loads.Should().Be(1);
		results.Should().AllSatisfy(x => x.Should().BeSameAs(results[0]));
		results[0].Key.Should().Be(key);
		results[0].Document.Should().Be(document);
	}

	[Fact]
	public async Task GetOrAddAsync_FailedLoadIsNotCached()
	{
		var key = Guid.NewGuid().ToString();
		var act = () => EdmModelCache.GetOrAddAsync(key, _ => throw new HttpRequestException("boom"), MetadataLoader.Parse, CancellationToken.None).AsTask();
		await act.Should().ThrowAsync<HttpRequestException>();

		var cached = await EdmModelCache.GetOrAddAsync(key, _ => Task.FromResult(Resources.Read("Northwind4.xml")), MetadataLoader.Parse, CancellationToken.None);
		cached.Model.EntityContainer.FindEntitySet("Products").Should().NotBeNull();
	}

	[Fact]
	public async Task GetOrAddAsync_PropagatesCancellation()
	{
		var key = Guid.NewGuid().ToString();
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var act = () => EdmModelCache.GetOrAddAsync(key, async ct => { await Task.Delay(1000, ct); return ""; }, MetadataLoader.Parse, cts.Token).AsTask();
		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task GetOrAdd_SyncAndRemove()
	{
		var key = Guid.NewGuid().ToString();
		var cached = EdmModelCache.GetOrAdd(key, Resources.Read("TripPin.xml"), MetadataLoader.Parse);
		cached.Model.EntityContainer.FindSingleton("Me").Should().NotBeNull();

		var again = await EdmModelCache.GetOrAddAsync(key, _ => throw new InvalidOperationException("should not load"), MetadataLoader.Parse, CancellationToken.None);
		again.Should().BeSameAs(cached);

		EdmModelCache.Remove(key);
		var loads = 0;
		await EdmModelCache.GetOrAddAsync(key, _ => { loads++; return Task.FromResult(Resources.Read("TripPin.xml")); }, MetadataLoader.Parse, CancellationToken.None);
		loads.Should().Be(1);
	}
}
