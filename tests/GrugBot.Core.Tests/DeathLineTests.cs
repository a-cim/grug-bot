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
}
