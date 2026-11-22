using GrugBot.Core.Lifecycle;
using Xunit;

namespace GrugBot.Core.Tests;

public class ServerRunStateTests
{
    [Fact]
    public void A_new_run_has_not_started_or_begun_shutting_down()
    {
        var state = new ServerRunState();

        Assert.False(state.HasStarted);
        Assert.False(state.IsShuttingDown);
    }

    [Fact]
    public void Starting_counts_only_the_first_time()
    {
        // Test plan 6.2 and 1.7: "Server started" once per run.
        var state = new ServerRunState();

        Assert.True(state.TryMarkStarted());
        Assert.False(state.TryMarkStarted());
        Assert.True(state.HasStarted);
        Assert.False(state.IsShuttingDown);
    }

    [Fact]
    public void Shutting_down_counts_only_the_first_time()
    {
        // The game's shutdown phase and the mod's Dispose both lead here; only one posts and flushes.
        var state = new ServerRunState();
        state.TryMarkStarted();

        Assert.True(state.TryBeginShutdown());
        Assert.False(state.TryBeginShutdown());
        Assert.True(state.IsShuttingDown);
        Assert.True(state.HasStarted);
    }

    [Fact]
    public void A_run_that_never_started_can_still_shut_down()
    {
        var state = new ServerRunState();

        Assert.True(state.TryBeginShutdown());
        Assert.True(state.IsShuttingDown);
        Assert.False(state.HasStarted);
    }

    [Fact]
    public void Nothing_starts_once_shutdown_has_begun()
    {
        var state = new ServerRunState();
        state.TryBeginShutdown();

        Assert.False(state.TryMarkStarted());
        Assert.False(state.HasStarted);
        Assert.True(state.IsShuttingDown);
    }
}
