using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Turns a chat channel message into the text shown after the Discord name in General (decisions #25 and #41,
/// 05-message-format § Discord → Game): mentions become names, custom emoji become <c>:name:</c>, attachments and
/// stickers get a short placeholder, and the result is one line of at most <see cref="MaxLength"/> characters.
/// </summary>
/// <remarks>
/// The result is not escaped. <see cref="DiscordToGameFormatter.Format"/> escapes it last, so resolved names and
/// placeholders are escaped too, and the length cap never cuts an escape like <c>&amp;lt;</c> in half: the cap
/// counts the characters players see.
/// </remarks>
public static class DiscordToGameText
{
    /// <summary>
    /// The most characters the text may have, placeholders included. The Discord name in front of it isn't counted.
    /// </summary>
    public const int MaxLength = 500;

    /// <summary>Ends text that was cut at <see cref="MaxLength"/>.</summary>
    public const string Ellipsis = "…";

    /// <summary>A user mention whose name can't be found, as Discord itself shows it.</summary>
    public const string UnknownUser = "@unknown-user";

    /// <summary>A role mention whose role can't be found, for example a deleted role.</summary>
    public const string UnknownRole = "@unknown-role";

    /// <summary>A channel mention whose channel can't be found, for example one the bot can't see.</summary>
    public const string UnknownChannel = "#unknown-channel";

    // Discord's message formatting: <@123> and <@!123> for a user, <@&123> for a role, <#123> for a channel, and
    // <:name:123> or <a:name:123> (animated) for a custom emoji. Anything else in angle brackets, such as a
    // <t:1700000000:R> timestamp, is left as typed (decision #25). Snowflakes are at most 20 digits.
    private static readonly Regex Token = new(
        @"<(?:@!?(?<user>[0-9]{1,20})|@&(?<role>[0-9]{1,20})|#(?<channel>[0-9]{1,20})|a?:(?<emoji>\w+):[0-9]{1,20})>",
        RegexOptions.CultureInvariant);

    // CRLF counts as one line break.
    private static readonly Regex LineBreak = new(@"\r\n|[\n\r\u0085\u2028\u2029]", RegexOptions.CultureInvariant);

    /// <summary>
    /// The IDs of the users <paramref name="content"/> mentions, each once, in the order they first appear. The mod
    /// uses this to see which names it still has to look up before calling <see cref="Build"/>.
    /// </summary>
    public static IReadOnlyList<ulong> FindUserMentionIds(string content)
    {
        var ids = new List<ulong>();
        foreach (Match match in Token.Matches(content))
        {
            Group user = match.Groups["user"];
            if (user.Success && TryParseId(user.Value, out ulong id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Builds the text for one message, not yet escaped: <see cref="ResolveMentions"/>, then <see cref="ToOneLine"/>,
    /// then trimmed, then the <see cref="Placeholders"/> after it, all within <see cref="MaxLength"/>. If the text has
    /// to be cut, it's the message's own text that is cut (ending in <see cref="Ellipsis"/>) and the placeholders are kept.
    /// </summary>
    /// <param name="content">The message content exactly as posted. A reply's content is only its own text.</param>
    /// <param name="lookups">Names for the mentioned users, roles and channels.</param>
    /// <param name="attachmentCount">How many files the message has attached (images included).</param>
    /// <param name="stickerCount">How many stickers the message has.</param>
    /// <returns>The text to show, or an empty string if there's nothing to show (the message is then ignored).</returns>
    public static string Build(string? content, MentionLookups lookups, int attachmentCount, int stickerCount)
    {
        string text = ToOneLine(ResolveMentions(content ?? "", lookups)).Trim();
        string placeholders = Placeholders(attachmentCount, stickerCount);

        if (placeholders.Length == 0)
        {
            return Cap(text, MaxLength);
        }

        if (text.Length == 0)
        {
            return placeholders;
        }

        string suffix = " " + placeholders;
        return Cap(text, MaxLength - suffix.Length) + suffix;
    }

    /// <summary>
    /// Replaces user, role and channel mentions with <c>@Name</c>, <c>@Role</c> and <c>#channel</c>, and custom emoji
    /// (still or animated) with <c>:name:</c>. A mention whose name isn't known becomes <see cref="UnknownUser"/>,
    /// <see cref="UnknownRole"/> or <see cref="UnknownChannel"/>. One pass, so a name that itself looks like a mention
    /// is left as it is.
    /// </summary>
    public static string ResolveMentions(string content, MentionLookups lookups)
    {
        if (lookups is null)
        {
            throw new ArgumentNullException(nameof(lookups));
        }

        return Token.Replace(content, match =>
        {
            Group emoji = match.Groups["emoji"];
            if (emoji.Success)
            {
                return ":" + emoji.Value + ":";
            }

            Group user = match.Groups["user"];
            Group role = match.Groups["role"];
            Group group = user.Success ? user : role.Success ? role : match.Groups["channel"];
            if (!TryParseId(group.Value, out ulong id))
            {
                // Too big for a snowflake, so not a real mention.
                return match.Value;
            }

            if (user.Success)
            {
                return Named("@", lookups.UserName(id), UnknownUser);
            }

            return role.Success
                ? Named("@", lookups.RoleName(id), UnknownRole)
                : Named("#", lookups.ChannelName(id), UnknownChannel);
        });
    }

    /// <summary>Turns every line break into a space: a General line is a single line (decision #25).</summary>
    public static string ToOneLine(string text) => LineBreak.Replace(text, " ");

    /// <summary>
    /// What stands in for the files and stickers a message has: <c>[Attachment]</c> or <c>[n Attachments]</c>, then
    /// <c>[Sticker]</c> or <c>[n Stickers]</c>, joined by a space. Empty if there are none.
    /// </summary>
    public static string Placeholders(int attachmentCount, int stickerCount)
    {
        string attachments = Placeholder(attachmentCount, "Attachment");
        string stickers = Placeholder(stickerCount, "Sticker");
        if (attachments.Length == 0 || stickers.Length == 0)
        {
            return attachments + stickers;
        }

        return attachments + " " + stickers;
    }

    /// <summary>
    /// Shortens <paramref name="text"/> to at most <paramref name="maxLength"/> characters, ending in
    /// <see cref="Ellipsis"/> when it was cut. Never splits an emoji or other character made of two UTF-16 halves.
    /// Text that fits is returned as it is.
    /// </summary>
    public static string Cap(string text, int maxLength)
    {
        if (maxLength < Ellipsis.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "Must leave room for the ellipsis.");
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        int keep = maxLength - Ellipsis.Length;
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        return text.Substring(0, keep).TrimEnd() + Ellipsis;
    }

    private static string Placeholder(int count, string noun) => count switch
    {
        <= 0 => "",
        1 => "[" + noun + "]",
        _ => "[" + count.ToString(CultureInfo.InvariantCulture) + " " + noun + "s]",
    };

    private static string Named(string sigil, string? name, string unknown) =>
        string.IsNullOrWhiteSpace(name) ? unknown : sigil + name;

    private static bool TryParseId(string digits, out ulong id) =>
        ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out id);
}
