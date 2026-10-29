namespace Simple.OData.Client.Tests.Infrastructure;

/// <summary>A time provider whose timers fire immediately and record the requested delays.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
	public List<TimeSpan> Delays { get; } = [];

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		Delays.Add(dueTime);
		var timer = new ImmediateTimer();
		if (dueTime != Timeout.InfiniteTimeSpan)
		{
			ThreadPool.QueueUserWorkItem(_ => callback(state));
		}

		return timer;
	}

	private sealed class ImmediateTimer : ITimer
	{
		public bool Change(TimeSpan dueTime, TimeSpan period) => true;

		public void Dispose()
		{
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
