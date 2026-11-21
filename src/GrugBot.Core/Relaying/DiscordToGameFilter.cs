using System.Collections.Generic;

namespace GrugBot.Core.Relaying;

/// <summary>
/// Decides whose chat channel messages reach General (01-architecture § Discord → Game, test plan 5.9).
/// Built once from the config; safe to call from any thread.
/// </summary>
public sealed class DiscordToGameFilter
{
    private readonly bool _ignoreOtherBots;
    private readonly HashSet<ulong> _ignoredUserIds;

    /// <param name="ignoreOtherBots">The <c>IgnoreOtherBots</c> setting.</param>
    /// <param name="ignoredUserIds">The <c>IgnoredDiscordUserIds</c> setting.</param>
    public DiscordToGameFilter(bool ignoreOtherBots, IEnumerable<ulong>? ignoredUserIds)
    {
        _ignoreOtherBots = ignoreOtherBots;
        _ignoredUserIds = new HashSet<ulong>(ignoredUserIds ?? new List<ulong>());
    }

    /// <summary>
    /// Whether a message from this author goes to General. Never our own bot or a webhook, whatever the settings:
    /// either could be a line we posted coming back, and relaying it would loop (gotcha #7). Other bots only when
    /// <c>IgnoreOtherBots</c> is off, and never anyone in <c>IgnoredDiscordUserIds</c>.
    /// </summary>
    /// <param name="authorId">The Discord user ID of the author.</param>
    /// <param name="isBot">Whether the author is a bot account.</param>
    /// <param name="isWebhook">Whether the message was posted by a webhook.</param>
    /// <param name="ownBotId">The user ID of grug-bot's own bot.</param>
    public bool ShouldRelay(ulong authorId, bool isBot, bool isWebhook, ulong ownBotId)
    {
        if (authorId == ownBotId || isWebhook)
        {
            return false;
        }

        if (isBot && _ignoreOtherBots)
        {
            return false;
        }

        return !_ignoredUserIds.Contains(authorId);
    }
}
