namespace GrugBot.Core.Config;

/// <summary>What to do with an existing config file, judged by its <c>ConfigVersion</c> (issue #14).</summary>
public enum ConfigUpgradeAction
{
    /// <summary>The file is at the current version: leave it as it is.</summary>
    KeepAsIs,

    /// <summary>The file is from an older version, or has no <c>ConfigVersion</c>: save the upgraded file back.</summary>
    Upgraded,

    /// <summary>The file is from a newer grug-bot than this one: leave it untouched and warn once.</summary>
    NewerThanMod,

    /// <summary>
    /// The file can't be read here as a JSON object, for example because an unknown field holds <c>NaN</c>, which the
    /// game accepts but System.Text.Json doesn't. Its version is unknown, so leave it untouched and warn once.
    /// </summary>
    Unreadable,
}
