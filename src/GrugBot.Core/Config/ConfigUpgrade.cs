using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrugBot.Core.Config;

/// <summary>
/// Decides what to do with an existing <c>ModConfig/grugbot.json</c> written by another grug-bot version, and builds
/// the upgraded file (issue #14, decision #28).
/// </summary>
/// <remarks>
/// It works on the file's JSON rather than on <see cref="GrugBotConfig"/>, so every value the owner set is copied
/// exactly as written (token and channel IDs included) and fields this build doesn't know, such as a typo'd setting,
/// are kept instead of silently dropped. It uses System.Text.Json from the .NET runtime, so this library still needs
/// no package of its own; the game reads the result with Newtonsoft.Json, which the tests check.
/// </remarks>
public static class ConfigUpgrade
{
    private const string VersionField = nameof(GrugBotConfig.ConfigVersion);

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        // Newtonsoft.Json, which the game uses, accepts both, so a file the game can read must not trip us up.
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Keep strings readable in the file (no \u escapes for characters like + or é). The text is only ever
        // written to the config file, never into HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Looks at the file's <c>ConfigVersion</c> (missing or <c>null</c> counts as 0) and decides:
    /// <list type="bullet">
    /// <item>same as <paramref name="currentVersion"/>: keep the file as it is, even if fields are missing;</item>
    /// <item>older: build the upgraded file, with missing settings (also inside sections such as <c>Relay</c>) set to
    /// their defaults, <c>ConfigVersion</c> set to <paramref name="currentVersion"/>, and everything else kept;</item>
    /// <item>newer: leave the file untouched, so the caller can warn once.</item>
    /// </list>
    /// Settings come out in the same order as in a new file, followed by any fields this build doesn't know. Names are
    /// matched ignoring case, as Newtonsoft.Json does when the game reads the file. Values, <c>null</c> included, are
    /// copied as written. A file that can't be read as a JSON object is <see cref="ConfigUpgradeAction.Unreadable"/>
    /// and left untouched. Never throws.
    /// </summary>
    /// <param name="fileJson">The config file's text.</param>
    /// <param name="currentVersion">The version this build writes; only tests pass anything but the default.</param>
    public static ConfigUpgradeResult Decide(string fileJson, int currentVersion = GrugBotConfig.CurrentVersion)
    {
        try
        {
            if (JsonNode.Parse(fileJson, documentOptions: ReadOptions) is not JsonObject file)
            {
                return new ConfigUpgradeResult(ConfigUpgradeAction.Unreadable, 0, currentVersion, null);
            }

            int fileVersion = ReadVersion(file);
            if (fileVersion == currentVersion)
            {
                return new ConfigUpgradeResult(ConfigUpgradeAction.KeepAsIs, fileVersion, currentVersion, null);
            }

            if (fileVersion > currentVersion)
            {
                return new ConfigUpgradeResult(ConfigUpgradeAction.NewerThanMod, fileVersion, currentVersion, null);
            }

            JsonObject upgraded = Merge(file, Defaults());
            upgraded[VersionField] = currentVersion;
            return new ConfigUpgradeResult(
                ConfigUpgradeAction.Upgraded, fileVersion, currentVersion, upgraded.ToJsonString(WriteOptions));
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            // Not JSON we can make sense of (System.Text.Json may report duplicate names with ArgumentException).
            // Leave the file alone. The exception isn't kept: its message can quote part of the file.
            return new ConfigUpgradeResult(ConfigUpgradeAction.Unreadable, 0, currentVersion, null);
        }
    }

    /// <summary>A new config file as JSON: every setting with its default, in file order.</summary>
    private static JsonObject Defaults() => JsonSerializer.SerializeToNode(new GrugBotConfig())!.AsObject();

    /// <summary>The file's <c>ConfigVersion</c>; 0 when it is missing, <c>null</c> or not a whole number.</summary>
    private static int ReadVersion(JsonObject file)
    {
        if (FindSetting(file, VersionField, null) is JsonValue value)
        {
            if (value.TryGetValue(out int number))
            {
                return number;
            }

            // Newtonsoft.Json reads "1" into an int too.
            if (value.TryGetValue(out string? text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return 0;
    }

    /// <summary>
    /// Every setting of <paramref name="defaults"/> in its order, taking the file's value where it has one (merging
    /// sections recursively), then the file's fields that match no setting, in the file's order.
    /// </summary>
    private static JsonObject Merge(JsonObject file, JsonObject defaults)
    {
        var merged = new JsonObject();
        var matched = new HashSet<string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNode?> setting in defaults)
        {
            if (!HasSetting(file, setting.Key))
            {
                merged[setting.Key] = setting.Value?.DeepClone();
                continue;
            }

            JsonNode? owned = FindSetting(file, setting.Key, matched);
            merged[setting.Key] = owned is JsonObject ownedSection && setting.Value is JsonObject defaultSection
                ? Merge(ownedSection, defaultSection)
                : owned?.DeepClone();
        }

        foreach (KeyValuePair<string, JsonNode?> field in file)
        {
            if (!matched.Contains(field.Key))
            {
                merged[field.Key] = field.Value?.DeepClone();
            }
        }

        return merged;
    }

    private static bool HasSetting(JsonObject file, string name)
    {
        foreach (KeyValuePair<string, JsonNode?> field in file)
        {
            if (string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The value of setting <paramref name="name"/>, matched ignoring case. If the file spells it more than once
    /// (say <c>ChatChannelId</c> and <c>chatChannelId</c>), the last one is used whole. For a single value that matches
    /// how Newtonsoft.Json reads the file; for a section or list it doesn't, because Newtonsoft fills one object (or
    /// list) from every spelling. Files the game writes never do this. Every spelling found is added to
    /// <paramref name="matched"/>, so none of them is copied again as an unknown field.
    /// </summary>
    private static JsonNode? FindSetting(JsonObject file, string name, HashSet<string>? matched)
    {
        JsonNode? value = null;
        foreach (KeyValuePair<string, JsonNode?> field in file)
        {
            if (string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = field.Value;
                matched?.Add(field.Key);
            }
        }

        return value;
    }
}
