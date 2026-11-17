namespace GrugBot.Core.Config;

/// <summary>Per-route switches for the relay and system messages. Everything is on by default.</summary>
public sealed class RelayConfig
{
    public bool DiscordToGame { get; set; } = true;

    public bool Joins { get; set; } = true;

    public bool Leaves { get; set; } = true;

    public bool Deaths { get; set; } = true;

    public bool ServerStartStop { get; set; } = true;

    public bool TemporalStorms { get; set; } = true;
}
