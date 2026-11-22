using GrugBot.Core.Config;

namespace GrugBot.Core.Relaying;

/// <summary>
/// The thebasics safety check (01-architecture § Data flow, gotcha #15, issue #11). thebasics can be configured to
/// turn General into its Proximity chat group; relaying General then would leak local roleplay to Discord, so the
/// General → Discord relay is turned off and the log says why.
/// </summary>
/// <remarks>
/// grug-bot reaches thebasics only at runtime, by reflection, with no compile-time reference and none of its code
/// (ADR 0002). The names below are what that lookup searches for. If the lookup fails (an update renamed something,
/// a property went missing, it threw), the relay stays on and one warning is logged: grug-bot shouldn't go quiet just
/// because thebasics updated (in the spirit of decision #37).
/// </remarks>
public static class TheBasicsGuard
{
    /// <summary>thebasics' mod ID.</summary>
    public const string ModId = "thebasics";

    /// <summary>The full name of thebasics' Proximity chat mod system.</summary>
    public const string ProximityChatSystemTypeName = "thebasics.ModSystems.ProximityChat.RPProximityChatSystem";

    /// <summary>The public <c>int</c> property on that mod system that holds the Proximity chat group ID.</summary>
    public const string ProximityChatGroupIdPropertyName = "ProximityChatGroupId";

    /// <summary>thebasics' config setting that makes General its Proximity chat group.</summary>
    public const string GeneralAsProximitySettingName = "UseGeneralChannelAsProximityChat";

    /// <summary>
    /// Decides whether General is relayed to Discord, given what the runtime lookup found. Only Game → Discord chat
    /// from General is affected; system messages and Discord → Game are not (decision #48).
    /// </summary>
    /// <param name="lookup">What the lookup of thebasics' Proximity chat group found.</param>
    /// <param name="generalChatGroupId">The game's General chat group ID (<c>GlobalConstants.GeneralChatGroup</c>, 0).</param>
    public static TheBasicsGuardDecision Decide(TheBasicsLookup lookup, int generalChatGroupId)
    {
        switch (lookup.Outcome)
        {
            case TheBasicsLookupOutcome.NotInstalled:
                return new TheBasicsGuardDecision(
                    relayGeneral: true,
                    isWarning: false,
                    "thebasics isn't installed, so General is relayed to Discord as usual.");

            case TheBasicsLookupOutcome.Found when lookup.ProximityChatGroupId == generalChatGroupId:
                return new TheBasicsGuardDecision(
                    relayGeneral: false,
                    isWarning: true,
                    "thebasics has made General its Proximity chat group (" + GeneralAsProximitySettingName
                    + " is true in thebasics' config), so grug-bot does not relay General to Discord: it would leak local"
                    + " roleplay. Only General chat to Discord is off; system messages and messages from Discord are"
                    + " unchanged (set Relay.DiscordToGame to false in " + GrugBotConfig.DisplayPath + " to turn messages"
                    + " from Discord off too). To relay General again, set " + GeneralAsProximitySettingName
                    + " to false in thebasics' config under ModConfig and restart the server.");

            case TheBasicsLookupOutcome.Found:
                return new TheBasicsGuardDecision(
                    relayGeneral: true,
                    isWarning: false,
                    "thebasics is installed and its Proximity chat group is " + lookup.ProximityChatGroupId
                    + ", not General, so General is relayed to Discord and Proximity chat stays in game.");

            default:
                return new TheBasicsGuardDecision(
                    relayGeneral: true,
                    isWarning: true,
                    "grug-bot could not check whether thebasics has made General its Proximity chat group, so General is"
                    + " still relayed to Discord. If " + GeneralAsProximitySettingName + " is true in thebasics' config,"
                    + " local roleplay said in General will reach Discord. Cause: " + (lookup.FailureCause ?? "unknown"));
        }
    }
}
