using System;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordToGameFormatterTests
{
    private const string Blurple = "#7289DA";

    [Fact]
    public void A_plain_line_is_the_coloured_bold_name_then_the_text()
    {
        // Test plan 5.1, in th3essentials' format (05-message-format § Discord → Game).
        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Grug: </strong></font>hello",
            DiscordToGameFormatter.Format("Grug", "hello", Blurple));
    }

    [Fact]
    public void The_name_colour_comes_from_the_setting()
    {
        Assert.Equal(
            "<font color=\"#FF8800\"><strong>Grug: </strong></font>hello",
            DiscordToGameFormatter.Format("Grug", "hello", "#FF8800"));
    }

    [Fact]
    public void Markup_in_the_text_shows_literally()
    {
        // Test plan 5.2 and gotcha #20: a stray <font must not break the chat line.
        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Grug: </strong></font>&lt;font color=red&gt;x&lt;/font&gt; & &lt;i&gt;",
            DiscordToGameFormatter.Format("Grug", "<font color=red>x</font> & <i>", Blurple));
    }

    [Fact]
    public void The_name_is_escaped_too()
    {
        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Ugg &lt;3 & &lt;/strong&gt;: </strong></font>hi",
            DiscordToGameFormatter.Format("Ugg <3 & </strong>", "hi", Blurple));
    }

    [Fact]
    public void An_ampersand_passes_through_and_angle_brackets_are_escaped()
    {
        // Decision #46: the game's chat shows &amp; literally (owner's test 5.2), so & is left as typed.
        string? line = DiscordToGameFormatter.Format("Grug & Ugg", "fish & chips && <i>", Blurple);

        Assert.Equal("<font color=\"#7289DA\"><strong>Grug & Ugg: </strong></font>fish & chips && &lt;i&gt;", line);
        Assert.DoesNotContain("&amp;", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_is_otherwise_unchanged()
    {
        // Markdown, mentions, emoji and spacing are left as posted; the Discord → Game rules (issue #5) come later.
        const string Text = "  *hi* _there_ <@123> :) \"quoted\" ";
        string? line = DiscordToGameFormatter.Format("Grug", Text, Blurple);

        Assert.Equal("<font color=\"#7289DA\"><strong>Grug: </strong></font>  *hi* _there_ &lt;@123&gt; :) \"quoted\" ", line);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n\t")]
    public void An_empty_message_is_ignored(string text)
    {
        Assert.Null(DiscordToGameFormatter.Format("Grug", text, Blurple));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("7289DA")]
    [InlineData("\"><strong>")]
    public void A_name_colour_that_is_not_a_colour_falls_back_to_the_default(string? color)
    {
        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Grug: </strong></font>hi",
            DiscordToGameFormatter.Format("Grug", "hi", color));
    }

    [Theory]
    [InlineData("#7289DA", true)]
    [InlineData("#7289da", true)]
    [InlineData("#7289DAFF", true)]
    [InlineData("7289DA", false)]
    [InlineData("#FFF", false)]
    [InlineData("#72Z9DA", false)]
    [InlineData("#7289DA\n", false)]
    [InlineData("#7289DA\"", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Name_colours_are_six_or_eight_hex_digits_after_a_hash(string? color, bool valid)
    {
        Assert.Equal(valid, DiscordToGameFormatter.IsValidNameColor(color));
    }

    [Theory]
    [InlineData("Grug the Nick", "Grug Global", "grug", "Grug the Nick")]
    [InlineData(null, "Grug Global", "grug", "Grug Global")]
    [InlineData(null, null, "grug", "grug")]
    [InlineData("", "Grug Global", "grug", "Grug Global")]
    [InlineData(" ", "", "grug", "grug")]
    public void The_Discord_name_is_the_server_nickname_else_the_global_name_else_the_username(
        string? serverNickname, string? globalName, string username, string expected)
    {
        Assert.Equal(expected, DiscordToGameFormatter.DiscordName(serverNickname, globalName, username));
    }
}
