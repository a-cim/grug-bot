namespace GrugBot.Core.Config;

/// <summary>
/// Per-route switches for the relay and system messages. Everything is on by default except <see cref="DeathCount"/>,
/// which needs thebasics.
/// </summary>
public sealed class RelayConfig
{
    public bool DiscordToGame { get; set; } = true;

    public bool Joins { get; set; } = true;

    public bool Leaves { get; set; } = true;

    public bool Deaths { get; set; } = true;

    /// <summary>
    /// Ends each death line with the player's death count from thebasics' stats, the one <c>/playerstats</c> shows:
    /// <c>This is death number 7.</c> (#32, decision #52). Off by default, because many servers don't run thebasics.
    /// Without thebasics, or while it isn't counting deaths, lines have no count.
    /// </summary>
    public bool DeathCount { get; set; }

    public bool ServerStartStop { get; set; } = true;

    public bool TemporalStorms { get; set; } = true;
}
