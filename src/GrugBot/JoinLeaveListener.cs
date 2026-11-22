using System;
using System.Collections.Generic;
using GrugBot.Core.Config;
using GrugBot.Core.Formatting;
using GrugBot.Core.Lifecycle;
using GrugBot.Core.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Joins and leaves: posts <c>***📥 Player Ugg connected (2/10)***</c> and <c>***📤 Player Ugg disconnected (1/10)***</c>
/// to the system channel, and keeps the bot presence (<c>players: n/max</c>) up to date (05-message-format, issue #7).
/// </summary>
/// <remarks>
/// <para>
/// A join is the game's <c>PlayerNowPlaying</c>: the player's client has loaded the world. A leave is <c>PlayerDisconnect</c>,
/// which the game also raises for players who never got that far; those get no leave line (gotcha #17, test plan 3.3).
/// </para>
/// <para>
/// No leave lines once shutdown has begun: the game disconnects everyone, and "Server shutdown" already says so
/// (decision #22, test plan 6.1). Both our own <see cref="ServerRunState.IsShuttingDown"/> and the game's
/// <c>IsShuttingDown</c> are checked, because the game may disconnect players before or after our shutdown hook runs.
/// </para>
/// <para>
/// <c>Relay.Joins</c> and <c>Relay.Leaves</c> switch the lines only (test plan 3.8). The presence is always kept up to
/// date: it's the bot's status, not a message in the channel.
/// </para>
/// <para>Everything here runs on the game thread and only queues work for Discord; nothing waits on it.</para>
/// </remarks>
public sealed class JoinLeaveListener : IDisposable
{
    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly ServerRunState _runState;
    private readonly bool _postJoins;
    private readonly bool _postLeaves;

    // Players whose join was seen and who haven't left yet. Only they get a leave line. Game thread only.
    private readonly HashSet<string> _inGame = new(StringComparer.Ordinal);

    private bool _subscribed;
    private bool _disposed;

    public JoinLeaveListener(ICoreServerAPI api, ILogger log, DiscordConnection discord, ServerRunState runState, RelayConfig relay)
    {
        _api = api;
        _log = log;
        _discord = discord;
        _runState = runState;
        _postJoins = relay.Joins;
        _postLeaves = relay.Leaves;
    }

    /// <summary>Subscribes to the game's join and leave events. Called once, from <c>StartServerSide</c>, never on Ready (gotcha #11).</summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        if (!_postJoins)
        {
            _log.Notification("Relay.Joins is off in {0}, so Discord isn't told when players join.", GrugBotConfig.DisplayPath);
        }

        if (!_postLeaves)
        {
            _log.Notification("Relay.Leaves is off in {0}, so Discord isn't told when players leave.", GrugBotConfig.DisplayPath);
        }

        _api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        _api.Event.PlayerDisconnect += OnPlayerDisconnect;

        // The game has no way to take this back; after Dispose it finds _disposed set and returns.
        _api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        _subscribed = true;
    }

    public void Dispose()
    {
        _disposed = true;
        if (_subscribed)
        {
            _api.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            _api.Event.PlayerDisconnect -= OnPlayerDisconnect;
            _subscribed = false;
        }
    }

    /// <summary>True once shutdown has begun, by our own reckoning or the game's (decision #22).</summary>
    private bool IsShuttingDown => _runState.IsShuttingDown || _api.Server.IsShuttingDown;

    /// <summary>The server is up: show the count at once, so the presence is right from the start.</summary>
    private void OnRunGame()
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            _discord.ShowPlayerCount(new PlayerCount(OnlineCount.Now(OnlinePlayers()), MaxClients));
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not set the player count in its Discord status: {0}", e);
        }
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        try
        {
            string? uid = player.PlayerUID;
            if (uid is null)
            {
                return;
            }

            // Tracked whether or not joins are posted, so Relay.Leaves works on its own (test plan 3.8).
            _inGame.Add(uid);
            if (IsShuttingDown)
            {
                return;
            }

            var count = new PlayerCount(OnlineCount.WithJoining(OnlinePlayers(), uid), MaxClients);
            if (_postJoins)
            {
                _discord.EnqueueSystem(SystemMessages.PlayerConnected(GeneralChatListener.ShownName(player), count));
            }

            _discord.PlayerCountChanged(count);
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not post a join message: {0}", e);
        }
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        try
        {
            // A player without an ID is still connecting: they never joined and were never counted.
            string? uid = player.PlayerUID;
            if (uid is null)
            {
                return;
            }

            bool wasInGame = _inGame.Remove(uid);
            if (IsShuttingDown)
            {
                return;
            }

            // The game still lists the leaving player, so they're counted out by ID (gotcha #17).
            var count = new PlayerCount(OnlineCount.WithoutLeaving(OnlinePlayers(), uid), MaxClients);
            _discord.PlayerCountChanged(count);

            // Only players whose join was seen get a leave line. The ConnectionState check is the one
            // th3essentials relies on for players still connecting (gotcha #17, test plan 3.3).
            if (!wasInGame || player.ConnectionState == EnumClientState.Connecting)
            {
                return;
            }

            if (_postLeaves)
            {
                _discord.EnqueueSystem(SystemMessages.PlayerDisconnected(GeneralChatListener.ShownName(player), count));
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not post a leave message: {0}", e);
        }
    }

    /// <summary>The server's player limit (<c>MaxClients</c>), the <c>{max}</c> in the count.</summary>
    private int MaxClients => _api.Server.Config.MaxClients;

    /// <summary>
    /// The game's online players, which include players still connecting (their data may not be set up yet), so
    /// each is reduced to its ID and whether it's admitted.
    /// </summary>
    private List<OnlinePlayer> OnlinePlayers()
    {
        var online = new List<OnlinePlayer>();
        foreach (IPlayer player in _api.World.AllOnlinePlayers)
        {
            if (player is IServerPlayer serverPlayer && serverPlayer.PlayerUID is { } uid)
            {
                online.Add(new OnlinePlayer(uid, IsAdmitted(serverPlayer.ConnectionState)));
            }
        }

        return online;
    }

    /// <summary>
    /// Past the server's <c>MaxClients</c> check, so counted as online: admitted, loaded or playing. Not players still
    /// connecting or waiting in the connection queue.
    /// </summary>
    private static bool IsAdmitted(EnumClientState state) =>
        state is EnumClientState.Admitted or EnumClientState.Connected or EnumClientState.Playing;
}
