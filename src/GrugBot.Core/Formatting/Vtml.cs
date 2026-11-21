using System.Text;
using System.Text.RegularExpressions;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Turns Vintage Story's chat markup (VTML) into plain text. Written from scratch for grug-bot:
/// thebasics has similar helpers, but it is GPL-2.0 and none of its code may be copied (ADR 0002).
/// </summary>
public static class Vtml
{
    // A line break tag (<br>, <br/>, <br />) becomes a space so the words on either side stay apart.
    private static readonly Regex LineBreakTag = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Anything that looks like an opening or closing tag: <strong>, </font>, <font color="#fff">, <icon name=x/>.
    // A "<" not directly followed by a letter or "/" (as in "1 < 2") is left alone.
    private static readonly Regex Tag = new(@"</?[A-Za-z][^<>]*>", RegexOptions.CultureInvariant);

    // Only the three entities the game produces (05-message-format § Player text). One pass, so
    // "&amp;lt;" becomes the literal text "&lt;" and is not unescaped twice.
    private static readonly Regex Entity = new("&(lt|gt|amp);", RegexOptions.CultureInvariant);

    /// <summary>Removes VTML tags, keeping the text between them.</summary>
    public static string StripTags(string text)
    {
        string withoutBreaks = LineBreakTag.Replace(text, " ");
        return Tag.Replace(withoutBreaks, "");
    }

    /// <summary>Turns <c>&amp;lt;</c>, <c>&amp;gt;</c> and <c>&amp;amp;</c> back into <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c>.</summary>
    public static string Unescape(string text) => Entity.Replace(text, match => match.Groups[1].Value switch
    {
        "lt" => "<",
        "gt" => ">",
        _ => "&",
    });

    /// <summary>
    /// Strips tags first, then unescapes. In that order, text a player typed as <c>&lt;b&gt;</c> (which the
    /// game escapes) survives as readable text instead of being mistaken for a tag.
    /// </summary>
    public static string ToPlainText(string text) => Unescape(StripTags(text));

    /// <summary>
    /// Turns <c>&lt;</c> and <c>&gt;</c> into <c>&amp;lt;</c> and <c>&amp;gt;</c>, so text going into the game can't open
    /// or close a tag (gotcha #20). <c>&amp;</c> is left as it is: the game's chat shows <c>&amp;amp;</c> literally
    /// rather than as <c>&amp;</c> (owner's test 5.2), and a lone <c>&amp;</c> can't open a tag (decision #46).
    /// </summary>
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case '<':
                    escaped.Append("&lt;");
                    break;
                case '>':
                    escaped.Append("&gt;");
                    break;
                default:
                    escaped.Append(c);
                    break;
            }
        }

        return escaped.ToString();
    }
}
