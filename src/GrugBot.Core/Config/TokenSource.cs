namespace GrugBot.Core.Config;

/// <summary>Where the Discord token was found (decision #27).</summary>
public enum TokenSource
{
    /// <summary>No token anywhere.</summary>
    None,

    /// <summary>The <c>GRUGBOT_DISCORD_TOKEN</c> environment variable.</summary>
    EnvironmentVariable,

    /// <summary><c>DiscordToken</c> in the config file.</summary>
    ConfigFile,
}
