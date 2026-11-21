using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GrugBot.Core.Sending;

/// <summary>
/// Lines waiting to be posted to one Discord channel. The game thread adds lines; the send loop drains
/// them about once a second and posts the batched messages (01-architecture § Lifecycle). Safe to use
/// from several threads at once.
/// </summary>
/// <remarks>
/// Lines wait here for as long as Discord is unreachable. Limiting that backlog (decision #23) belongs here.
/// </remarks>
public sealed class DiscordSendQueue
{
    private readonly ConcurrentQueue<string> _lines = new();

    public bool IsEmpty => _lines.IsEmpty;

    public void Enqueue(string line) => _lines.Enqueue(line);

    /// <summary>Throws away everything queued, for when nothing will ever be sent (a rejected token, say).</summary>
    public void Clear() => _lines.Clear();

    /// <summary>
    /// Takes every line queued so far and returns them joined into Discord messages, in order
    /// (see <see cref="DiscordMessageBatcher.Batch"/>). Lines queued while this runs wait for the next drain.
    /// </summary>
    public List<string> DrainMessages()
    {
        var lines = new List<string>();
        while (_lines.TryDequeue(out string? line))
        {
            lines.Add(line);
        }

        return DiscordMessageBatcher.Batch(lines);
    }
}
