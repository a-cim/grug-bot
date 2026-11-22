using System;
using System.Collections.Generic;
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
/// selector, skin tone and tag characters after it. Variation selectors and skin tones are ignored when looking up
/// the name, so thumbs up (U+1F44D) is <c>:thumbsup:</c> with or without U+FE0F and in every skin tone. The longest
/// run of parts the table knows is replaced first; a run it doesn't know is shown part by part, as an emoji font
/// without that sequence would show it, and a part it doesn't know is <see cref="Unknown"/>. Joiners, variation
/// selectors and skin tones never stay behind.
/// </para>
/// <para>
/// A character that is text by default and only an emoji when a variation selector asks for it (the copyright sign
/// U+00A9, the trade mark sign U+2122, the left right arrow U+2194, the heart U+2764 and so on) is left alone unless
/// U+FE0F, a skin tone or a joiner follows it: that's when Discord shows it as an emoji picture. On its own it is
/// text the game can draw. Other text is never changed.
/// </para>
/// <para>
/// If the table couldn't be read (see <see cref="LoadProblem"/>), text is left as it is rather than the relay
/// failing.
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

    /// <summary>
    /// What went wrong reading the embedded emoji table, or <c>null</c> if it was read whole. The mod logs it once at
    /// startup. With no table at all, <see cref="Replace(string)"/> leaves text as it is.
    /// </summary>
    public static string? LoadProblem => EmojiTable.Embedded.Problem;

    /// <summary>
    /// Replaces every standard emoji in <paramref name="text"/> with <c>:name:</c>, or with <see cref="Unknown"/> if
    /// the table doesn't know it, and drops stray variation selectors. Everything else is kept as it is.
    /// </summary>
    public static string Replace(string text) => Replace(text, EmojiTable.Embedded);

    /// <summary>As <see cref="Replace(string)"/>, with the given table.</summary>
    internal static string Replace(string text, EmojiTable table)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (table.IsEmpty)
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var parts = new List<(int Start, int End)>();
        int i = 0;
        while (i < text.Length)
        {
            int end = ReadEmoji(text, i, parts, table);
            if (end > i)
            {
                AppendNames(result, text, parts, table);
                i = end;
                continue;
            }

            char c = text[i];
            if (IsSelector(c))
            {
                // A selector after something that isn't an emoji does nothing, but the game might draw a box for it.
                i++;
                continue;
            }

            if (c == ZeroWidthJoiner)
            {
                // Joiners right before an emoji join it to nothing, so they go. Others belong to the text around them.
                int afterJoiners = SkipJoiners(text, i);
                if (ReadPart(text, afterJoiners, afterJoiner: false, table) == afterJoiners)
                {
                    result.Append(text, i, afterJoiners - i);
                }

                i = afterJoiners;
                continue;
            }

            int length = char.IsSurrogatePair(text, i) ? 2 : 1;
            result.Append(text, i, length);
            i += length;
        }

        return result.ToString();
    }

    /// <summary>
    /// Appends the characters of <paramref name="text"/> from <paramref name="start"/> to <paramref name="end"/>
    /// without variation selectors or skin tones: the form the table's keys are in.
    /// </summary>
    internal static void AppendWithoutModifiers(StringBuilder key, string text, int start, int end)
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

    // Reads the emoji starting at start, if there is one: its parts go into parts, and the index after it is
    // returned. Returns start if there's no emoji there. Joiners that lead to nothing are taken as part of the
    // emoji, so that they're dropped with it, and a run of joiners between two parts counts as one.
    private static int ReadEmoji(string text, int start, List<(int Start, int End)> parts, EmojiTable table)
    {
        parts.Clear();
        int end = ReadPart(text, start, afterJoiner: false, table);
        if (end == start)
        {
            return start;
        }

        parts.Add((start, end));
        while (end < text.Length && text[end] == ZeroWidthJoiner)
        {
            int partStart = SkipJoiners(text, end);
            int next = ReadPart(text, partStart, afterJoiner: true, table);
            if (next == partStart)
            {
                return partStart;
            }

            parts.Add((partStart, next));
            end = next;
        }

        return end;
    }

    // Reads one part of an emoji (see the remarks on the class) and returns the index after it, or start if there is
    // no emoji part there. After a joiner, a text-by-default character counts without a selector, such as the male
    // sign U+2642 in runner, joiner, male sign.
    private static int ReadPart(string text, int start, bool afterJoiner, EmojiTable table)
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

        if (!table.IsSingle(first) && !IsInEmojiBlock(first))
        {
            return start;
        }

        if (!afterJoiner && first <= char.MaxValue && table.IsTextByDefault(first) && !AsksForEmoji(text, end))
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

    // Whether what's at index makes the text-by-default character before it an emoji: U+FE0F, a skin tone or a joiner.
    private static bool AsksForEmoji(string text, int index) =>
        index < text.Length
        && (text[index] == EmojiSelector || text[index] == ZeroWidthJoiner || IsSkinToneAt(text, index));

    // Appends the names of one emoji's parts: the longest run of parts the table knows first, then the rest. A run is
    // never longer than the table's longest emoji, so a long chain of joined emoji takes time in proportion to its
    // length.
    private static void AppendNames(StringBuilder result, string text, List<(int Start, int End)> parts, EmojiTable table)
    {
        var partKeys = new string[parts.Count];
        var key = new StringBuilder();
        for (int p = 0; p < parts.Count; p++)
        {
            key.Clear();
            AppendWithoutModifiers(key, text, parts[p].Start, parts[p].End);
            partKeys[p] = key.ToString();
        }

        int from = 0;
        while (from < parts.Count)
        {
            string? name = null;
            int to = Math.Min(parts.Count, from + table.MaxParts);
            for (; to > from; to--)
            {
                key.Clear();
                for (int p = from; p < to; p++)
                {
                    if (p > from)
                    {
                        key.Append(ZeroWidthJoiner);
                    }

                    key.Append(partKeys[p]);
                }

                if (table.TryGetName(key.ToString(), out name))
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

    private static int SkipJoiners(string text, int index)
    {
        while (index < text.Length && text[index] == ZeroWidthJoiner)
        {
            index++;
        }

        return index;
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
}
