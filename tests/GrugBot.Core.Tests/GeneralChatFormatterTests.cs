using System;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class GeneralChatFormatterTests
{
    // What the game's PlayerChat event hands over: the name prefix, then the player's text, VTML-escaped.
    private static string Said(string name, string escapedText) => "<strong>" + name + ":</strong> " + escapedText;

    [Fact]
    public void A_plain_line_is_the_bold_name_then_the_text()
    {
        // Test plan 2.1.
        Assert.Equal("**Grug:** hello", GeneralChatFormatter.Format("Grug", Said("Grug", "hello"), escapeMarkdown: false));
    }

    [Fact]
    public void Escaped_text_arrives_readable()
    {
        // Test plan 2.3: the player typed <b>hi</b> & 1 < 2.
        string? line = GeneralChatFormatter.Format("Grug", Said("Grug", "&lt;b&gt;hi&lt;/b&gt; &amp; 1 &lt; 2"), escapeMarkdown: false);

        Assert.Equal("**Grug:** <b>hi</b> & 1 < 2", line);
    }

    [Fact]
    public void Vtml_markup_in_the_text_is_stripped()
    {
        string? line = GeneralChatFormatter.Format("Grug", Said("Grug", "<i>hi</i> <font color=\"#fff\">there</font>"), escapeMarkdown: false);

        Assert.Equal("**Grug:** hi there", line);
    }

    // Assertions on mention text are ordinal: culture-aware comparison ignores the zero-width space,
    // so "@\u200Beveryone" would count as containing "@everyone".

    [Fact]
    public void Everyone_and_here_cannot_ping()
    {
        // Test plan 2.4.
        string? line = GeneralChatFormatter.Format("Grug", Said("Grug", "@everyone hi @here"), escapeMarkdown: false);

        Assert.NotNull(line);
        Assert.DoesNotContain("@everyone", line, StringComparison.Ordinal);
        Assert.DoesNotContain("@here", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_role_mention_cannot_ping()
    {
        // Test plan 2.4: the player typed <@&123> hi, which the game escapes.
        string? line = GeneralChatFormatter.Format("Grug", Said("Grug", "&lt;@&amp;123&gt; hi"), escapeMarkdown: false);

        Assert.NotNull(line);
        Assert.DoesNotContain("<@&123>", line, StringComparison.Ordinal);
        Assert.Contains("@&123> hi", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_passes_through_by_default()
    {
        // Test plan 2.5.
        Assert.Equal("**Grug:** *bold* _it_", GeneralChatFormatter.Format("Grug", Said("Grug", "*bold* _it_"), escapeMarkdown: false));
    }

    [Fact]
    public void Markdown_is_escaped_when_EscapeMarkdown_is_on()
    {
        // Test plan 2.5.
        Assert.Equal(@"**Grug:** \*bold\* \_it\_", GeneralChatFormatter.Format("Grug", Said("Grug", "*bold* _it_"), escapeMarkdown: true));
    }

    [Fact]
    public void The_name_is_always_escaped()
    {
        Assert.Equal(@"**Grug\_Ugg:** hi", GeneralChatFormatter.Format("Grug_Ugg", Said("Grug_Ugg", "hi"), escapeMarkdown: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<i></i>")]
    public void Nothing_is_posted_when_no_text_is_left(string escapedText)
    {
        Assert.Null(GeneralChatFormatter.Format("Grug", Said("Grug", escapedText), escapeMarkdown: false));
    }

    [Fact]
    public void A_message_without_the_name_prefix_is_used_whole()
    {
        Assert.Equal("**Grug:** hello", GeneralChatFormatter.Format("Grug", "hello", escapeMarkdown: false));
    }

    [Fact]
    public void Text_before_the_name_prefix_is_dropped()
    {
        // Another mod may put something in front of the prefix, such as a timestamp.
        Assert.Equal(" hi", GeneralChatFormatter.ExtractPlayerText("12:00: <strong>Grug:</strong> hi"));
    }
}
