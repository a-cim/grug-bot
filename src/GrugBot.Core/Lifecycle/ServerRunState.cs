using System.Threading;

namespace GrugBot.Core.Lifecycle;

/// <summary>
/// Where the game server is in its run, as grug-bot sees it: starting, running (the first game tick is
/// about to run), or shutting down. One instance per server run. Safe to read from any thread.
/// </summary>
/// <remarks>
/// <para>
/// It answers "has this already happened?" so each of the start and stop messages is posted at most once,
/// however often the game or Discord calls back. Discord's Ready plays no part: it fires again after
/// reconnects (gotcha #11), while the server starts once.
/// </para>
/// <para>
/// System messages that must stop once shutdown begins read <see cref="IsShuttingDown"/>: leave messages
/// (#7) are suppressed from then on, because the game disconnects everyone and the shutdown line already
/// says so (decision #22).
/// </para>
/// </remarks>
public sealed class ServerRunState
{
    private const int Starting = 0;
    private const int Running = 1;
    private const int ShuttingDown = 2;

    private int _phase = Starting;

    // Set together with Running and kept through ShuttingDown, so a shutdown can tell whether the server ever ran.
    private int _started;

    /// <summary>True once the server has started running, including after shutdown has begun.</summary>
    public bool HasStarted => Volatile.Read(ref _started) != 0;

    /// <summary>True once shutdown has begun. It never goes back.</summary>
    public bool IsShuttingDown => Volatile.Read(ref _phase) == ShuttingDown;

    /// <summary>
    /// Records that the server is running. Returns <c>true</c> only the first time, and never once shutdown
    /// has begun: the caller posts "Server started" only when this says so.
    /// </summary>
    public bool TryMarkStarted()
    {
        if (Interlocked.CompareExchange(ref _phase, Running, Starting) != Starting)
        {
            return false;
        }

        Volatile.Write(ref _started, 1);
        return true;
    }

    /// <summary>
    /// Records that shutdown has begun. Returns <c>true</c> only the first time: the caller posts "Server
    /// shutdown" and flushes only when this says so, whichever of the game's shutdown hooks gets there first.
    /// </summary>
    public bool TryBeginShutdown() => Interlocked.Exchange(ref _phase, ShuttingDown) != ShuttingDown;
}
