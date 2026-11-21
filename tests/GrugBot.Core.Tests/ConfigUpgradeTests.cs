using System.Linq;
using GrugBot.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GrugBot.Core.Tests;

/// <summary>
/// Upgrading an existing config file (issue #14). The upgraded text is read back with Newtonsoft.Json, as the game does.
/// </summary>
public class ConfigUpgradeTests
{
    private const string SecretToken = "MTIz.secret-token+value_9";

    private static JObject Parse(string? json)
    {
        Assert.NotNull(json);
        return JObject.Parse(json);
    }

    [Fact]
    public void A_v0_file_without_ConfigVersion_is_upgraded_and_keeps_every_value()
    {
        string file = $$"""
            {
              "DiscordToken": "{{SecretToken}}",
              "ChatChannelId": 1234567890123456789,
              "SystemChannelId": 987654321098765432,
              "Relay": { "Deaths": false }
            }
            """;

        ConfigUpgradeResult result = ConfigUpgrade.Decide(file);

        Assert.Equal(ConfigUpgradeAction.Upgraded, result.Action);
        Assert.Equal(0, result.FileVersion);
        JObject json = Parse(result.UpgradedJson);
        Assert.Equal(GrugBotConfig.CurrentVersion, json["ConfigVersion"]!.Value<int>());
        Assert.Equal(SecretToken, json["DiscordToken"]!.Value<string>());
        Assert.Equal(1234567890123456789UL, json["ChatChannelId"]!.Value<ulong>());
        Assert.Equal(987654321098765432UL, json["SystemChannelId"]!.Value<ulong>());
        Assert.False(json["Relay"]!["Deaths"]!.Value<bool>());

        // Missing settings, including ones inside Relay, get their defaults.
        Assert.Equal("#7289DA", json["DiscordNameColor"]!.Value<string>());
        Assert.True(json["IgnoreOtherBots"]!.Value<bool>());
        Assert.Empty((JArray)json["IgnoredDiscordUserIds"]!);
        Assert.False(json["EscapeMarkdown"]!.Value<bool>());
        Assert.True(json["Relay"]!["DiscordToGame"]!.Value<bool>());
        Assert.True(json["Relay"]!["Joins"]!.Value<bool>());
    }

    [Fact]
    public void A_partial_older_file_gets_only_the_missing_settings()
    {
        // A version-1 file from before IgnoredDiscordUserIds and Relay.DiscordToGame existed, read by a version-2 build.
        // Every value differs from its default, so a reset would show.
        string file = $$"""
            {
              "ConfigVersion": 1,
              "DiscordToken": "{{SecretToken}}",
              "ChatChannelId": 111111111111111111,
              "SystemChannelId": 222222222222222222,
              "DiscordNameColor": "#FF0000",
              "IgnoreOtherBots": false,
              "EscapeMarkdown": true,
              "Relay": { "Joins": false, "Leaves": false, "Deaths": false, "ServerStartStop": false, "TemporalStorms": false }
            }
            """;

        ConfigUpgradeResult result = ConfigUpgrade.Decide(file, currentVersion: 2);

        Assert.Equal(ConfigUpgradeAction.Upgraded, result.Action);
        Assert.Equal(1, result.FileVersion);
        JObject json = Parse(result.UpgradedJson);
        Assert.Equal(2, json["ConfigVersion"]!.Value<int>());
        Assert.Equal(SecretToken, json["DiscordToken"]!.Value<string>());
        Assert.Equal(111111111111111111UL, json["ChatChannelId"]!.Value<ulong>());
        Assert.Equal(222222222222222222UL, json["SystemChannelId"]!.Value<ulong>());
        Assert.Equal("#FF0000", json["DiscordNameColor"]!.Value<string>());
        Assert.False(json["IgnoreOtherBots"]!.Value<bool>());
        Assert.True(json["EscapeMarkdown"]!.Value<bool>());
        Assert.Empty((JArray)json["IgnoredDiscordUserIds"]!);

        var relay = (JObject)json["Relay"]!;
        Assert.True(relay["DiscordToGame"]!.Value<bool>());
        Assert.All(
            new[] { "Joins", "Leaves", "Deaths", "ServerStartStop", "TemporalStorms" },
            name => Assert.False(relay[name]!.Value<bool>()));
    }

    [Fact]
    public void The_game_reads_the_upgraded_file_with_the_same_values()
    {
        string file = $$"""
            { "DiscordToken": "{{SecretToken}}", "ChatChannelId": 1234567890123456789, "IgnoredDiscordUserIds": [ 111, 222 ] }
            """;

        ConfigUpgradeResult result = ConfigUpgrade.Decide(file);
        var config = JsonConvert.DeserializeObject<GrugBotConfig>(result.UpgradedJson!)!;

        Assert.Equal(GrugBotConfig.CurrentVersion, config.ConfigVersion);
        Assert.Equal(SecretToken, config.DiscordToken);
        Assert.Equal(1234567890123456789UL, config.ChatChannelId);
        Assert.Equal(new[] { 111UL, 222UL }, config.IgnoredDiscordUserIds);
    }

    [Fact]
    public void An_empty_file_upgrades_to_exactly_a_new_config_file()
    {
        // Also guards the defaults: the upgrade builds them with System.Text.Json, the game with Newtonsoft.Json.
        JToken fresh = JToken.Parse(JsonConvert.SerializeObject(new GrugBotConfig()));

        JObject upgraded = Parse(ConfigUpgrade.Decide("{}").UpgradedJson);

        Assert.True(JToken.DeepEquals(fresh, upgraded), upgraded.ToString());
        Assert.Equal(((JObject)fresh).Properties().Select(p => p.Name), upgraded.Properties().Select(p => p.Name));
    }

    [Fact]
    public void Settings_come_out_in_new_file_order_with_unknown_fields_last()
    {
        string file = """
            { "Relay": { "Typo": 1, "Deaths": false }, "MyNote": "hi", "ChatChannelId": 42 }
            """;

        JObject json = Parse(ConfigUpgrade.Decide(file).UpgradedJson);

        Assert.Equal(
            new[]
            {
                "ConfigVersion", "DiscordToken", "ChatChannelId", "SystemChannelId", "DiscordNameColor",
                "IgnoreOtherBots", "IgnoredDiscordUserIds", "EscapeMarkdown", "Relay", "MyNote",
            },
            json.Properties().Select(p => p.Name));
        Assert.Equal(
            new[] { "DiscordToGame", "Joins", "Leaves", "Deaths", "ServerStartStop", "TemporalStorms", "Typo" },
            ((JObject)json["Relay"]!).Properties().Select(p => p.Name));
    }

    [Fact]
    public void Unknown_extra_fields_are_kept_with_their_values()
    {
        string file = """
            {
              "ChatChanelId": 1234567890123456789,
              "Extra": { "Nested": [ 1, "two", null ], "Flag": true },
              "Relay": { "Typos": "kept" }
            }
            """;

        ConfigUpgradeResult result = ConfigUpgrade.Decide(file);

        Assert.Equal(ConfigUpgradeAction.Upgraded, result.Action);
        JObject json = Parse(result.UpgradedJson);
        Assert.Equal(1234567890123456789UL, json["ChatChanelId"]!.Value<ulong>());
        Assert.True(JToken.DeepEquals(JToken.Parse("""{ "Nested": [ 1, "two", null ], "Flag": true }"""), json["Extra"]));
        Assert.Equal("kept", json["Relay"]!["Typos"]!.Value<string>());
        Assert.Equal(0UL, json["ChatChannelId"]!.Value<ulong>());
    }

    [Fact]
    public void Setting_names_match_ignoring_case_like_the_game_does()
    {
        JObject json = Parse(ConfigUpgrade.Decide("""{ "chatchannelid": 42, "relay": { "deaths": false } }""").UpgradedJson);

        Assert.Equal(42UL, json["ChatChannelId"]!.Value<ulong>());
        Assert.False(json["Relay"]!["Deaths"]!.Value<bool>());
        Assert.Null(json["chatchannelid"]);
        Assert.Null(json["relay"]);
    }

    [Fact]
    public void Null_values_are_kept_as_written()
    {
        // The mod already turns null sections into defaults when it reads the file (FillMissingSections).
        JObject json = Parse(ConfigUpgrade.Decide("""{ "Relay": null }""").UpgradedJson);

        Assert.Equal(JTokenType.Null, json["Relay"]!.Type);
    }

    [Fact]
    public void A_file_at_the_current_version_is_kept_as_it_is()
    {
        string file = JsonConvert.SerializeObject(new GrugBotConfig { DiscordToken = SecretToken, ChatChannelId = 42 });

        ConfigUpgradeResult result = ConfigUpgrade.Decide(file);

        Assert.Equal(ConfigUpgradeAction.KeepAsIs, result.Action);
        Assert.Equal(GrugBotConfig.CurrentVersion, result.FileVersion);
        Assert.Null(result.UpgradedJson);
    }

    [Fact]
    public void A_file_at_the_current_version_is_kept_even_with_settings_missing()
    {
        ConfigUpgradeResult result = ConfigUpgrade.Decide("""{ "ConfigVersion": 1, "ChatChannelId": 42 }""", currentVersion: 1);

        Assert.Equal(ConfigUpgradeAction.KeepAsIs, result.Action);
        Assert.Null(result.UpgradedJson);
    }

    [Fact]
    public void A_file_from_a_newer_version_is_left_untouched_with_a_warning()
    {
        ConfigUpgradeResult result = ConfigUpgrade.Decide(
            $$"""{ "ConfigVersion": 5, "DiscordToken": "{{SecretToken}}", "FutureSetting": true }""", currentVersion: 1);

        Assert.Equal(ConfigUpgradeAction.NewerThanMod, result.Action);
        Assert.Equal(5, result.FileVersion);
        Assert.Null(result.UpgradedJson);

        string warning = result.Describe();
        Assert.Contains("version 5", warning);
        Assert.Contains("(1)", warning);
        Assert.Contains(GrugBotConfig.DisplayPath, warning);
        Assert.DoesNotContain(SecretToken, warning);
    }

    [Theory]
    [InlineData("""{ "ConfigVersion": null }""", 0)]
    [InlineData("""{ "ConfigVersion": "0" }""", 0)]
    [InlineData("""{ "configversion": 0 }""", 0)]
    [InlineData("""{ "ConfigVersion": "1" }""", 1)]
    public void The_version_is_read_like_the_game_reads_it(string file, int expected)
    {
        Assert.Equal(expected, ConfigUpgrade.Decide(file, currentVersion: 1).FileVersion);
    }

    [Fact]
    public void The_upgrade_log_line_names_the_from_version_and_never_the_token()
    {
        ConfigUpgradeResult result = ConfigUpgrade.Decide($$"""{ "DiscordToken": "{{SecretToken}}" }""");

        string line = result.Describe();

        Assert.Contains("from config version 0", line);
        Assert.Contains(GrugBotConfig.DisplayPath, line);
        Assert.DoesNotContain(SecretToken, line);
        Assert.DoesNotContain("\n", line);
        Assert.DoesNotContain("{", line);
        Assert.DoesNotContain("}", line);
        Assert.DoesNotContain(SecretToken, result.ToString());
    }

    [Theory]
    [InlineData("[ 1, 2 ]")]
    [InlineData("\"text\"")]
    [InlineData("{ not json")]
    // Newtonsoft.Json writes NaN as a bare literal and the game reads it; System.Text.Json rejects it.
    [InlineData("{ \"DiscordToken\": \"" + SecretToken + "\", \"Extra\": NaN }")]
    public void A_file_that_cant_be_read_as_a_json_object_is_left_untouched_with_a_warning(string file)
    {
        ConfigUpgradeResult result = ConfigUpgrade.Decide(file);

        Assert.Equal(ConfigUpgradeAction.Unreadable, result.Action);
        Assert.Null(result.UpgradedJson);

        string warning = result.Describe();
        Assert.Contains(GrugBotConfig.DisplayPath, warning);
        Assert.DoesNotContain(SecretToken, warning);
        Assert.DoesNotContain("Extra", warning);
        Assert.DoesNotContain("{", warning);
        Assert.DoesNotContain("}", warning);
    }
}
