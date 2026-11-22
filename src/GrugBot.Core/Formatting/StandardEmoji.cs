using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Turns standard emoji (the Unicode ones from Discord's own picker, which Discord sends as plain text) into
/// <c>:name:</c>, the way custom emoji already show (decision #47, 05-message-format § Discord → Game). Vintage
/// Story's chat font has no emoji, so they would otherwise show as boxes.
/// </summary>
/// <remarks>
/// <para>
/// The names come from <c>EmojiNames.tsv</c>, generated from GitHub's gemoji by <c>tools/generate-emoji-names.py</c>
/// and embedded in this assembly.
/// </para>
/// <para>
/// An emoji is a run of parts joined by zero-width joiners (U+200D). A part is a flag (two regional indicator
/// letters), a keycap (<c>0</c>-<c>9</c>, <c>#</c> or <c>*</c>, then U+20E3), or an emoji with an optional variation
/// selector, skin tone and tag characters after it. Variation selectors and skin tones are ignored when looking up the
/// name, so thumbs up (U+1F44D) is <c>:thumbsup:</c> with or without U+FE0F and in every skin tone. The longest run of parts the table knows is
/// replaced first; a run it doesn't know is shown part by part, as an emoji font without that sequence would show it,
/// and a part it doesn't know is <see cref="Unknown"/>. Joiners, variation selectors and skin tones never stay behind.
/// </para>
/// <para>
/// A character that is text by default and only an emoji when a variation selector asks for it (the copyright sign
/// U+00A9, the trade mark sign U+2122, the left right arrow U+2194, the heart U+2764 and so on) is left alone unless U+FE0F, a skin tone or a joiner follows it: that's when Discord shows it
/// as an emoji picture. On its own it is text the game can draw. Other text is never changed.
/// </para>
/// </remarks>
public static class StandardEmoji
{
    /// <summary>What an emoji the table doesn't know is shown as.</summary>
    public const string Unknown = ":emoji:";

    private const char ZeroWidthJoiner = '\u200D';
    private const char EmojiSelector = '\uFE0F';
    private const char TextSelector = '\uFE0E';
    private const char KeycapMark = '\u20E3';

    private const string ResourceName = "GrugBot.Core.Formatting.EmojiNames.tsv";

    /// <summary>
    /// Replaces every standard emoji in <paramref name="text"/> with <c>:name:</c>, or with <see cref="Unknown"/> if
    /// the table doesn't know it, and drops stray variation selectors. Everything else is kept as it is.
    /// </summary>
    public static string Replace(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var result = new StringBuilder(text.Length);
        var parts = new List<(int Start, int End)>();
        int i = 0;
        while (i < text.Length)
        {
            int end = ReadEmoji(text, i, parts);
            if (end > i)
            {
                AppendNames(result, text, parts);
                i = end;
                continue;
            }

            char c = text[i];
            if (c == EmojiSelector || c == TextSelector)
            {
                // A selector after something that isn't an emoji does nothing, but the game might draw a box for it.
                i++;
                continue;
            }

            int length = char.IsSurrogatePair(text, i) ? 2 : 1;
            result.Append(text, i, length);
            i += length;
        }

        return result.ToString();
    }

    // Reads the emoji starting at start, if there is one: its parts go into parts, and the index after it is
    // returned. Returns start if there's no emoji there. A joiner that leads to nothing is taken as part of the
    // emoji, so that it's dropped with it.
    private static int ReadEmoji(string text, int start, List<(int Start, int End)> parts)
    {
        parts.Clear();
        int end = ReadPart(text, start, afterJoiner: false);
        if (end == start)
        {
            return start;
        }

        parts.Add((start, end));
        while (end < text.Length && text[end] == ZeroWidthJoiner)
        {
            int next = ReadPart(text, end + 1, afterJoiner: true);
            if (next == end + 1)
            {
                return end + 1;
            }

            parts.Add((end + 1, next));
            end = next;
        }

        return end;
    }

    // Reads one part of an emoji (see the remarks on the class) and returns the index after it, or start if there is
    // no emoji part there. After a joiner, a text-by-default character counts without a selector, such as the male
    // sign U+2642 in runner, joiner, male sign.
    private static int ReadPart(string text, int start, bool afterJoiner)
    {
        if (!TryCodePointAt(text, start, out int first, out int length))
        {
            return start;
        }

        int end = start + length;

        if (IsRegionalIndicator(first))
        {
            // A flag is a pair of letters. A lone letter is still an emoji part, just not one the table knows.
            if (TryCodePointAt(text, end, out int second, out int secondLength) && IsRegionalIndicator(second))
            {
                end += secondLength;
            }

            return end;
        }

        if (first is (>= 0x30 and <= 0x39) or 0x23 or 0x2A) // 0-9, # or *
        {
            // Only a keycap: the digit, an optional selector, then U+20E3. A plain digit is text.
            if (end < text.Length && IsSelector(text[end]))
            {
                end++;
            }

            return end < text.Length && text[end] == KeycapMark ? end + 1 : start;
        }

        bool known = Table.Instance.IsSingle(first);
        if (!known && !IsInEmojiBlock(first))
        {
            return start;
        }

        if (!afterJoiner && first <= char.MaxValue && Table.Instance.IsTextByDefault(first)
            && !(end < text.Length && (text[end] == EmojiSelector || text[end] == ZeroWidthJoiner || IsSkinToneAt(text, end))))
        {
            return start;
        }

        if (end < text.Length && IsSelector(text[end]))
        {
            end++;
        }

        if (IsSkinToneAt(text, end))
        {
            end += 2;
            if (end < text.Length && text[end] == EmojiSelector)
            {
                end++;
            }
        }

        // Tag characters spell out a region after a black flag, as in the flag of England.
        while (TryCodePointAt(text, end, out int tag, out int tagLength) && tag is >= 0xE0020 and <= 0xE007F)
        {
            end += tagLength;
        }

        return end;
    }

    // Appends the names of one emoji's parts: the longest run of parts the table knows first, then the rest.
    private static void AppendNames(StringBuilder result, string text, List<(int Start, int End)> parts)
    {
        int from = 0;
        while (from < parts.Count)
        {
            int to = parts.Count;
            string? name = null;
            for (; to > from; to--)
            {
                if (Table.Instance.TryGetName(Key(text, parts, from, to), out name))
                {
                    break;
                }
            }

            if (name is null)
            {
                result.Append(Unknown);
                from++;
                continue;
            }

            result.Append(':').Append(name).Append(':');
            from = to;
        }
    }

    // Parts from to to (exclusive), joined by joiners, without variation selectors or skin tones.
    private static string Key(string text, List<(int Start, int End)> parts, int from, int to)
    {
        var key = new StringBuilder();
        for (int p = from; p < to; p++)
        {
            if (p > from)
            {
                key.Append(ZeroWidthJoiner);
            }

            AppendWithoutModifiers(key, text, parts[p].Start, parts[p].End);
        }

        return key.ToString();
    }

    private static void AppendWithoutModifiers(StringBuilder key, string text, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (IsSelector(text[i]))
            {
                continue;
            }

            if (IsSkinToneAt(text, i))
            {
                i++;
                continue;
            }

            key.Append(text[i]);
        }
    }

    private static bool IsSelector(char c) => c == EmojiSelector || c == TextSelector;

    // Skin tones U+1F3FB to U+1F3FF, which are D83C DFFB to D83C DFFF in UTF-16.
    private static bool IsSkinToneAt(string text, int index) =>
        index + 1 < text.Length && text[index] == '\uD83C' && text[index + 1] is >= '\uDFFB' and <= '\uDFFF';

    private static bool IsRegionalIndicator(int codePoint) => codePoint is >= 0x1F1E6 and <= 0x1F1FF;

    // The Unicode blocks emoji come from, outside the Basic Multilingual Plane. A character from one that the table
    // doesn't know, such as an emoji newer than the table, is Unknown rather than a box. Every emoji in the Basic
    // Multilingual Plane is old enough to be in the table, so no block there is needed.
    private static bool IsInEmojiBlock(int codePoint) => codePoint switch
    {
        >= 0x1F000 and <= 0x1F0FF => true, // Mahjong tiles, dominoes, playing cards
        >= 0x1F300 and <= 0x1F64F => true, // Miscellaneous symbols and pictographs (skin tones included), emoticons
        >= 0x1F680 and <= 0x1F6FF => true, // Transport and map symbols
        >= 0x1F7E0 and <= 0x1F7FF => true, // Coloured circles and squares
        >= 0x1F900 and <= 0x1F9FF => true, // Supplemental symbols and pictographs
        >= 0x1FA70 and <= 0x1FAFF => true, // Symbols and pictographs extended-A
        _ => false,
    };

    private static bool TryCodePointAt(string text, int index, out int codePoint, out int length)
    {
        codePoint = 0;
        length = 0;
        if (index >= text.Length)
        {
            return false;
        }

        if (char.IsSurrogatePair(text, index))
        {
            codePoint = char.ConvertToUtf32(text[index], text[index + 1]);
            length = 2;
            return true;
        }

        if (char.IsSurrogate(text[index]))
        {
            // Half of a pair on its own isn't a character.
            return false;
        }

        codePoint = text[index];
        length = 1;
        return true;
    }

    // The embedded table, read once on first use.
    private sealed class Table
    {
        public static readonly Table Instance = Load();

        private readonly Dictionary<string, string> _names;
        private readonly HashSet<int> _singles;
        private readonly HashSet<int> _textByDefault;

        private Table(Dictionary<string, string> names, HashSet<int> singles, HashSet<int> textByDefault)
        {
            _names = names;
            _singles = singles;
            _textByDefault = textByDefault;
        }

        // Whether the code point is an emoji on its own (possibly needing U+FE0F), not only a part of a sequence.
        public bool IsSingle(int codePoint) => _singles.Contains(codePoint);

        // Whether the code point is text unless U+FE0F asks for an emoji: gemoji spells it with U+FE0F after it.
        public bool IsTextByDefault(int codePoint) => _textByDefault.Contains(codePoint);

        public bool TryGetName(string key, [NotNullWhen(true)] out string? name) => _names.TryGetValue(key, out name);

        private static Table Load()
        {
            using Stream stream = typeof(StandardEmoji).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("The embedded resource " + ResourceName + " is missing.");
            using var reader = new StreamReader(stream, Encoding.ASCII);

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var singles = new HashSet<int>();
            var textByDefault = new HashSet<int>();
            var codePoints = new List<int>();

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string[] fields = line.Split('\t');
                if (fields.Length != 2)
                {
                    throw new InvalidOperationException("Bad line in " + ResourceName + ": " + line);
                }

                codePoints.Clear();
                foreach (string hex in fields[0].Split(' '))
                {
                    codePoints.Add(int.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                }

                var key = new StringBuilder();
                foreach (int codePoint in codePoints)
                {
                    key.Append(char.ConvertFromUtf32(codePoint));
                }

                var keyText = key.ToString();
                var normalised = new StringBuilder();
                AppendWithoutModifiers(normalised, keyText, 0, keyText.Length);
                names[normalised.ToString()] = fields[1];

                if (codePoints.Count == 1 || (codePoints.Count == 2 && codePoints[1] == EmojiSelector))
                {
                    singles.Add(codePoints[0]);
                    if (codePoints.Count == 2)
                    {
                        textByDefault.Add(codePoints[0]);
                    }
                }
            }

            return new Table(names, singles, textByDefault);
        }
    }
}
