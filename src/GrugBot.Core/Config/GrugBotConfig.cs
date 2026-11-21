using System.Collections.Generic;

namespace GrugBot.Core.Config;

/// <summary>
/// The contents of <c>ModConfig/grugbot.json</c> (01-architecture § Config).
/// The game serializes this class with Newtonsoft.Json, so every setting is a public property
/// with a default, and properties are declared in the order they appear in the file.
/// </summary>
public sealed class GrugBotConfig
{
    /// <summary>File name passed to the game's mod config API; it lives in the ModConfig folder.</summary>
    public const string FileName = "grugbot.json";

    /// <summary>How the file is named in log lines, so server owners know where to look.</summary>
    public const string DisplayPath = "ModConfig/" + FileName;

    /// <summary>
    /// The config format this build writes (decision #28). Bump it whenever a setting is added, renamed or removed:
    /// files already at the current version are never rewritten, so without a bump existing files never get the change
    /// (issue #14). A test in <c>GrugBotConfigTests</c> fails until you do.
    /// </summary>
    public const int CurrentVersion = 1;

    public const string DefaultDiscordNameColor = "#7289DA";

    public int ConfigVersion { get; set; } = CurrentVersion;

    /// <summary>
    /// Secret. The <c>GRUGBOT_DISCORD_TOKEN</c> environment variable wins if it is set (decision #27).
    /// Never log this value.
    /// </summary>
    public string DiscordToken { get; set; } = "";

    /// <summary>The Discord channel paired with General. 0 means not configured.</summary>
    public ulong ChatChannelId { get; set; }

    /// <summary>The Discord channel for system messages. 0 means use <see cref="ChatChannelId"/>.</summary>
    public ulong SystemChannelId { get; set; }

    public string DiscordNameColor { get; set; } = DefaultDiscordNameColor;

    public bool IgnoreOtherBots { get; set; } = true;

    public List<ulong> IgnoredDiscordUserIds { get; set; } = new();

    public bool EscapeMarkdown { get; set; }

    public RelayConfig Relay { get; set; } = new();

    /// <summary>
    /// Puts back defaults for sections a hand-edited file has set to <c>null</c>
    /// (for example <c>"Relay": null</c>), so the rest of the mod never sees a null section.
    /// </summary>
    public void FillMissingSections()
    {
        DiscordToken ??= "";
        DiscordNameColor ??= DefaultDiscordNameColor;
        IgnoredDiscordUserIds ??= new List<ulong>();
        Relay ??= new RelayConfig();
    }
}
