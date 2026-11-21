using System.Text;

namespace GrugBot.Core.Formatting;

/// <summary>Escapes Discord Markdown so text shows literally (gotcha #9).</summary>
public static class DiscordMarkdown
{
    // Discord treats a backslash before any ASCII punctuation as an escape and hides the backslash,
    // so escaping a character that wouldn't have formatted anything is harmless.
    // Covered: \ (first, so our own backslashes aren't doubled later), bold/italic/underline (* _),
    // strikethrough (~), code (`), spoilers (|), quotes (>), headings (#), lists (-) and masked links ([ ]).
    // Not escaped: . : / ( ), which would break links people paste.
    private const string SpecialCharacters = "\\*_~`|>#-[]";

    /// <summary>Puts a backslash before every character that Discord Markdown could act on.</summary>
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (SpecialCharacters.IndexOf(c) >= 0)
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
