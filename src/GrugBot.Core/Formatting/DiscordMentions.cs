using System.Text.RegularExpressions;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Breaks up text that Discord would read as a mention, as a second line of defence. The first is that
/// every send uses <c>AllowedMentions.None</c>, so even an unbroken mention pings no one (gotcha #8).
/// </summary>
public static class DiscordMentions
{
    /// <summary>A zero-width space: invisible, but it stops Discord from seeing a mention.</summary>
    public const char ZeroWidthSpace = '​';

    private static readonly Regex EveryoneOrHere = new(@"@(?=everyone|here)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // User (<@123>, <@!123>) and role (<@&123>) mentions, the forms Discord turns into pings.
    private static readonly Regex UserOrRole = new(@"<(?=@[!&]?\d+>)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Puts a zero-width space after the <c>@</c> of <c>@everyone</c> and <c>@here</c>, and between the
    /// <c>&lt;</c> and <c>@</c> of user and role mentions. The text still reads the same.
    /// </summary>
    public static string Neutralise(string text)
    {
        string result = EveryoneOrHere.Replace(text, "@" + ZeroWidthSpace);
        return UserOrRole.Replace(result, "<" + ZeroWidthSpace);
    }
}
