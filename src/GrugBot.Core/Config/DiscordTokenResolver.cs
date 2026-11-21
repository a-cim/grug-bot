namespace GrugBot.Core.Config;

/// <summary>
/// Picks the Discord token: the <c>GRUGBOT_DISCORD_TOKEN</c> environment variable first,
/// else <c>DiscordToken</c> from the config file (decision #27).
/// </summary>
public static class DiscordTokenResolver
{
    public const string EnvironmentVariableName = "GRUGBOT_DISCORD_TOKEN";

    /// <summary>
    /// Pure so it can be tested: the caller reads the environment variable and passes its value in.
    /// Blank values count as not set, and surrounding whitespace (easy to paste by accident) is trimmed.
    /// </summary>
    public static ResolvedToken Resolve(string? environmentValue, string? configValue)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new ResolvedToken(environmentValue.Trim(), TokenSource.EnvironmentVariable);
        }

        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return new ResolvedToken(configValue.Trim(), TokenSource.ConfigFile);
        }

        return new ResolvedToken(null, TokenSource.None);
    }
}
