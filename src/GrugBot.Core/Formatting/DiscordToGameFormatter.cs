using System.Text.RegularExpressions;
using GrugBot.Core.Config;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Builds the General line for a message posted in the chat channel, in th3essentials' format
/// (05-message-format § Discord → Game):
/// <c>&lt;font color="{DiscordNameColor}"&gt;&lt;strong&gt;{Discord name}: &lt;/strong&gt;&lt;/font&gt;{text}</c>.
/// </summary>
public static class DiscordToGameFormatter
{
    // What VTML's font colour takes: # and six hex digits, or eight with alpha (ColorUtil.Hex2Doubles).
    // \z, not $, so a trailing newline doesn't pass.
    private static readonly Regex NameColor = new(@"^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// The Discord name (<c>CONTEXT.md</c>): the server nickname, else the global name, else the username.
    /// A missing or blank name falls through to the next one.
    /// </summary>
    /// <param name="serverNickname">The nickname set in this Discord server; <c>null</c> for a user who isn't a server member.</param>
    /// <param name="globalName">The name the user set for all of Discord, if any.</param>
    /// <param name="username">The unique Discord username.</param>
    public static string DiscordName(string? serverNickname, string? globalName, string? username)
    {
        if (!string.IsNullOrWhiteSpace(serverNickname))
        {
            return serverNickname;
        }

        if (!string.IsNullOrWhiteSpace(globalName))
        {
            return globalName;
        }

        return username ?? "";
    }

    /// <summary>
    /// Whether <paramref name="color"/> can go into the line as the name colour: <c>#</c> followed by six or eight
    /// hex digits, like <c>#7289DA</c>.
    /// </summary>
    public static bool IsValidNameColor(string? color) => color is not null && NameColor.IsMatch(color);

    /// <summary>
    /// Formats one chat channel message for General. Both the name and the text have <c>&amp;</c>, <c>&lt;</c> and
    /// <c>&gt;</c> escaped (gotcha #20), and nothing else in them is changed.
    /// </summary>
    /// <param name="discordName">The poster's Discord name, from <see cref="DiscordName"/>. Not yet escaped.</param>
    /// <param name="text">
    /// The text to show, not yet escaped. Today that's the message content as posted; the Discord → Game rules
    /// (mentions, custom emoji, attachments, newlines and the length cap, decision #25) are applied before this.
    /// </param>
    /// <param name="nameColor">
    /// The <c>DiscordNameColor</c> setting. One that isn't a colour (see <see cref="IsValidNameColor"/>) is replaced with
    /// the default, so a typo in the config can't break every line.
    /// </param>
    /// <returns>The General line, or <c>null</c> if there's no text to show (an empty message is ignored).</returns>
    public static string? Format(string discordName, string text, string? nameColor)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string color = IsValidNameColor(nameColor) ? nameColor! : GrugBotConfig.DefaultDiscordNameColor;

        return "<font color=\"" + color + "\"><strong>" + Vtml.Escape(discordName) + ": </strong></font>" + Vtml.Escape(text);
    }
}
