using System;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class DeathLineTests
{
    [Fact]
    public void The_game_sentence_is_posted_bold_italic()
    {
        // 05-message-format § Deaths, test plan 3.4.
        DeathLine line = DeathLine.For("Player Ugg got killed by a nightmare drifter", "Ugg", "Nightmare drifter");

        Assert.Equal("***Player Ugg got killed by a nightmare drifter***", line.Message);
        Assert.False(line.IsFallback);
    }

    [Fact]
    public void The_game_sentence_is_plain_text_with_markdown_escaped()
    {
        Assert.Equal(
            @"***Player Grug\_the\_Great forgot to eat. <3 & co***",
            SystemMessages.PlayerDied(" <font color=\"#ff0000\">Player Grug_the_Great</font> forgot to eat. &lt;3 &amp; co\n"));
    }

    [Fact]
    public void Mentions_in_the_game_sentence_are_broken_up()
    {
        Assert.DoesNotContain("@everyone", SystemMessages.PlayerDied("Player @everyone drowned."), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n")]
    [InlineData("<font color=\"#fff\"></font>")]
    public void Without_a_game_sentence_the_fallback_says_the_player_died(string? sentence)
    {
        // Gotcha #19: the private method may be gone, fail or return nothing.
        DeathLine line = DeathLine.For(sentence, "Ugg", null);

        Assert.Equal("***Player Ugg died***", line.Message);
        Assert.True(line.IsFallback);
    }

    [Fact]
    public void The_fallback_names_the_killer_when_known()
    {
        DeathLine line = DeathLine.For(null, "Ugg", "Nightmare drifter");

        Assert.Equal("***Player Ugg was killed by Nightmare drifter***", line.Message);
        Assert.True(line.IsFallback);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<br>")]
    public void A_blank_killer_name_reads_as_no_killer(string killer)
    {
        Assert.Equal("***Player Ugg died***", SystemMessages.PlayerDiedFallback("Ugg", killer));
    }

    [Fact]
    public void The_fallback_escapes_markdown_in_both_names()
    {
        Assert.Equal(
            @"***Player Grug\_the\_Great was killed by Ugg\*\****",
            SystemMessages.PlayerDiedFallback("Grug_the_Great", "Ugg**"));
    }

    [Fact]
    public void Mentions_in_the_fallback_are_broken_up()
    {
        string line = SystemMessages.PlayerDiedFallback("@everyone", "@here");

        Assert.DoesNotContain("@everyone", line, StringComparison.Ordinal);
        Assert.DoesNotContain("@here", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_identical_deaths_give_two_identical_lines()
    {
        // Test plan 3.5: no de-duplication; each death is its own line.
        DeathLine first = DeathLine.For("Player Ugg fell to their death.", "Ugg", null);
        DeathLine second = DeathLine.For("Player Ugg fell to their death.", "Ugg", null);

        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public void The_death_count_ends_the_game_sentence_inside_the_markers()
    {
        // #32, 05-message-format § Deaths, test plan 3.9.
        DeathLine line = DeathLine.For("Player Ugg got pancaked", "Ugg", null, deathNumber: 7);

        Assert.Equal("***Player Ugg got pancaked. This is death number 7.***", line.Message);
        Assert.False(line.IsFallback);
    }

    [Theory]
    [InlineData("Player Ugg fell to their death.", "***Player Ugg fell to their death. This is death number 2.***")]
    [InlineData("Player Ugg exploded!", "***Player Ugg exploded! This is death number 2.***")]
    [InlineData("Player Ugg drowned?", "***Player Ugg drowned? This is death number 2.***")]
    [InlineData("Player Ugg got killed by a nightmare drifter", "***Player Ugg got killed by a nightmare drifter. This is death number 2.***")]
    [InlineData("Player Ugg drowned.  \n", "***Player Ugg drowned. This is death number 2.***")]
    [InlineData("Player Ugg drowned \t", "***Player Ugg drowned. This is death number 2.***")]
    [InlineData("<font color=\"#f00\">Player Ugg drowned.</font> ", "***Player Ugg drowned. This is death number 2.***")]
    [InlineData("Player Ugg was eaten by Grug**", @"***Player Ugg was eaten by Grug\*\*. This is death number 2.***")]
    public void A_full_stop_is_added_only_when_the_sentence_has_no_end_punctuation(string sentence, string expected)
    {
        Assert.Equal(expected, DeathLine.For(sentence, "Ugg", null, deathNumber: 2).Message);
    }

    [Fact]
    public void The_fallback_ends_with_the_death_count_too()
    {
        Assert.Equal("***Player Ugg died. This is death number 1.***", DeathLine.For(null, "Ugg", null, deathNumber: 1).Message);
        Assert.Equal(
            "***Player Ugg was killed by Nightmare drifter. This is death number 3.***",
            DeathLine.For("  ", "Ugg", "Nightmare drifter", deathNumber: 3).Message);
        Assert.True(DeathLine.For(null, "Ugg", null, deathNumber: 1).IsFallback);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void No_count_means_no_suffix(int deathNumber)
    {
        // thebasics not installed, not counting deaths, or the count unreadable: the line is as before.
        Assert.Equal("***Player Ugg got pancaked***", DeathLine.For("Player Ugg got pancaked", "Ugg", null, deathNumber).Message);
        Assert.Equal("***Player Ugg died***", DeathLine.For(null, "Ugg", null, deathNumber).Message);
    }

    [Fact]
    public void Without_a_count_the_line_is_unchanged()
    {
        Assert.Equal(DeathLine.For("Player Ugg got pancaked", "Ugg", null, 0), DeathLine.For("Player Ugg got pancaked", "Ugg", null));
        Assert.Equal("***Player Ugg got pancaked***", SystemMessages.PlayerDied("Player Ugg got pancaked"));
        Assert.Equal("***Player Ugg died***", SystemMessages.PlayerDiedFallback("Ugg", null));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(10, "10")]
    [InlineData(1234567, "1234567")]
    [InlineData(int.MaxValue, "2147483647")]
    public void The_count_is_written_as_plain_digits(int deathNumber, string digits)
    {
        Assert.Equal(
            "***Player Ugg drowned. This is death number " + digits + ".***",
            SystemMessages.PlayerDied("Player Ugg drowned", deathNumber));
    }

    [Fact]
    public void A_blank_game_sentence_still_falls_back_with_a_count()
    {
        Assert.Throws<ArgumentException>(() => SystemMessages.PlayerDied("  ", 4));
        Assert.Equal("***Player Ugg died. This is death number 4.***", DeathLine.For("<br>", "Ugg", null, 4).Message);
    }
}
