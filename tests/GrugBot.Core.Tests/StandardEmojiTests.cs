using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

// Emoji are written as escapes, never as literal characters: several of them (joiners, variation selectors) are
// invisible, and a literal one is easy to lose or break in an editor.
public class StandardEmojiTests
{
    private const string ThumbsUp = "\U0001F44D";
    private const string Joy = "\U0001F602";
    private const string Vs16 = "\uFE0F";
    private const string MediumSkinTone = "\U0001F3FD";

    private static string Replace(string text) => StandardEmoji.Replace(text);

    // Single emoji

    [Theory]
    [InlineData("\U0001F44D", ":thumbsup:")]
    [InlineData("\U0001F602", ":joy:")]
    [InlineData("\U0001F9B4", ":bone:")]
    [InlineData("\U0001F4AF", ":100:")]
    [InlineData("\U0001F600", ":grinning:")]
    [InlineData("\U0001F642", ":slight_smile:")]
    [InlineData("\u2705", ":white_check_mark:")]
    [InlineData("\u2764\uFE0F", ":heart:")]
    public void A_standard_emoji_becomes_colon_name_colon(string emoji, string expected)
    {
        Assert.Equal(expected, Replace(emoji));
    }

    [Fact]
    public void The_variation_selector_may_be_there_or_not()
    {
        Assert.Equal(":thumbsup:", Replace(ThumbsUp + Vs16));
        Assert.Equal(":thumbsup:", Replace(ThumbsUp));
    }

    [Fact]
    public void Emoji_next_to_each_other_each_get_their_name()
    {
        Assert.Equal(":thumbsup::joy::joy:", Replace(ThumbsUp + Joy + Joy));
    }

    // Skin tones: the base name, no tone suffix

    [Theory]
    [InlineData("\U0001F3FB")]
    [InlineData("\U0001F3FC")]
    [InlineData("\U0001F3FD")]
    [InlineData("\U0001F3FE")]
    [InlineData("\U0001F3FF")]
    public void A_skin_tone_shows_the_base_name(string tone)
    {
        Assert.Equal(":thumbsup:", Replace(ThumbsUp + tone));
    }

    [Theory]
    [InlineData("\u270C\U0001F3FD", ":v:")] // victory hand is text by default, but a skin tone makes it an emoji
    [InlineData("\u270C\uFE0F\U0001F3FD", ":v:")]
    [InlineData("\U0001F926\U0001F3FD", ":person_facepalming:")]
    [InlineData("\U0001F469\U0001F3FD\u200D\U0001F4BB", ":woman_technologist:")]
    public void A_skin_tone_inside_an_emoji_is_dropped_too(string emoji, string expected)
    {
        Assert.Equal(expected, Replace(emoji));
    }

    [Fact]
    public void A_skin_tone_on_its_own_is_an_unknown_emoji()
    {
        Assert.Equal("a :emoji:", Replace("a " + MediumSkinTone));
    }

    // ZWJ sequences

    [Theory]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467", ":family_man_woman_girl:")]
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308", ":rainbow_flag:")]
    [InlineData("\U0001F3F3\u200D\U0001F308", ":rainbow_flag:")]
    [InlineData("\u2764\uFE0F\u200D\U0001F525", ":heart_on_fire:")]
    [InlineData("\u2764\u200D\U0001F525", ":heart_on_fire:")]
    public void A_zwj_sequence_is_matched_whole(string emoji, string expected)
    {
        Assert.Equal(expected, Replace(emoji));
    }

    [Fact]
    public void A_zwj_sequence_the_table_does_not_know_is_shown_part_by_part()
    {
        // Phoenix (bird, joiner, fire) is newer than the table: an emoji font without it shows a bird and a fire.
        Assert.Equal(":bird::fire:", Replace("\U0001F426\u200D\U0001F525"));
    }

    [Fact]
    public void The_longest_known_run_of_a_sequence_wins()
    {
        // A man, a woman and a girl joined to a frog: the family is known, the frog is added on.
        Assert.Equal(
            ":family_man_woman_girl::frog:",
            Replace("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F438"));
    }

    [Fact]
    public void A_joiner_leading_nowhere_is_dropped_with_the_emoji()
    {
        Assert.Equal(":grinning:a", Replace("\U0001F600\u200Da"));
    }

    // Flags

    [Theory]
    [InlineData("\U0001F1F3\U0001F1F4", ":flag_no:")]
    [InlineData("\U0001F1FA\U0001F1F8", ":flag_us:")]
    [InlineData("\U0001F1F3\U0001F1F4\U0001F1F8\U0001F1EA", ":flag_no::flag_se:")]
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", ":england:")]
    public void A_flag_becomes_its_name(string flag, string expected)
    {
        Assert.Equal(expected, Replace(flag));
    }

    [Fact]
    public void A_lone_regional_indicator_is_an_unknown_emoji()
    {
        Assert.Equal(":flag_no::emoji:", Replace("\U0001F1F3\U0001F1F4\U0001F1F8"));
    }

    // Keycaps

    [Theory]
    [InlineData("1\uFE0F\u20E3", ":one:")]
    [InlineData("1\u20E3", ":one:")]
    [InlineData("#\uFE0F\u20E3", ":hash:")]
    [InlineData("*\uFE0F\u20E3", ":asterisk:")]
    [InlineData("\U0001F51F", ":keycap_ten:")]
    public void A_keycap_becomes_its_name(string keycap, string expected)
    {
        Assert.Equal(expected, Replace(keycap));
    }

    [Theory]
    [InlineData("100")]
    [InlineData("#1 *bold*")]
    public void Digits_hash_and_star_on_their_own_are_text(string text)
    {
        Assert.Equal(text, Replace(text));
    }

    [Fact]
    public void A_digit_with_only_a_variation_selector_is_the_digit()
    {
        Assert.Equal("1", Replace("1\uFE0F"));
    }

    // Unknown emoji

    [Theory]
    [InlineData("\U0001FAE9")] // face with bags under eyes, newer than the table
    [InlineData("\U0001FAE9\U0001F3FD")]
    public void An_emoji_not_in_the_table_is_emoji(string emoji)
    {
        Assert.Equal(":emoji:", Replace(emoji));
    }

    // No stray invisible characters

    [Theory]
    [InlineData("\U0001F44D\uFE0F\U0001F3FD\uFE0F")]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D")]
    [InlineData("\U0001F426\u200D\U0001F525\uFE0F")]
    [InlineData("\U0001FAE9\u200D\U0001FAE9")]
    [InlineData("1\uFE0F\u20E3\uFE0F")]
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308\uFE0F")]
    public void No_joiner_selector_or_skin_tone_is_left_behind(string emoji)
    {
        string result = Replace("a " + emoji + " b");

        Assert.StartsWith("a :", result, StringComparison.Ordinal);
        Assert.EndsWith(": b", result, StringComparison.Ordinal);
        Assert.True(result.All(c => c < 0x80), "Not plain ASCII: " + Hex(result));
    }

    [Theory]
    [InlineData("a\uFE0Fb", "ab")]
    [InlineData("a\uFE0Eb", "ab")]
    [InlineData("\uFE0F", "")]
    public void A_stray_variation_selector_is_dropped(string text, string expected)
    {
        Assert.Equal(expected, Replace(text));
    }

    // Text that isn't emoji

    [Theory]
    [InlineData("\u00A9 2026")] // copyright sign
    [InlineData("\u00AE")] // registered sign
    [InlineData("\u2122")] // trade mark sign
    [InlineData("a \u2192 b")] // rightwards arrow
    [InlineData("wait\u2026")] // ellipsis
    [InlineData("\u2194 \u2B06 \u25B6")] // arrows and a triangle that are emoji only with U+FE0F
    [InlineData("\u2764 \u2665 \u2605 \u263A")] // heart, heart suit, star, smiling face, all text by default
    [InlineData("<3 :) :thumbsup:")]
    [InlineData("a\u200Db")] // a joiner between letters belongs to the text
    [InlineData("\U0001D538")] // mathematical double-struck A: two UTF-16 halves, but not an emoji
    [InlineData("\u65E5\u672C")]
    [InlineData("")]
    public void Text_that_is_not_emoji_is_untouched(string text)
    {
        Assert.Equal(text, Replace(text));
    }

    [Theory]
    [InlineData("\u00A9\uFE0F", ":copyright:")]
    [InlineData("\u00AE\uFE0F", ":registered:")]
    [InlineData("\u2122\uFE0F", ":tm:")]
    [InlineData("\u2665\uFE0F", ":hearts:")]
    public void A_text_symbol_with_the_emoji_selector_is_an_emoji(string text, string expected)
    {
        Assert.Equal(expected, Replace(text));
    }

    [Fact]
    public void A_text_symbol_with_the_text_selector_stays_text()
    {
        Assert.Equal("\u2764", Replace("\u2764\uFE0E"));
    }

    [Fact]
    public void A_lone_surrogate_half_is_kept()
    {
        Assert.Equal("a\uD83Cb", Replace("a\uD83Cb"));
    }

    // Leftover joiners

    [Theory]
    [InlineData("a\u200D\U0001F600", "a:grinning:")]
    [InlineData("a\u200D\u200D\U0001F600 b", "a:grinning: b")]
    [InlineData("\U0001F44D\u200D\u200D\U0001F44D", ":thumbsup::thumbsup:")]
    [InlineData("\U0001F600\u200D\u200D", ":grinning:")]
    [InlineData("\U0001F468\u200D\u200D\U0001F469\u200D\U0001F467", ":family_man_woman_girl:")]
    public void Joiners_before_or_between_emoji_are_not_left_behind(string text, string expected)
    {
        Assert.Equal(expected, Replace(text));
    }

    [Theory]
    [InlineData("a\u200D\u200Db")]
    [InlineData("\u200D")]
    [InlineData("a\u200D\u00A9")] // the copyright sign on its own is text, so the joiner belongs to the text
    public void Joiners_between_text_stay(string text)
    {
        Assert.Equal(text, Replace(text));
    }

    // Long text

    [Fact]
    public void A_long_chain_of_joined_emoji_takes_time_in_proportion_to_its_length()
    {
        // About 4,000 characters: 1,333 grinning faces joined by U+200D. Each run tried is at most as long as the
        // longest emoji in the table, so this is quick; trying every run of the chain would take seconds.
        string text = string.Join("\u200D", Enumerable.Repeat("\U0001F600", 1333));
        var stopwatch = Stopwatch.StartNew();

        string result = Replace(text);

        stopwatch.Stop();
        Assert.Equal(string.Concat(Enumerable.Repeat(":grinning:", 1333)), result);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "Took " + stopwatch.Elapsed);
    }

    // A broken table fails safe

    [Fact]
    public void The_embedded_table_is_read_without_problems()
    {
        Assert.Null(StandardEmoji.LoadProblem);
    }

    [Fact]
    public void With_an_empty_table_text_is_left_as_it_is()
    {
        EmojiTable table = EmojiTable.Parse(new StringReader("# nothing here\n"));
        string text = "hi " + ThumbsUp + Vs16 + " \U0001FAE9 \u00A9\uFE0F";

        Assert.True(table.IsEmpty);
        Assert.NotNull(table.Problem);
        Assert.Equal(text, StandardEmoji.Replace(text, table));
    }

    [Fact]
    public void Lines_that_cannot_be_read_are_skipped_and_the_rest_still_work()
    {
        string tsv = string.Join(
            "\n",
            "# a comment",
            "1F44D\tthumbsup",
            "not hex\tbad",
            "1F602",
            "1F600\tNot<Plain>",
            "D83D\tsurrogate",
            "110000\ttoo_big",
            "1F468 200D 1F469 200D 1F467\tfamily_man_woman_girl",
            "1F602\tjoy");
        EmojiTable table = EmojiTable.Parse(new StringReader(tsv));

        Assert.Equal(":thumbsup: :joy: :family_man_woman_girl: :emoji:", StandardEmoji.Replace(
            ThumbsUp + " " + Joy + " \U0001F468\u200D\U0001F469\u200D\U0001F467 \U0001F600", table));
        Assert.Equal(3, table.MaxParts);
        Assert.Contains("5 line(s)", table.Problem, StringComparison.Ordinal);
        Assert.Contains("line 3", table.Problem, StringComparison.Ordinal);
    }

    // Mixed text

    [Fact]
    public void Mixed_text_keeps_everything_that_is_not_emoji()
    {
        string text = "gg " + ThumbsUp + MediumSkinTone + " nice " + Joy + Joy + " \u00A9 2026 \U0001F1F3\U0001F1F4!";

        Assert.Equal("gg :thumbsup: nice :joy::joy: \u00A9 2026 :flag_no:!", Replace(text));
    }

    // The whole table

    [Fact]
    public void Every_emoji_in_the_table_becomes_its_name_with_or_without_selectors()
    {
        List<(string Emoji, string Name, bool TextByDefault)> table = ReadTable();
        Assert.True(table.Count > 1800, "Only " + table.Count + " emoji in the table.");

        var failures = new List<string>();
        foreach ((string emoji, string name, bool textByDefault) in table)
        {
            string expected = ":" + name + ":";
            string bare = emoji.Replace(Vs16, "", StringComparison.Ordinal);

            if (Replace(emoji) != expected)
            {
                failures.Add(Hex(emoji) + " -> " + Replace(emoji));
            }

            // A text-by-default symbol without its selector is text, and stays as it is.
            string expectedBare = textByDefault && bare.Length <= 2 && !char.IsSurrogate(bare[0]) ? bare : expected;
            if (Replace(bare) != expectedBare)
            {
                failures.Add(Hex(bare) + " -> " + Replace(bare));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    [Fact]
    public void Table_names_are_plain_and_unique()
    {
        List<(string Emoji, string Name, bool TextByDefault)> table = ReadTable();

        Assert.All(table, entry => Assert.Matches("^[a-z0-9_]+$", entry.Name));
        Assert.Equal(table.Count, table.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count());
    }

    // Reads the embedded table directly, as gemoji spells each emoji.
    private static List<(string Emoji, string Name, bool TextByDefault)> ReadTable()
    {
        using Stream stream = typeof(StandardEmoji).Assembly.GetManifestResourceStream("GrugBot.Core.Formatting.EmojiNames.tsv")
            ?? throw new InvalidOperationException("The emoji table isn't embedded.");
        using var reader = new StreamReader(stream, Encoding.ASCII);

        var table = new List<(string, string, bool)>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split('\t');
            int[] codePoints = fields[0].Split(' ')
                .Select(hex => int.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture))
                .ToArray();
            string emoji = string.Concat(codePoints.Select(char.ConvertFromUtf32));
            bool textByDefault = codePoints.Length == 2 && codePoints[1] == 0xFE0F;
            table.Add((emoji, fields[1], textByDefault));
        }

        return table;
    }

    private static string Hex(string text) =>
        string.Join(" ", text.Select(c => ((int)c).ToString("X4", CultureInfo.InvariantCulture)));
}
