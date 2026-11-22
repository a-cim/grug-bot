using System;
using GrugBot.Core.Players;

namespace GrugBot.Core.Sending;

/// <summary>
/// Decides when the bot presence (<c>players: n/max</c>) is sent to Discord. A join or leave makes it due
/// <see cref="SettleDelay"/> later, and each further join or leave starts that wait again, so a burst of them
/// costs one update (decision #26, test plan 3.7). A count already shown isn't sent again. Safe to use from
/// several threads at once.
/// </summary>
/// <remarks>
/// Times are whatever monotonic clock the caller uses, passed in as <c>now</c>, so the schedule can be tested
/// without waiting. Discord.Net allows 5 presence updates a minute and holds back any more, so the caller sends at
/// most one at a time.
/// </remarks>
public sealed class PresenceSchedule
{
    /// <summary>How long after the last join or leave the presence is updated (decision #26).</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private PlayerCount? _latest;
    private PlayerCount? _shown;
    private TimeSpan? _dueAt;

    /// <summary>A player joined or left: show <paramref name="count"/> once nobody has joined or left for <see cref="SettleDelay"/>.</summary>
    public void Changed(PlayerCount count, TimeSpan now)
    {
        lock (_lock)
        {
            _latest = count;
            _dueAt = now + SettleDelay;
        }
    }

    /// <summary>Show <paramref name="count"/> without waiting: the count when the server starts.</summary>
    public void ShowAtOnce(PlayerCount count, TimeSpan now)
    {
        lock (_lock)
        {
            _latest = count;
            _dueAt = now;
        }
    }

    /// <summary>
    /// Show the latest count again without waiting, even if it was shown already: Discord has just (re)connected.
    /// Does nothing until there is a count.
    /// </summary>
    public void ShowAgain(TimeSpan now)
    {
        lock (_lock)
        {
            if (_latest is null)
            {
                return;
            }

            _shown = null;
            _dueAt = now;
        }
    }

    /// <summary>
    /// The count to send now, or <c>null</c> when nothing is due or the count due is the one already shown. A count
    /// returned here counts as shown, so it's returned once.
    /// </summary>
    public PlayerCount? TakeDue(TimeSpan now)
    {
        lock (_lock)
        {
            if (_dueAt is not { } dueAt || now < dueAt)
            {
                return null;
            }

            _dueAt = null;
            if (_latest == _shown)
            {
                return null;
            }

            _shown = _latest;
            return _latest;
        }
    }
}
