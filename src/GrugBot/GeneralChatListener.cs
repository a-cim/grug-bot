using System;
using GrugBot.Core.Formatting;
using GrugBot.Core.Relaying;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Game → Discord for chat: passes what players say in General to the chat channel. Every other chat
/// group (thebasics' Proximity and Global OOC, player-made groups) is ignored (decision #7). If thebasics has
/// made General its Proximity chat group, General isn't relayed either (issue #11, <see cref="TheBasicsGuard"/>).
/// </summary>
public sealed class GeneralChatListener : IDisposable
{
    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly bool _escapeMarkdown;
    private bool _subscribed;

    // Turned off by the thebasics check at RunGame. On until then: players can't chat before the world runs.
    private bool _relayGeneral = true;

    public GeneralChatListener(ICoreServerAPI api, ILogger log, DiscordConnection discord, bool escapeMarkdown)
    {
        _api = api;
        _log = log;
        _discord = discord;
        _escapeMarkdown = escapeMarkdown;
    }

    /// <summary>
    /// Subscribes to the game's chat event, and schedules the thebasics check for <c>RunGame</c>. Called once, from
    /// <c>StartServerSide</c>.
    /// </summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _api.Event.PlayerChat += OnPlayerChat;
        // The game has no way to take this back; after Dispose it finds nothing to do.
        _api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, CheckTheBasics);
        _subscribed = true;
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            _api.Event.PlayerChat -= OnPlayerChat;
            _subscribed = false;
        }
    }

    /// <summary>
    /// Runs on the game thread, so it only formats the line and queues it; sending happens on the send loop.
    /// The game calls its <c>channelId</c> a chat group (decision #20).
    /// </summary>
    private void OnPlayerChat(IServerPlayer byPlayer, int chatGroupId, ref string message, ref string data, BoolRef consumed)
    {
        try
        {
            if (!_relayGeneral || chatGroupId != GlobalConstants.GeneralChatGroup)
            {
                return;
            }

            // A consumed message never reached players (a mute, an anti-spam mod), so it mustn't reach
            // Discord either. grug-bot loads late so this handler sees what earlier mods decided (decision #24).
            if (consumed.value)
            {
                return;
            }

            string? line = GeneralChatFormatter.Format(ShownName(byPlayer), message, _escapeMarkdown);
            if (line is not null)
            {
                _discord.EnqueueChat(line);
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not relay a General chat message: {0}", e);
        }
    }

    /// <summary>
    /// The thebasics safety check (gotcha #15): reads thebasics' Proximity chat group and turns the relay off if it is
    /// General. Runs once, on the game thread, as the world starts running. thebasics is expected to have set that group
    /// by then: it does so during its own startup, and every mod's <c>StartServerSide</c> and earlier run phases are
    /// over by <c>RunGame</c> (grug-bot also loads late, so a <c>RunGame</c> handler of thebasics' should be registered
    /// first). A 0 read here is therefore taken to mean thebasics made General its Proximity chat group, not that it
    /// hasn't got that far yet. That timing isn't guaranteed by thebasics; test plan 7.3 checks it.
    /// Logs exactly one line, which says what was found.
    /// </summary>
    private void CheckTheBasics()
    {
        if (!_subscribed)
        {
            return;
        }

        try
        {
            TheBasicsGuardDecision decision = TheBasicsGuard.Decide(TheBasicsProbe.Look(_api), GlobalConstants.GeneralChatGroup);
            _relayGeneral = decision.RelayGeneral;

            // The line can hold exception text, so it goes in as an argument, never as the format string.
            if (decision.IsWarning)
            {
                _log.Warning("{0}", decision.LogLine);
            }
            else
            {
                _log.Notification("{0}", decision.LogLine);
            }
        }
        catch (Exception e)
        {
            // Our own bug, not thebasics': keep relaying, as for a failed lookup.
            _log.Error("grug-bot could not check thebasics' Proximity chat group, so General is still relayed. Cause: {0}", e);
        }
    }

    /// <summary>
    /// The name other players see for this player in General (decision #36). Today that's the player
    /// name: thebasics doesn't put nicknames into General. If testing shows otherwise (test plan 2.9,
    /// decision #40), the nickname lookup goes here.
    /// </summary>
    private static string ShownName(IServerPlayer player) => player.PlayerName;
}
