using System;
using GrugBot.Core.Formatting;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Deaths: posts the game's own death sentence, <c>***Player Ugg got killed by a nightmare drifter***</c>, to the system
/// channel, as players see it in General (05-message-format § Deaths, issue #8, test plan 3.4). Every death is posted,
/// two quick ones as two lines (test plan 3.5).
/// </summary>
/// <remarks>
/// <para>
/// The sentence comes from <see cref="VanillaDeathMessage"/>, found by reflection at the first death. When it can't be had
/// (a member missing after a game update, the call failing, or no sentence), <see cref="DeathLine"/> posts our own
/// <c>Player Ugg died</c> instead, and one warning is logged for the run, not one per death (gotcha #19).
/// </para>
/// <para>
/// Only created when <c>Relay.Deaths</c> is on (test plan 3.8). Nothing is posted while the world config
/// <c>disableDeathMessages</c> is true, as the game shows none either; that is read at each death, so changing it in a running
/// world takes effect at once.
/// </para>
/// <para>Everything here runs on the game thread and only queues work for Discord; nothing here throws into the game.</para>
/// </remarks>
public sealed class DeathListener : IDisposable
{
    private const string DisableDeathMessagesKey = "disableDeathMessages";

    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;

    private VanillaDeathMessage? _vanilla;
    private bool _lookedForVanilla;
    private bool _warnedFallback;
    private bool _toldDisabled;
    private bool _subscribed;
    private bool _disposed;

    public DeathListener(ICoreServerAPI api, ILogger log, DiscordConnection discord)
    {
        _api = api;
        _log = log;
        _discord = discord;
    }

    /// <summary>Subscribes to the game's death event. Called once, from <c>StartServerSide</c>, never on Ready (gotcha #11).</summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _api.Event.PlayerDeath += OnPlayerDeath;

        // The world config is loaded by then. The game has no way to take this back; after Dispose it returns.
        _api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        _subscribed = true;
    }

    public void Dispose()
    {
        _disposed = true;
        if (_subscribed)
        {
            _api.Event.PlayerDeath -= OnPlayerDeath;
            _subscribed = false;
        }
    }

    /// <summary>Says once, at start-up, that deaths aren't posted in this world.</summary>
    private void OnRunGame()
    {
        try
        {
            if (!_disposed)
            {
                IsDisabledInWorld();
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not read the world's {0} setting: {1}", DisableDeathMessagesKey, e);
        }
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
    {
        try
        {
            if (IsDisabledInWorld())
            {
                return;
            }

            string shownName = GeneralChatListener.ShownName(byPlayer);
            DeathLine line = DeathLine.For(VanillaSentence(byPlayer, damageSource), shownName, KillerName(byPlayer, damageSource));
            if (line.IsFallback)
            {
                WarnFallbackOnce("the game gave no death sentence");
            }

            _discord.EnqueueSystem(line.Message);
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not post a death message: {0}", e);
        }
    }

    /// <summary>
    /// True when the world config <c>disableDeathMessages</c> is on, saying so once per run (test plan 3.4).
    /// Missing means off, as in the game.
    /// </summary>
    private bool IsDisabledInWorld()
    {
        if (!_api.World.Config.GetBool(DisableDeathMessagesKey, false))
        {
            return false;
        }

        if (!_toldDisabled)
        {
            _toldDisabled = true;
            _log.Notification("Death messages are off in this world ({0}), so Discord isn't told when players die.", DisableDeathMessagesKey);
        }

        return true;
    }

    /// <summary>The game's sentence, or <c>null</c> after warning once (gotcha #19). Never throws.</summary>
    private string? VanillaSentence(IServerPlayer byPlayer, DamageSource damageSource)
    {
        try
        {
            if (!_lookedForVanilla)
            {
                _lookedForVanilla = true;
                _vanilla = VanillaDeathMessage.Find(_api.World, out string? problem);
                if (_vanilla is null)
                {
                    WarnFallbackOnce(problem ?? "the game's death message method could not be found");
                }
            }

            return _vanilla?.Get(byPlayer.ClientId, damageSource);
        }
        catch (Exception e)
        {
            // Unwraps the TargetInvocationException from the reflected call, for a shorter warning.
            Exception cause = e.InnerException ?? e;
            WarnFallbackOnce(cause.GetType().Name + ": " + cause.Message);
            return null;
        }
    }

    private void WarnFallbackOnce(string reason)
    {
        if (_warnedFallback)
        {
            return;
        }

        _warnedFallback = true;
        _log.Warning("grug-bot could not get the game's own death message ({0}), so it posts a simpler sentence such as \"Player Ugg died\". Shown once per run.", reason);
    }

    /// <summary>
    /// The shown name of the entity that caused the death (a player's shown name, decision #36), for the fallback only.
    /// <c>null</c> when there is none, it's the player themselves, or it can't be read.
    /// </summary>
    private static string? KillerName(IServerPlayer byPlayer, DamageSource? damageSource)
    {
        try
        {
            Entity? cause = damageSource?.GetCauseEntity();
            if (cause is null || cause.EntityId == byPlayer.Entity?.EntityId)
            {
                return null;
            }

            if (cause is EntityPlayer { Player: IServerPlayer killer })
            {
                return GeneralChatListener.ShownName(killer);
            }

            return cause.GetName();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
