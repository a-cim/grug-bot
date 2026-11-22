using System;
using System.Linq;
using GrugBot.Core.Formatting;
using GrugBot.Core.Sending;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordOutboxTests
{
    private const ulong ChatId = 111;
    private const ulong SystemId = 222;

    [Fact]
    public void A_system_channel_of_0_means_the_chat_channel()
    {
        // Test plan 3.6, the default.
        var outbox = new DiscordOutbox(ChatId, 0);

        Assert.Equal(ChatId, outbox.SystemChannelId);
        Assert.False(outbox.HasSeparateSystemChannel);
        Assert.Equal(new[] { ChatId }, outbox.ChannelIds);
    }

    [Fact]
    public void A_system_channel_set_to_the_chat_channel_is_the_same_channel()
    {
        var outbox = new DiscordOutbox(ChatId, ChatId);

        Assert.False(outbox.HasSeparateSystemChannel);
        Assert.Equal(new[] { ChatId }, outbox.ChannelIds);
    }

    [Fact]
    public void A_separate_system_channel_gets_its_own_queue()
    {
        // Test plan 3.6: system messages go to #system, chat stays in #chat.
        var outbox = new DiscordOutbox(ChatId, SystemId);
        outbox.EnqueueChat("**Grug:** hello");
        outbox.EnqueueSystem("***✅ Server started***");
        outbox.EnqueueChat("**Ugg:** hi");

        Assert.True(outbox.HasSeparateSystemChannel);
        Assert.Equal(new[] { ChatId, SystemId }, outbox.ChannelIds);
        Assert.Equal(new[] { "**Grug:** hello\n**Ugg:** hi" }, outbox.DrainMessages(ChatId));
        Assert.Equal(new[] { "***✅ Server started***" }, outbox.DrainMessages(SystemId));
        Assert.True(outbox.IsEmpty);
    }

    [Fact]
    public void In_one_channel_chat_and_system_lines_keep_the_order_they_happened_in()
    {
        var outbox = new DiscordOutbox(ChatId, 0);
        outbox.EnqueueChat("**Grug:** brb");
        outbox.EnqueueSystem("***⛔ Server shutdown***");

        Assert.Equal(new[] { "**Grug:** brb\n***⛔ Server shutdown***" }, outbox.DrainMessages(ChatId));
        Assert.True(outbox.IsEmpty);
    }

    [Fact]
    public void Is_empty_for_looks_at_one_channel_only()
    {
        var outbox = new DiscordOutbox(ChatId, SystemId);
        outbox.EnqueueSystem("***✅ Server started***");

        Assert.True(outbox.IsEmptyFor(ChatId));
        Assert.False(outbox.IsEmptyFor(SystemId));
        Assert.False(outbox.IsEmpty);
    }

    [Fact]
    public void Clearing_empties_every_channel()
    {
        var outbox = new DiscordOutbox(ChatId, SystemId);
        outbox.EnqueueChat("**Grug:** hello");
        outbox.EnqueueSystem("***✅ Server started***");

        outbox.Clear();

        Assert.True(outbox.IsEmpty);
        Assert.Empty(outbox.DrainMessages(ChatId));
        Assert.Empty(outbox.DrainMessages(SystemId));
    }

    [Fact]
    public void In_one_channel_chat_and_system_lines_share_the_500_line_limit_and_one_skipped_line()
    {
        var time = new ManualTimeProvider();
        var outbox = new DiscordOutbox(ChatId, 0, time);
        outbox.EnqueueSystem(SystemMessages.ServerStarted);
        for (int i = 1; i <= 300; i++)
        {
            outbox.EnqueueChat("**Grug:** line " + i);
        }

        for (int i = 1; i <= 300; i++)
        {
            outbox.EnqueueSystem(SystemMessages.Format("system " + i));
        }

        string[] posted = outbox.DrainMessages(ChatId).SelectMany(m => m.Split('\n')).ToArray();

        // 601 lines, 500 kept: "Server started" and the first 100 chat lines made way.
        Assert.Equal(1 + DiscordSendQueue.MaxLines, posted.Length);
        Assert.Equal(SystemMessages.MessagesSkipped(101), posted[0]);
        Assert.Equal("**Grug:** line 101", posted[1]);
        Assert.Equal(SystemMessages.Format("system 300"), posted[^1]);
        Assert.True(outbox.IsEmpty);
    }

    [Fact]
    public void With_a_system_channel_each_channel_has_its_own_limit_and_skipped_line()
    {
        var time = new ManualTimeProvider();
        var outbox = new DiscordOutbox(ChatId, SystemId, time);
        outbox.EnqueueChat("**Grug:** old");
        outbox.EnqueueSystem(SystemMessages.ServerStarted);
        outbox.EnqueueSystem(SystemMessages.Format("old join"));
        time.Advance(TimeSpan.FromMinutes(4));
        for (int i = 1; i <= DiscordSendQueue.MaxLines; i++)
        {
            outbox.EnqueueChat("**Ugg:** line " + i);
        }

        outbox.EnqueueSystem(SystemMessages.Format("new join"));
        time.Advance(TimeSpan.FromMinutes(2));

        string[] chat = outbox.DrainMessages(ChatId).SelectMany(m => m.Split('\n')).ToArray();
        string[] system = outbox.DrainMessages(SystemId).SelectMany(m => m.Split('\n')).ToArray();

        // The chat channel dropped its oldest line to stay at 500; the system channel dropped two lines by age.
        Assert.Equal(SystemMessages.MessagesSkipped(1), chat[0]);
        Assert.Equal(1 + DiscordSendQueue.MaxLines, chat.Length);
        Assert.Equal(new[] { SystemMessages.MessagesSkipped(2), SystemMessages.Format("new join") }, system);
        Assert.True(outbox.IsEmpty);
    }

    [Fact]
    public void With_a_system_channel_a_channel_that_dropped_nothing_posts_no_skipped_line()
    {
        var time = new ManualTimeProvider();
        var outbox = new DiscordOutbox(ChatId, SystemId, time);
        outbox.EnqueueChat("**Grug:** old");
        time.Advance(TimeSpan.FromMinutes(4));
        outbox.EnqueueSystem(SystemMessages.ServerStarted);
        time.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(new[] { SystemMessages.MessagesSkipped(1) }, outbox.DrainMessages(ChatId));
        Assert.Equal(new[] { SystemMessages.ServerStarted }, outbox.DrainMessages(SystemId));
    }

    [Fact]
    public void A_channel_nothing_is_queued_for_is_refused()
    {
        var outbox = new DiscordOutbox(ChatId, 0);

        Assert.Throws<ArgumentException>(() => outbox.DrainMessages(SystemId));
    }
}
