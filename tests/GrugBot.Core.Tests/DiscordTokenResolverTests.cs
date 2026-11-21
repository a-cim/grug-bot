using GrugBot.Core.Config;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordTokenResolverTests
{
    private const string EnvToken = "env-token-value";
    private const string FileToken = "file-token-value";

    [Fact]
    public void The_environment_variable_wins_over_the_config_file()
    {
        ResolvedToken token = DiscordTokenResolver.Resolve(EnvToken, FileToken);

        Assert.Equal(TokenSource.EnvironmentVariable, token.Source);
        Assert.Equal(EnvToken, token.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void The_config_file_is_used_when_the_environment_variable_is_not_set(string? environmentValue)
    {
        ResolvedToken token = DiscordTokenResolver.Resolve(environmentValue, FileToken);

        Assert.Equal(TokenSource.ConfigFile, token.Source);
        Assert.Equal(FileToken, token.Value);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", "\t")]
    public void No_token_anywhere_is_missing(string? environmentValue, string? configValue)
    {
        ResolvedToken token = DiscordTokenResolver.Resolve(environmentValue, configValue);

        Assert.Equal(TokenSource.None, token.Source);
        Assert.True(token.IsMissing);
        Assert.Null(token.Value);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        ResolvedToken token = DiscordTokenResolver.Resolve(null, "  " + FileToken + "\n");

        Assert.Equal(FileToken, token.Value);
    }

    [Theory]
    [InlineData(EnvToken, null, "GRUGBOT_DISCORD_TOKEN")]
    [InlineData(null, FileToken, "ModConfig/grugbot.json")]
    public void Describing_the_source_names_it_but_never_prints_the_token(
        string? environmentValue, string? configValue, string expectedSourceText)
    {
        ResolvedToken token = DiscordTokenResolver.Resolve(environmentValue, configValue);

        Assert.Contains(expectedSourceText, token.DescribeSource());
        Assert.DoesNotContain(token.Value!, token.DescribeSource());
        Assert.DoesNotContain(token.Value!, token.ToString());
    }
}
