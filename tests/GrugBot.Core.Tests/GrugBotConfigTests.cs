using System.Linq;
using GrugBot.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GrugBot.Core.Tests;

public class GrugBotConfigTests
{
    [Fact]
    public void Defaults_match_the_architecture_doc()
    {
        var config = new GrugBotConfig();

        Assert.Equal(1, config.ConfigVersion);
        Assert.Equal("", config.DiscordToken);
        Assert.Equal(0UL, config.ChatChannelId);
        Assert.Equal(0UL, config.SystemChannelId);
        Assert.Equal("#7289DA", config.DiscordNameColor);
        Assert.True(config.IgnoreOtherBots);
        Assert.Empty(config.IgnoredDiscordUserIds);
        Assert.False(config.EscapeMarkdown);
        Assert.True(config.Relay.DiscordToGame);
        Assert.True(config.Relay.Joins);
        Assert.True(config.Relay.Leaves);
        Assert.True(config.Relay.Deaths);
        Assert.True(config.Relay.ServerStartStop);
        Assert.True(config.Relay.TemporalStorms);
    }

    [Fact]
    public void A_new_config_file_has_every_field_in_order()
    {
        JObject json = JObject.FromObject(new GrugBotConfig());

        Assert.Equal(
            new[]
            {
                "ConfigVersion", "DiscordToken", "ChatChannelId", "SystemChannelId", "DiscordNameColor",
                "IgnoreOtherBots", "IgnoredDiscordUserIds", "EscapeMarkdown", "Relay",
            },
            json.Properties().Select(p => p.Name));

        Assert.Equal(
            new[] { "DiscordToGame", "Joins", "Leaves", "Deaths", "ServerStartStop", "TemporalStorms" },
            ((JObject)json["Relay"]!).Properties().Select(p => p.Name));
    }

    [Fact]
    public void Fields_left_out_of_the_file_keep_their_defaults()
    {
        var config = JsonConvert.DeserializeObject<GrugBotConfig>(
            """{ "ChatChannelId": 1234567890123456789, "Relay": { "Deaths": false } }""")!;

        Assert.Equal(1234567890123456789UL, config.ChatChannelId);
        Assert.Equal(GrugBotConfig.CurrentVersion, config.ConfigVersion);
        Assert.Equal("#7289DA", config.DiscordNameColor);
        Assert.False(config.Relay.Deaths);
        Assert.True(config.Relay.Joins);
    }

    [Fact]
    public void Ignored_user_ids_are_read_without_duplicating_defaults()
    {
        var config = JsonConvert.DeserializeObject<GrugBotConfig>(
            """{ "IgnoredDiscordUserIds": [ 111, 222 ] }""")!;

        Assert.Equal(new[] { 111UL, 222UL }, config.IgnoredDiscordUserIds);
    }

    [Fact]
    public void Null_sections_are_filled_with_defaults()
    {
        var config = JsonConvert.DeserializeObject<GrugBotConfig>(
            """{ "DiscordToken": null, "DiscordNameColor": null, "IgnoredDiscordUserIds": null, "Relay": null }""")!;

        config.FillMissingSections();

        Assert.Equal("", config.DiscordToken);
        Assert.Equal("#7289DA", config.DiscordNameColor);
        Assert.Empty(config.IgnoredDiscordUserIds);
        Assert.True(config.Relay.DiscordToGame);
    }
}
