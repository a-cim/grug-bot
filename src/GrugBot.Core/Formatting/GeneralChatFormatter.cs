namespace GrugBot.Core.Formatting;

/// <summary>
/// Builds the Discord line for something a player said in General: <c>**{shown name}:** {text}</c>
/// (05-message-format § General chat, Player text).
/// </summary>
public static class GeneralChatFormatter
{
    private const string NamePrefixEnd = "</strong>";

    /// <summary>
    /// Formats one General chat message for the chat channel.
    /// </summary>
    /// <param name="shownName">The name other players see for the speaker (decision #36).</param>
    /// <param name="chatMessage">
    /// The message as the game's <c>PlayerChat</c> event hands it over: already formatted, like
    /// <c>&lt;strong&gt;Grug:&lt;/strong&gt; hello</c>, with the player's text VTML-escaped (gotcha #13).
    /// </param>
    /// <param name="escapeMarkdown">The <c>EscapeMarkdown</c> setting. Off: Markdown passes through, as in th3essentials.</param>
    /// <returns>The Discord line, or <c>null</c> if nothing readable is left once the markup is removed.</returns>
    public static string? Format(string shownName, string chatMessage, bool escapeMarkdown)
    {
        string text = Vtml.ToPlainText(ExtractPlayerText(chatMessage)).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (escapeMarkdown)
        {
            text = DiscordMarkdown.Escape(text);
        }

        // The name sits inside our own bold markers, so it's always escaped, whatever the setting.
        // Escaped characters look the same on Discord, so a name like Grug_Ugg still reads as typed.
        string name = DiscordMarkdown.Escape(Vtml.ToPlainText(shownName).Trim());

        return "**" + DiscordMentions.Neutralise(name) + ":** " + DiscordMentions.Neutralise(text);
    }

    /// <summary>
    /// Returns what the player typed, without the game's <c>&lt;strong&gt;Name:&lt;/strong&gt; </c> prefix.
    /// The prefix ends at the first <c>&lt;/strong&gt;</c>; the player's own text shouldn't contain one, because
    /// the game escapes it (gotcha #13). If another mod has rewritten the message so there's no such prefix, the whole
    /// message is used: better a repeated name than a lost line.
    /// </summary>
    public static string ExtractPlayerText(string chatMessage)
    {
        int prefixEnd = chatMessage.IndexOf(NamePrefixEnd, System.StringComparison.OrdinalIgnoreCase);
        return prefixEnd < 0 ? chatMessage : chatMessage.Substring(prefixEnd + NamePrefixEnd.Length);
    }
}
