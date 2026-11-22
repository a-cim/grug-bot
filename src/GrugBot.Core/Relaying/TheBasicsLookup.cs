namespace GrugBot.Core.Relaying;

/// <summary>
/// What grug-bot found when it looked for thebasics' Proximity chat group at runtime (gotcha #15). The mod builds it
/// by reflection (ADR 0002); <see cref="TheBasicsGuard.Decide"/> turns it into relay on or off and one log line.
/// </summary>
public sealed class TheBasicsLookup
{
    private TheBasicsLookup(TheBasicsLookupOutcome outcome, int proximityChatGroupId, string? failureCause)
    {
        Outcome = outcome;
        ProximityChatGroupId = proximityChatGroupId;
        FailureCause = failureCause;
    }

    public TheBasicsLookupOutcome Outcome { get; }

    /// <summary>thebasics' Proximity chat group ID, read from the running mod. Only meaningful when <see cref="Outcome"/> is <see cref="TheBasicsLookupOutcome.Found"/>.</summary>
    public int ProximityChatGroupId { get; }

    /// <summary>Why the lookup failed, for the log. Only set when <see cref="Outcome"/> is <see cref="TheBasicsLookupOutcome.Failed"/>.</summary>
    public string? FailureCause { get; }

    /// <summary>thebasics isn't installed, or isn't enabled.</summary>
    public static TheBasicsLookup NotInstalled() => new(TheBasicsLookupOutcome.NotInstalled, 0, null);

    /// <summary>thebasics is running and its Proximity chat group has this ID.</summary>
    public static TheBasicsLookup Found(int proximityChatGroupId) => new(TheBasicsLookupOutcome.Found, proximityChatGroupId, null);

    /// <summary>thebasics seems to be there, but its Proximity chat group couldn't be read, for example after an update renamed something.</summary>
    /// <param name="cause">A short description of what went wrong, for the log.</param>
    public static TheBasicsLookup Failed(string cause) => new(TheBasicsLookupOutcome.Failed, 0, cause);

    public override string ToString() => Outcome switch
    {
        TheBasicsLookupOutcome.Found => "Found (Proximity chat group " + ProximityChatGroupId + ")",
        TheBasicsLookupOutcome.Failed => "Failed (" + FailureCause + ")",
        _ => Outcome.ToString(),
    };
}
