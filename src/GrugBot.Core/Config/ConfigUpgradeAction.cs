namespace GrugBot.Core.Config;

/// <summary>What to do with an existing config file, judged by its <c>ConfigVersion</c> (issue #14).</summary>
public enum ConfigUpgradeAction
{
    /// <summary>The file is at the current version (or can't be read as a JSON object): leave it as it is.</summary>
    KeepAsIs,

    /// <summary>The file is from an older version, or has no <c>ConfigVersion</c>: save the upgraded file back.</summary>
    Upgraded,

    /// <summary>The file is from a newer grug-bot than this one: leave it untouched and warn once.</summary>
    NewerThanMod,
}
