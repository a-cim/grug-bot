using GrugBot.Core.Sending;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordSendQueueTests
{
    [Fact]
    public void Draining_joins_everything_queued_so_far_in_order()
    {
        var queue = new DiscordSendQueue();
        queue.Enqueue("**Grug:** one");
        queue.Enqueue("**Ugg:** two");
        queue.Enqueue("**Grug:** three");

        Assert.Equal(new[] { "**Grug:** one\n**Ugg:** two\n**Grug:** three" }, queue.DrainMessages());
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void A_drained_queue_gives_nothing_until_more_is_queued()
    {
        var queue = new DiscordSendQueue();
        queue.Enqueue("**Grug:** one");
        queue.DrainMessages();

        Assert.Empty(queue.DrainMessages());

        queue.Enqueue("**Grug:** two");
        Assert.Equal(new[] { "**Grug:** two" }, queue.DrainMessages());
    }

    [Fact]
    public void Clearing_throws_away_everything_queued()
    {
        var queue = new DiscordSendQueue();
        queue.Enqueue("**Grug:** one");
        queue.Enqueue("**Ugg:** two");

        queue.Clear();

        Assert.True(queue.IsEmpty);
        Assert.Empty(queue.DrainMessages());
    }
}
