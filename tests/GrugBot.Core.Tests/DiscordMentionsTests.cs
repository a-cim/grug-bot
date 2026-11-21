using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordMentionsTests
{
    private const string Zwsp = "\u200B";

    [Theory]
    [InlineData("@everyone hi", "@" + Zwsp + "everyone hi")]
    [InlineData("hi @here", "hi @" + Zwsp + "here")]
    [InlineData("@Everyone", "@" + Zwsp + "Everyone")]
    [InlineData("@everyone and @everyone", "@" + Zwsp + "everyone and @" + Zwsp + "everyone")]
    public void Everyone_and_here_are_broken_up(string input, string expected)
    {
        Assert.Equal(expected, DiscordMentions.Neutralise(input));
    }

    [Theory]
    [InlineData("<@&123> hi", "<" + Zwsp + "@&123> hi")]
    [InlineData("<@123>", "<" + Zwsp + "@123>")]
    [InlineData("<@!123>", "<" + Zwsp + "@!123>")]
    public void User_and_role_mentions_are_broken_up(string input, string expected)
    {
        Assert.Equal(expected, DiscordMentions.Neutralise(input));
    }

    [Theory]
    [InlineData("mail grug@example.com")]
    [InlineData("@Ugg look")]
    [InlineData("<#123> is a channel")]
    [InlineData("<@nope>")]
    public void Other_text_is_unchanged(string input)
    {
        Assert.Equal(input, DiscordMentions.Neutralise(input));
    }
}
