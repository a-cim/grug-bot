using System.Collections.Generic;

namespace GrugBot.Core.Config;

/// <summary>Decides whether grug-bot has what it needs to start relaying.</summary>
public static class ConfigCheck
{
    /// <summary>
    /// Returns <c>null</c> when the token and chat channel are both set. Otherwise returns one log line
    /// naming everything that is missing and where to set it (01-architecture § Lifecycle, test 1.1).
    /// The line never contains the token. It has no curly braces, so it is safe as a log format string.
    /// </summary>
    public static string? DescribeMissing(ResolvedToken token, GrugBotConfig config)
    {
        var missing = new List<string>();

        if (token.IsMissing)
        {
            missing.Add("no Discord token (set the " + DiscordTokenResolver.EnvironmentVariableName
                + " environment variable, or DiscordToken in " + GrugBotConfig.DisplayPath + ")");
        }

        if (config.ChatChannelId == 0)
        {
            missing.Add("no chat channel (set ChatChannelId in " + GrugBotConfig.DisplayPath + ")");
        }

        if (missing.Count == 0)
        {
            return null;
        }

        return "grug-bot is not configured, so the relay is off: " + string.Join(" and ", missing)
            + ". The server carries on normally; restart it after fixing the config.";
    }
}
