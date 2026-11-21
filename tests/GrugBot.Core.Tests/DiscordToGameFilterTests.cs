using GrugBot.Core.Relaying;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordToGameFilterTests
{
    private const ulong OwnBot = 1;
    private const ulong Person = 2;
    private const ulong OtherBot = 3;
    private const ulong Ignored = 4;

    private static readonly DiscordToGameFilter Defaults = new(ignoreOtherBots: true, ignoredUserIds: null);

    [Fact]
    public void A_person_is_relayed()
    {
        Assert.True(Defaults.ShouldRelay(Person, isBot: false, isWebhook: false, ownBotId: OwnBot));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Our_own_bot_is_never_relayed(bool ignoreOtherBots)
    {
        // Gotcha #7 and test plan 5.11: our own posts must not come back into the game.
        var filter = new DiscordToGameFilter(ignoreOtherBots, ignoredUserIds: null);

        Assert.False(filter.ShouldRelay(OwnBot, isBot: true, isWebhook: false, ownBotId: OwnBot));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_webhook_is_never_relayed(bool ignoreOtherBots)
    {
        // Gotcha #7: filtering on our own user ID isn't enough.
        var filter = new DiscordToGameFilter(ignoreOtherBots, ignoredUserIds: null);

        Assert.False(filter.ShouldRelay(OtherBot, isBot: true, isWebhook: true, ownBotId: OwnBot));
        Assert.False(filter.ShouldRelay(OtherBot, isBot: false, isWebhook: true, ownBotId: OwnBot));
    }

    [Fact]
    public void Other_bots_are_ignored_when_IgnoreOtherBots_is_on()
    {
        // Test plan 5.9.
        Assert.False(Defaults.ShouldRelay(OtherBot, isBot: true, isWebhook: false, ownBotId: OwnBot));
    }

    [Fact]
    public void Other_bots_are_relayed_when_IgnoreOtherBots_is_off()
    {
        var filter = new DiscordToGameFilter(ignoreOtherBots: false, ignoredUserIds: null);

        Assert.True(filter.ShouldRelay(OtherBot, isBot: true, isWebhook: false, ownBotId: OwnBot));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Users_in_IgnoredDiscordUserIds_are_ignored(bool ignoreOtherBots)
    {
        // Test plan 5.9.
        var filter = new DiscordToGameFilter(ignoreOtherBots, new[] { Ignored });

        Assert.False(filter.ShouldRelay(Ignored, isBot: false, isWebhook: false, ownBotId: OwnBot));
        Assert.False(filter.ShouldRelay(Ignored, isBot: true, isWebhook: false, ownBotId: OwnBot));
        Assert.True(filter.ShouldRelay(Person, isBot: false, isWebhook: false, ownBotId: OwnBot));
    }
}
