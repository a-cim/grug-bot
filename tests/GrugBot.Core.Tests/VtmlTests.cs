using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class VtmlTests
{
    [Theory]
    [InlineData("<strong>hi</strong>", "hi")]
    [InlineData("<font color=\"#ff0000\">red</font> text", "red text")]
    [InlineData("<i>a</i><b>b</b>", "ab")]
    [InlineData("<icon name=\"wood\"/> logs", " logs")]
    [InlineData("no tags here", "no tags here")]
    public void Tags_are_removed_and_their_text_kept(string input, string expected)
    {
        Assert.Equal(expected, Vtml.StripTags(input));
    }

    [Theory]
    [InlineData("a<br>b")]
    [InlineData("a<br/>b")]
    [InlineData("a<BR />b")]
    public void A_line_break_tag_becomes_a_space(string input)
    {
        Assert.Equal("a b", Vtml.StripTags(input));
    }

    [Theory]
    [InlineData("1 < 2 and 3 > 2")]
    [InlineData("a < b > c")]
    [InlineData("<3")]
    public void A_bare_angle_bracket_is_not_a_tag(string input)
    {
        Assert.Equal(input, Vtml.StripTags(input));
    }

    [Fact]
    public void The_three_entities_are_unescaped()
    {
        Assert.Equal("<b> & x", Vtml.Unescape("&lt;b&gt; &amp; x"));
    }

    [Fact]
    public void Unescaping_happens_once()
    {
        Assert.Equal("&lt;", Vtml.Unescape("&amp;lt;"));
    }

    [Fact]
    public void Other_entities_are_left_alone()
    {
        Assert.Equal("&quot; &nbsp;", Vtml.Unescape("&quot; &nbsp;"));
    }

    [Fact]
    public void Text_the_player_typed_as_a_tag_survives_as_readable_text()
    {
        // Test plan 2.3: the game escapes what the player typed, so it must not be stripped as a tag.
        Assert.Equal("<b>hi</b> & 1 < 2", Vtml.ToPlainText("&lt;b&gt;hi&lt;/b&gt; &amp; 1 &lt; 2"));
    }

    [Fact]
    public void Real_tags_are_stripped_before_entities_are_unescaped()
    {
        Assert.Equal("hi & 1 < 2", Vtml.ToPlainText("<b>hi</b> &amp; 1 &lt; 2"));
    }

    [Theory]
    [InlineData("<b>hi</b>", "&lt;b&gt;hi&lt;/b&gt;")]
    [InlineData("<i>", "&lt;i&gt;")]
    [InlineData("1 < 2 > 0", "1 &lt; 2 &gt; 0")]
    [InlineData("no markup \"here\"", "no markup \"here\"")]
    [InlineData("", "")]
    public void Escaping_covers_angle_brackets(string input, string expected)
    {
        // Gotcha #20: a stray <font must not break the chat line.
        Assert.Equal(expected, Vtml.Escape(input));
    }

    [Theory]
    [InlineData("a & b")]
    [InlineData("&&")]
    [InlineData("&amp;")]
    public void An_ampersand_is_left_as_it_is(string input)
    {
        // Decision #46: the game's chat shows &amp; literally (owner's test 5.2), and a lone & can't open a tag.
        Assert.Equal(input, Vtml.Escape(input));
    }
}
