namespace GrugBot.Core.Config;

/// <summary>
/// The outcome of <see cref="ConfigUpgrade.Decide"/>. Deliberately a class, not a record: <see cref="UpgradedJson"/>
/// holds the whole file, token included, and a record's generated <c>ToString</c> would print it (decision #27).
/// </summary>
public sealed class ConfigUpgradeResult
{
    internal ConfigUpgradeResult(ConfigUpgradeAction action, int fileVersion, int currentVersion, string? upgradedJson)
    {
        Action = action;
        FileVersion = fileVersion;
        CurrentVersion = currentVersion;
        UpgradedJson = upgradedJson;
    }

    public ConfigUpgradeAction Action { get; }

    /// <summary>The <c>ConfigVersion</c> found in the file; 0 when the file has none.</summary>
    public int FileVersion { get; }

    /// <summary>The config version this build writes.</summary>
    public int CurrentVersion { get; }

    /// <summary>
    /// The file to save back, only when <see cref="Action"/> is <see cref="ConfigUpgradeAction.Upgraded"/>; otherwise
    /// <c>null</c>. It contains the Discord token if the file had one: never log it.
    /// </summary>
    public string? UpgradedJson { get; }

    /// <summary>
    /// One log line about the outcome, naming the versions involved. It never contains the token or any other value
    /// from the file, and has no curly braces, so it is safe as a log format string.
    /// </summary>
    public string Describe() => Action switch
    {
        ConfigUpgradeAction.Upgraded =>
            "Upgraded " + GrugBotConfig.DisplayPath + " from config version " + FileVersion + " to " + CurrentVersion
            + ": new settings were added with their defaults, and every existing value was kept.",
        ConfigUpgradeAction.NewerThanMod =>
            GrugBotConfig.DisplayPath + " is config version " + FileVersion + ", newer than this grug-bot build knows ("
            + CurrentVersion + "), so the file is left unchanged and settings this build doesn't know are ignored."
            + " Update grug-bot to use them.",
        _ => GrugBotConfig.DisplayPath + " is up to date (config version " + FileVersion + ").",
    };

    /// <summary>Versions and action only, so an accidental log of this object can't leak the token.</summary>
    public override string ToString() => Action + " (file version " + FileVersion + ", current " + CurrentVersion + ")";
}
