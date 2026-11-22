using System;
using GrugBot.Core.Formatting;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Game → Discord for chat: passes what players say in General to the chat channel. Every other chat
/// group (thebasics' Proximity and Global OOC, player-made groups) is ignored (decision #7).
/// </summary>
public sealed class GeneralChatListener : IDisposable
{
    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly bool _escapeMarkdown;
    private bool _subscribed;

    public GeneralChatListener(ICoreServerAPI api, ILogger log, DiscordConnection discord, bool escapeMarkdown)
    {
        _api = api;
        _log = log;
        _discord = discord;
        _escapeMarkdown = escapeMarkdown;
    }

    /// <summary>Subscribes to the game's chat event. Called once, from <c>StartServerSide</c>.</summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _api.Event.PlayerChat += OnPlayerChat;
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
            if (chatGroupId != GlobalConstants.GeneralChatGroup)
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
    /// The name other players see for this player in General (decision #36). Today that's the player
    /// name: thebasics doesn't put nicknames into General. If testing shows otherwise (test plan 2.9,
    /// decision #40), the nickname lookup goes here. Join and leave lines use it too, so every name on
    /// Discord stays the same (decision #36).
    /// </summary>
    internal static string ShownName(IServerPlayer player) => player.PlayerName;
}
