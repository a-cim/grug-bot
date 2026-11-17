using GrugBot.Core.Config;
using Xunit;

namespace GrugBot.Core.Tests;

public class ConfigCheckTests
{
    private const string SecretToken = "secret-token-value";

    [Fact]
    public void Nothing_is_reported_when_token_and_chat_channel_are_set()
    {
        var config = new GrugBotConfig { ChatChannelId = 42 };

        Assert.Null(ConfigCheck.DescribeMissing(DiscordTokenResolver.Resolve(SecretToken, null), config));
    }

    [Fact]
    public void A_fresh_config_reports_both_problems_in_one_line()
    {
        string? message = ConfigCheck.DescribeMissing(DiscordTokenResolver.Resolve(null, null), new GrugBotConfig());

        Assert.NotNull(message);
        Assert.DoesNotContain("\n", message);
        Assert.Contains("GRUGBOT_DISCORD_TOKEN", message);
        Assert.Contains("ChatChannelId", message);
    }

    [Fact]
    public void A_missing_token_is_reported_alone()
    {
        var config = new GrugBotConfig { ChatChannelId = 42 };

        string? message = ConfigCheck.DescribeMissing(DiscordTokenResolver.Resolve(null, null), config);

        Assert.NotNull(message);
        Assert.Contains("GRUGBOT_DISCORD_TOKEN", message);
        Assert.DoesNotContain("ChatChannelId", message);
    }

    [Fact]
    public void A_missing_chat_channel_is_reported_without_the_token()
    {
        string? message = ConfigCheck.DescribeMissing(DiscordTokenResolver.Resolve(SecretToken, null), new GrugBotConfig());

        Assert.NotNull(message);
        Assert.Contains("ChatChannelId", message);
        Assert.DoesNotContain("GRUGBOT_DISCORD_TOKEN", message);
        Assert.DoesNotContain(SecretToken, message);
    }

    [Fact]
    public void The_message_is_safe_to_use_as_a_log_format_string()
    {
        string? message = ConfigCheck.DescribeMissing(DiscordTokenResolver.Resolve(null, null), new GrugBotConfig());

        Assert.DoesNotContain("{", message);
        Assert.DoesNotContain("}", message);
    }
}
