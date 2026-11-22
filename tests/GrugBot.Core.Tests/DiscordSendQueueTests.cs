using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GrugBot.Core.Formatting;
using GrugBot.Core.Sending;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordSendQueueTests
{
    private readonly ManualTimeProvider _time = new();

    private DiscordSendQueue NewQueue() => new(_time);

    /// <summary>The lines a drain posts, one per entry, whatever messages they were batched into.</summary>
    private static string[] LinesOf(IEnumerable<string> messages) => messages.SelectMany(m => m.Split('\n')).ToArray();

    private static string Chat(int n) => "**Grug:** line " + n;

    private static IEnumerable<string> Chats(int from, int count) => Enumerable.Range(from, count).Select(Chat);

    private void EnqueueChats(DiscordSendQueue queue, int from, int count)
    {
        foreach (string line in Chats(from, count))
        {
            queue.Enqueue(line);
        }
    }

    [Fact]
    public void Draining_joins_everything_queued_so_far_in_order()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** one");
        queue.Enqueue("**Ugg:** two");
        queue.Enqueue("**Grug:** three");

        Assert.Equal(new[] { "**Grug:** one\n**Ugg:** two\n**Grug:** three" }, queue.DrainMessages());
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void A_drained_queue_gives_nothing_until_more_is_queued()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** one");
        queue.DrainMessages();

        Assert.Empty(queue.DrainMessages());

        queue.Enqueue("**Grug:** two");
        Assert.Equal(new[] { "**Grug:** two" }, queue.DrainMessages());
    }

    [Fact]
    public void Clearing_throws_away_everything_queued()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** one");
        queue.Enqueue("**Ugg:** two");

        queue.Clear();

        Assert.True(queue.IsEmpty);
        Assert.Empty(queue.DrainMessages());
    }

    [Fact]
    public void Lines_queued_during_a_short_outage_are_all_posted_after_it()
    {
        // Test plan 6.3: two minutes without Discord loses nothing and says nothing was skipped.
        var queue = NewQueue();
        queue.Enqueue("**Grug:** anyone there?");
        _time.Advance(TimeSpan.FromMinutes(1));
        queue.Enqueue("**Ugg:** yes");
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(new[] { "**Grug:** anyone there?\n**Ugg:** yes" }, queue.DrainMessages());
    }

    [Fact]
    public void A_line_exactly_five_minutes_old_is_still_posted()
    {
        // Decision #23 drops lines *older* than 5 minutes.
        var queue = NewQueue();
        queue.Enqueue("**Grug:** just in time");
        _time.Advance(DiscordSendQueue.MaxAge);

        Assert.Equal(new[] { "**Grug:** just in time" }, queue.DrainMessages());
    }

    [Fact]
    public void Lines_older_than_five_minutes_are_dropped_and_counted()
    {
        // Test plan 6.4: the old lines go, one line says how many, and the newer lines follow it.
        var queue = NewQueue();
        queue.Enqueue("**Grug:** old one");
        queue.Enqueue("**Ugg:** old two");
        _time.Advance(TimeSpan.FromMinutes(4));
        queue.Enqueue("**Grug:** new");
        _time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

        Assert.Equal(
            new[] { SystemMessages.MessagesSkipped(2) + "\n**Grug:** new" },
            queue.DrainMessages());
    }

    [Fact]
    public void When_every_line_is_too_old_only_the_skipped_line_is_posted()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** one");
        queue.Enqueue(SystemMessages.ServerShutdown);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(2) }, queue.DrainMessages());
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void Only_the_newest_500_lines_are_kept()
    {
        var queue = NewQueue();
        EnqueueChats(queue, 1, DiscordSendQueue.MaxLines + 3);

        string[] posted = LinesOf(queue.DrainMessages());

        Assert.Equal(
            new[] { SystemMessages.MessagesSkipped(3) }.Concat(Chats(4, DiscordSendQueue.MaxLines)),
            posted);
    }

    [Fact]
    public void Exactly_500_lines_are_all_kept()
    {
        var queue = NewQueue();
        EnqueueChats(queue, 1, DiscordSendQueue.MaxLines);

        Assert.Equal(Chats(1, DiscordSendQueue.MaxLines), LinesOf(queue.DrainMessages()));
    }

    [Fact]
    public void Lines_dropped_by_both_limits_are_each_counted_once()
    {
        var queue = NewQueue();
        EnqueueChats(queue, 1, 300);
        _time.Advance(TimeSpan.FromMinutes(3));

        // 100 of the first 300 make way for these, which leaves 200 of them to grow too old.
        EnqueueChats(queue, 301, 300);
        _time.Advance(TimeSpan.FromMinutes(3));

        string[] posted = LinesOf(queue.DrainMessages());

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(300) }.Concat(Chats(301, 300)), posted);
    }

    [Fact]
    public void The_skipped_line_is_posted_once_per_outage()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** old");
        _time.Advance(TimeSpan.FromMinutes(6));
        queue.Enqueue("**Ugg:** new");

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(1) + "\n**Ugg:** new" }, queue.DrainMessages());

        // Reconnected: later drains post only what is queued after.
        Assert.Empty(queue.DrainMessages());
        queue.Enqueue("**Grug:** back");
        Assert.Equal(new[] { "**Grug:** back" }, queue.DrainMessages());
    }

    [Fact]
    public void A_second_outage_counts_from_zero()
    {
        var queue = NewQueue();
        queue.Enqueue("**Grug:** first outage");
        _time.Advance(TimeSpan.FromMinutes(6));
        queue.DrainMessages();

        queue.Enqueue("**Grug:** second outage one");
        queue.Enqueue("**Grug:** second outage two");
        _time.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(2) }, queue.DrainMessages());
    }

    [Fact]
    public void Server_started_queued_during_an_outage_arrives_within_the_limits()
    {
        // Test plan 6.5: "Server started" waits for Discord like any other line (decision #23).
        var queue = NewQueue();
        queue.Enqueue(SystemMessages.ServerStarted);
        _time.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(new[] { SystemMessages.ServerStarted }, queue.DrainMessages());
    }

    [Fact]
    public void Server_started_is_not_exempt_from_the_age_limit()
    {
        var queue = NewQueue();
        queue.Enqueue(SystemMessages.ServerStarted);
        _time.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(1) }, queue.DrainMessages());
    }

    [Fact]
    public void A_queue_that_dropped_lines_is_not_empty_until_drained()
    {
        var queue = NewQueue();
        EnqueueChats(queue, 1, DiscordSendQueue.MaxLines + 1);

        Assert.False(queue.IsEmpty);
        queue.DrainMessages();
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void Clearing_forgets_the_skipped_count_too()
    {
        // A rejected token: nothing will be sent, so no "messages skipped" line either.
        var queue = NewQueue();
        EnqueueChats(queue, 1, DiscordSendQueue.MaxLines + 5);

        queue.Clear();
        queue.Enqueue("**Grug:** hello");

        Assert.Equal(new[] { "**Grug:** hello" }, queue.DrainMessages());
    }

    [Fact]
    public async Task Every_line_queued_from_several_threads_is_posted_or_counted()
    {
        const int Threads = 4;
        const int PerThread = 2000;
        var queue = NewQueue();
        var posted = new List<string>();
        int skipped = 0;
        string skippedLine = SystemMessages.MessagesSkipped(1);
        string skippedPrefix = skippedLine.Substring(0, skippedLine.IndexOf('1', StringComparison.Ordinal));

        void Collect(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                if (line.StartsWith(skippedPrefix, StringComparison.Ordinal))
                {
                    string number = line.Substring(skippedPrefix.Length).Split(' ')[0];
                    skipped += int.Parse(number, CultureInfo.InvariantCulture);
                }
                else
                {
                    posted.Add(line);
                }
            }
        }

        Task[] writers = Enumerable.Range(0, Threads)
            .Select(t => Task.Run(() => EnqueueChats(queue, t * PerThread, PerThread)))
            .ToArray();
        while (writers.Any(w => !w.IsCompleted))
        {
            Collect(LinesOf(queue.DrainMessages()));
        }

        await Task.WhenAll(writers);
        Collect(LinesOf(queue.DrainMessages()));

        Assert.Equal(Threads * PerThread, posted.Count + skipped);
        Assert.Equal(posted.Count, posted.Distinct().Count());
    }
}
