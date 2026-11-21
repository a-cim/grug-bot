using System.Collections.Generic;
using System.Linq;
using GrugBot.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GrugBot.Core.Tests;

public class GrugBotConfigTests
{
    /// <summary>
    /// The settings of each config version, with sections written as <c>Relay.Joins</c>. Files already at the current
    /// version are never rewritten (issue #14), so adding, renaming or removing a setting needs a
    /// <see cref="GrugBotConfig.CurrentVersion"/> bump. When you change the settings, bump it and add the new list here
    /// under the new number. Don't edit a list that is already recorded.
    /// </summary>
    private static readonly Dictionary<int, string[]> SettingsByVersion = new()
    {
        [1] = new[]
        {
            "ConfigVersion", "DiscordToken", "ChatChannelId", "SystemChannelId", "DiscordNameColor",
            "IgnoreOtherBots", "IgnoredDiscordUserIds", "EscapeMarkdown",
            "Relay.DiscordToGame", "Relay.Joins", "Relay.Leaves", "Relay.Deaths", "Relay.ServerStartStop",
            "Relay.TemporalStorms",
        },
    };

    [Fact]
    public void Changing_the_settings_needs_a_config_version_bump()
    {
        string[] settings = JObject.FromObject(new GrugBotConfig()).Properties()
            .SelectMany(p => p.Value is JObject section
                ? section.Properties().Select(s => p.Name + "." + s.Name)
                : new[] { p.Name })
            .ToArray();

        Assert.True(
            SettingsByVersion.TryGetValue(GrugBotConfig.CurrentVersion, out string[]? recorded),
            "No settings are recorded for config version " + GrugBotConfig.CurrentVersion + ". Add its list to SettingsByVersion.");
        Assert.True(
            recorded!.OrderBy(name => name).SequenceEqual(settings.OrderBy(name => name)),
            "The settings differ from the ones recorded for config version " + GrugBotConfig.CurrentVersion
            + ". Bump GrugBotConfig.CurrentVersion and record the new list under the new number."
            + " Settings now: " + string.Join(", ", settings));
    }

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
