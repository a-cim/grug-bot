using System;
using System.Collections.Generic;
using GrugBot.Core.Formatting;

namespace GrugBot.Core.Sending;

/// <summary>
/// Lines waiting to be posted to one Discord channel. The game thread adds lines; the send loop drains
/// them about once a second and posts the batched messages (01-architecture § Lifecycle). Safe to use
/// from several threads at once.
/// </summary>
/// <remarks>
/// <para>
/// Lines wait here for as long as Discord is unreachable, within the backlog limits of decision #23. A line
/// is dropped when it's older than <see cref="MaxAge"/> at the drain that would post it, or when it's the
/// oldest and the queue goes over <see cref="MaxLines"/>. Every kind of line counts alike: chat lines and
/// system messages, "Server started" included, which waits for Discord like anything else. Each dropped line
/// is counted once, and the next drain posts one <see cref="SystemMessages.MessagesSkipped"/> line with that
/// count ahead of the lines that were kept, then starts counting again from zero. Drains only happen once
/// Discord is connected, so that is one line per outage, posted after reconnecting (test plan 6.4).
/// </para>
/// <para>
/// Ages come from <see cref="TimeProvider.GetTimestamp"/>, a monotonic clock that a change to the computer's
/// time can't move, and that tests can fake. Every operation holds one lock for a few steps: queueing is O(1),
/// and a drain looks only at the lines it takes.
/// </para>
/// </remarks>
public sealed class DiscordSendQueue
{
    /// <summary>Lines older than this when they would be posted are dropped (decision #23).</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    /// <summary>The most lines kept waiting; queueing one more drops the oldest (decision #23).</summary>
    public const int MaxLines = 500;

    private readonly object _lock = new();
    private readonly TimeProvider _time;

    // Oldest first. The timestamps only go up, since each is read under the lock as its line is added.
    private Queue<QueuedLine> _lines = new();

    // Lines dropped since the last drain, by either limit.
    private int _skipped;

    public DiscordSendQueue()
        : this(TimeProvider.System)
    {
    }

    /// <param name="time">The clock line ages are measured with. Tests pass a fake one.</param>
    public DiscordSendQueue(TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>True when there is nothing to post: no lines, and no dropped lines still to report.</summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _lines.Count == 0 && _skipped == 0;
            }
        }
    }

    /// <summary>Queues a line, dropping (and counting) the oldest one if that makes more than <see cref="MaxLines"/>.</summary>
    public void Enqueue(string line)
    {
        lock (_lock)
        {
            _lines.Enqueue(new QueuedLine(line, _time.GetTimestamp()));
            if (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
                _skipped++;
            }
        }
    }

    /// <summary>
    /// Throws away everything queued, for when nothing will ever be sent (a rejected token, say). The count of
    /// dropped lines goes too, so no "messages skipped" line follows.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _lines.Clear();
            _skipped = 0;
        }
    }

    /// <summary>
    /// Takes every line queued so far and returns them joined into Discord messages, in order
    /// (see <see cref="DiscordMessageBatcher.Batch"/>). Lines older than <see cref="MaxAge"/> are dropped first.
    /// If any line was dropped since the last drain, the first line is a
    /// <see cref="SystemMessages.MessagesSkipped"/> line saying how many. Lines queued while this runs wait for
    /// the next drain.
    /// </summary>
    public List<string> DrainMessages()
    {
        Queue<QueuedLine> taken;
        int skipped;
        lock (_lock)
        {
            long now = _time.GetTimestamp();
            while (_lines.Count > 0 && _time.GetElapsedTime(_lines.Peek().QueuedAt, now) > MaxAge)
            {
                _lines.Dequeue();
                _skipped++;
            }

            taken = _lines;
            _lines = new Queue<QueuedLine>();
            skipped = _skipped;
            _skipped = 0;
        }

        // Batched outside the lock, so the game thread never waits on it.
        var lines = new List<string>(taken.Count + 1);
        if (skipped > 0)
        {
            lines.Add(SystemMessages.MessagesSkipped(skipped));
        }

        foreach (QueuedLine queued in taken)
        {
            lines.Add(queued.Line);
        }

        return DiscordMessageBatcher.Batch(lines);
    }

    /// <param name="QueuedAt">When the line was queued, as a <see cref="TimeProvider.GetTimestamp"/> value.</param>
    private readonly record struct QueuedLine(string Line, long QueuedAt);
}
