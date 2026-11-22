using System;
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
    public void A_channel_nothing_is_queued_for_is_refused()
    {
        var outbox = new DiscordOutbox(ChatId, 0);

        Assert.Throws<ArgumentException>(() => outbox.DrainMessages(SystemId));
    }
}
