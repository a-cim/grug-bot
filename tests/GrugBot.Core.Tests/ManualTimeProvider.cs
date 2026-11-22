using System;
using System.Threading;

namespace GrugBot.Core.Tests;

/// <summary>A clock that only moves when a test says so. Its timestamps count in <see cref="TimeSpan"/> ticks.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    // Not 0, so nothing can lean on timestamps starting there.
    private long _ticks = TimeSpan.FromHours(1).Ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
