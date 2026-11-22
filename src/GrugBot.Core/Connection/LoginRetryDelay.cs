using System;

namespace GrugBot.Core.Connection;

/// <summary>
/// How long to wait before trying to log in to Discord again when it couldn't be reached
/// (test plan 1.5: the bot connects once the network returns). Once logged in, Discord.Net
/// reconnects the gateway by itself, so this is only for the first login.
/// </summary>
public static class LoginRetryDelay
{
    private static readonly TimeSpan[] Delays =
    {
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
    };

    /// <summary>
    /// The longest wait between tries. Kept well under the 5-minute backlog limit (decision #50), so "Server started"
    /// queued during a startup outage is still young enough to post once Discord is reachable again.
    /// </summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(2);

    /// <summary>The wait after <paramref name="failedAttempts"/> failed tries in a row (1 or more).</summary>
    public static TimeSpan After(int failedAttempts)
    {
        if (failedAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(failedAttempts), failedAttempts, "Call this after at least one failed attempt.");
        }

        return failedAttempts <= Delays.Length ? Delays[failedAttempts - 1] : Maximum;
    }
}
