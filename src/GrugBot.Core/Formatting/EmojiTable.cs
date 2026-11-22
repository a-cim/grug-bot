using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace GrugBot.Core.Formatting;

/// <summary>
/// The standard emoji names <see cref="StandardEmoji"/> looks up, read from <c>EmojiNames.tsv</c> (decision #47).
/// Reading never throws: a line that can't be read is skipped, and a missing file gives an empty table, which
/// leaves text as it is. <see cref="Problem"/> says what went wrong, so the mod can log it once.
/// </summary>
internal sealed class EmojiTable
{
    /// <summary>The name the table is embedded under in this assembly (see GrugBot.Core.csproj).</summary>
    public const string ResourceName = "GrugBot.Core.Formatting.EmojiNames.tsv";

    private const char ZeroWidthJoiner = '\u200D';
    private const char EmojiSelector = '\uFE0F';

    private readonly Dictionary<string, string> _names;
    private readonly HashSet<int> _singles;
    private readonly HashSet<int> _textByDefault;

    private EmojiTable(
        Dictionary<string, string> names, HashSet<int> singles, HashSet<int> textByDefault, int maxParts, string? problem)
    {
        _names = names;
        _singles = singles;
        _textByDefault = textByDefault;
        MaxParts = maxParts;
        Problem = problem;
    }

    /// <summary>The table embedded in this assembly, read once on first use.</summary>
    public static EmojiTable Embedded { get; } = LoadEmbedded();

    /// <summary>Whether the table has no emoji at all, in which case nothing is replaced.</summary>
    public bool IsEmpty => _names.Count == 0;

    /// <summary>The most parts (emoji joined by U+200D) any emoji in the table has. At least 1.</summary>
    public int MaxParts { get; }

    /// <summary>What went wrong reading the table, or <c>null</c> if every line was read.</summary>
    public string? Problem { get; }

    /// <summary>Whether the code point is an emoji on its own (perhaps needing U+FE0F), not only part of a sequence.</summary>
    public bool IsSingle(int codePoint) => _singles.Contains(codePoint);

    /// <summary>Whether the code point is text unless U+FE0F asks for an emoji: gemoji spells it with U+FE0F after it.</summary>
    public bool IsTextByDefault(int codePoint) => _textByDefault.Contains(codePoint);

    /// <summary>
    /// The name for an emoji, given without variation selectors or skin tones and with its parts joined by U+200D.
    /// </summary>
    public bool TryGetName(string key, [NotNullWhen(true)] out string? name) => _names.TryGetValue(key, out name);

    /// <summary>
    /// Reads a table in <c>EmojiNames.tsv</c>'s format: <c>#</c> comment lines, then one emoji per line as hex code
    /// points separated by spaces, a tab, and a name of <c>a-z</c>, <c>0-9</c> and <c>_</c>.
    /// </summary>
    public static EmojiTable Parse(TextReader reader)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var singles = new HashSet<int>();
        var textByDefault = new HashSet<int>();
        var codePoints = new List<int>();
        int maxParts = 1;
        int badLines = 0;
        int firstBadLine = 0;
        int lineNumber = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (!TryParseLine(line, codePoints, out string? key, out string? name))
            {
                badLines++;
                firstBadLine = firstBadLine == 0 ? lineNumber : firstBadLine;
                continue;
            }

            names[key] = name;
            maxParts = Math.Max(maxParts, key.Split(ZeroWidthJoiner).Length);

            if (codePoints.Count == 1 || (codePoints.Count == 2 && codePoints[1] == EmojiSelector))
            {
                singles.Add(codePoints[0]);
                if (codePoints.Count == 2)
                {
                    textByDefault.Add(codePoints[0]);
                }
            }
        }

        string? problem = null;
        if (badLines > 0)
        {
            problem = string.Format(
                CultureInfo.InvariantCulture,
                "{0} line(s) of the emoji table couldn't be read and were skipped, the first being line {1}.",
                badLines,
                firstBadLine);
        }
        else if (names.Count == 0)
        {
            problem = "The emoji table is empty.";
        }

        return new EmojiTable(names, singles, textByDefault, maxParts, problem);
    }

    private static EmojiTable LoadEmbedded()
    {
        try
        {
            using Stream? stream = typeof(EmojiTable).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return Empty("The embedded resource " + ResourceName + " is missing.");
            }

            using var reader = new StreamReader(stream, Encoding.ASCII);
            return Parse(reader);
        }
        catch (Exception e)
        {
            // Never let the table stop the relay: without it, standard emoji just stay as they are.
            return Empty("The emoji table couldn't be read: " + e.GetType().Name + ": " + e.Message);
        }
    }

    private static EmojiTable Empty(string problem) => new(
        new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<int>(), new HashSet<int>(), 1, problem);

    // One table line: the key (without variation selectors or skin tones) and the name, plus the code points as written.
    private static bool TryParseLine(
        string line, List<int> codePoints, [NotNullWhen(true)] out string? key, [NotNullWhen(true)] out string? name)
    {
        key = null;
        name = null;
        codePoints.Clear();

        string[] fields = line.Split('\t');
        if (fields.Length != 2 || !IsPlainName(fields[1]))
        {
            return false;
        }

        var text = new StringBuilder();
        foreach (string hex in fields[0].Split(' '))
        {
            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int codePoint)
                || codePoint is < 0 or > 0x10FFFF
                || codePoint is >= 0xD800 and <= 0xDFFF)
            {
                return false;
            }

            codePoints.Add(codePoint);
            text.Append(char.ConvertFromUtf32(codePoint));
        }

        string written = text.ToString();
        var normalised = new StringBuilder(written.Length);
        StandardEmoji.AppendWithoutModifiers(normalised, written, 0, written.Length);
        if (normalised.Length == 0)
        {
            return false;
        }

        key = normalised.ToString();
        name = fields[1];
        return true;
    }

    private static bool IsPlainName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (char c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
