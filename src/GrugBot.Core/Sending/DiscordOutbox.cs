using System;
using System.Collections.Generic;

namespace GrugBot.Core.Sending;

/// <summary>
/// Everything waiting to be posted to Discord, one <see cref="DiscordSendQueue"/> per channel. Chat lines go
/// to the chat channel; system messages go to the system channel, which is the chat channel when
/// <c>SystemChannelId</c> is 0 (05-message-format, test plan 3.6). Safe to use from several threads at once.
/// </summary>
/// <remarks>
/// <para>
/// When both kinds go to the same channel they share one queue, so they're posted in the order they
/// happened and batched together: a join line stays above the chat line said right after it, and the
/// channel gets one message per send tick rather than two, which spares its rate limit (gotcha #10).
/// When the channels differ, each has its own queue, so a batch never mixes chat and system lines.
/// </para>
/// <para>Backlog limits while Discord is unreachable (decision #23) belong in <see cref="DiscordSendQueue"/>.</para>
/// </remarks>
public sealed class DiscordOutbox
{
    private readonly DiscordSendQueue _chat = new();

    // The same instance as _chat when the system channel is the chat channel.
    private readonly DiscordSendQueue _system;

    /// <param name="chatChannelId">The chat channel (<c>ChatChannelId</c>).</param>
    /// <param name="systemChannelId">The <c>SystemChannelId</c> setting: 0 means the chat channel.</param>
    public DiscordOutbox(ulong chatChannelId, ulong systemChannelId)
    {
        ChatChannelId = chatChannelId;
        SystemChannelId = ResolveSystemChannelId(chatChannelId, systemChannelId);

        if (SystemChannelId == ChatChannelId)
        {
            _system = _chat;
            ChannelIds = new[] { ChatChannelId };
        }
        else
        {
            _system = new DiscordSendQueue();
            ChannelIds = new[] { ChatChannelId, SystemChannelId };
        }
    }

    public ulong ChatChannelId { get; }

    /// <summary>Where system messages go: <c>SystemChannelId</c>, or the chat channel when that is 0.</summary>
    public ulong SystemChannelId { get; }

    /// <summary>True when system messages have a channel of their own.</summary>
    public bool HasSeparateSystemChannel => SystemChannelId != ChatChannelId;

    /// <summary>Every channel something can be queued for: the chat channel first, then the system channel if it differs.</summary>
    public IReadOnlyList<ulong> ChannelIds { get; }

    /// <summary>True when nothing is waiting for any channel.</summary>
    public bool IsEmpty => _chat.IsEmpty && _system.IsEmpty;

    /// <summary>The system channel for a <c>SystemChannelId</c> setting: itself, or the chat channel when it's 0.</summary>
    public static ulong ResolveSystemChannelId(ulong chatChannelId, ulong systemChannelId) =>
        systemChannelId == 0 ? chatChannelId : systemChannelId;

    /// <summary>Queues a finished chat line for the chat channel.</summary>
    public void EnqueueChat(string line) => _chat.Enqueue(line);

    /// <summary>Queues a finished system message (see <c>SystemMessages</c>) for the system channel.</summary>
    public void EnqueueSystem(string line) => _system.Enqueue(line);

    /// <summary>True when nothing is waiting for <paramref name="channelId"/>.</summary>
    /// <exception cref="ArgumentException">The channel isn't one of <see cref="ChannelIds"/>.</exception>
    public bool IsEmptyFor(ulong channelId) => QueueFor(channelId).IsEmpty;

    /// <summary>
    /// Takes everything queued for <paramref name="channelId"/> so far, joined into Discord messages in the
    /// order it was queued (see <see cref="DiscordSendQueue.DrainMessages"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The channel isn't one of <see cref="ChannelIds"/>.</exception>
    public List<string> DrainMessages(ulong channelId) => QueueFor(channelId).DrainMessages();

    /// <summary>Throws away everything queued for every channel, for when nothing will ever be sent.</summary>
    public void Clear()
    {
        _chat.Clear();
        _system.Clear();
    }

    private DiscordSendQueue QueueFor(ulong channelId)
    {
        if (channelId == ChatChannelId)
        {
            return _chat;
        }

        if (channelId == SystemChannelId)
        {
            return _system;
        }

        throw new ArgumentException("Nothing is ever queued for channel " + channelId + ".", nameof(channelId));
    }
}
