using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordMarkdownTests
{
    [Theory]
    [InlineData("*bold* _it_", @"\*bold\* \_it\_")]
    [InlineData("~~gone~~", @"\~\~gone\~\~")]
    [InlineData("`code`", @"\`code\`")]
    [InlineData("||spoiler||", @"\|\|spoiler\|\|")]
    [InlineData("> quote", @"\> quote")]
    [InlineData("# heading", @"\# heading")]
    [InlineData("- item", @"\- item")]
    [InlineData("[link](https://example.com)", @"\[link\](https://example.com)")]
    public void Markdown_characters_are_escaped(string input, string expected)
    {
        Assert.Equal(expected, DiscordMarkdown.Escape(input));
    }

    [Fact]
    public void A_backslash_is_escaped_so_it_cannot_escape_the_next_character()
    {
        Assert.Equal(@"a\\\*", DiscordMarkdown.Escape(@"a\*"));
    }

    [Theory]
    [InlineData("plain text 123")]
    [InlineData("https://example.com/a.b?c=d")]
    public void Text_without_markdown_is_unchanged(string input)
    {
        Assert.Equal(input, DiscordMarkdown.Escape(input));
    }
}
