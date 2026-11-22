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
/// With <c>Relay.DeathCount</c> on, the line ends with the player's death count from thebasics' stats,
/// <c>This is death number 7.</c> (#32, decision #52, test plan 3.9). thebasics adds this death to its count in its own
/// <c>PlayerDeath</c> handler, which may run before or after this one, so the count is read on the game thread a tick
/// later and the line is queued only then; it still posts exactly once. The sentence is worked out at once, while the game
/// still has everything it needs. A missing or 0 count means no suffix; a count that can't be read logs one warning for the
/// run and the line posts without it. With the setting off, the line is queued at once, as before.
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

    /// <summary>
    /// The player mod data key under which thebasics keeps the death count <c>/playerstats</c> shows: an <c>int</c> written
    /// with <c>SerializerUtil</c>. Read through the public player API only; no thebasics code (ADR 0002).
    /// </summary>
    private const string TheBasicsDeathCountKey = "BASIC_COUNT_DEATHS";

    /// <summary>The category the game's frame profiler shows the tick-later count read under.</summary>
    private const string DeathCountTaskCode = "grugbot-deathcount";

    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly bool _deathCount;

    private VanillaDeathMessage? _vanilla;
    private bool _lookedForVanilla;
    private bool _warnedFallback;
    private bool _warnedDeathCount;
    private bool _toldNoDeathCount;
    private bool _toldDisabled;
    private bool _subscribed;
    private bool _disposed;

    /// <param name="deathCount"><c>Relay.DeathCount</c>: end each line with the player's death count from thebasics.</param>
    public DeathListener(ICoreServerAPI api, ILogger log, DiscordConnection discord, bool deathCount)
    {
        _api = api;
        _log = log;
        _discord = discord;
        _deathCount = deathCount;
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

            // Worked out now: a tick later the game may no longer have the player's client or the damage source.
            string shownName = GeneralChatListener.ShownName(byPlayer);
            string? sentence = VanillaSentence(byPlayer, damageSource);
            string? killerName = KillerName(byPlayer, damageSource);

            if (!_deathCount || !PostAfterThisTick(byPlayer, sentence, shownName, killerName))
            {
                Post(sentence, shownName, killerName, 0);
            }
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not post a death message: {0}", e);
        }
    }

    /// <summary>
    /// Queues the line, with the death count, for the game thread's next round of tasks, which runs after every
    /// <c>PlayerDeath</c> handler, thebasics' included. Returns false (after warning once) if that couldn't be arranged,
    /// so the caller posts the line at once without the count.
    /// </summary>
    private bool PostAfterThisTick(IServerPlayer byPlayer, string? sentence, string shownName, string? killerName)
    {
        try
        {
            _api.Event.EnqueueMainThreadTask(() => PostWithDeathCount(byPlayer, sentence, shownName, killerName), DeathCountTaskCode);
            return true;
        }
        catch (Exception e)
        {
            WarnDeathCountOnce(e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    /// <summary>Runs on the game thread a tick after the death. Nothing here throws into the game.</summary>
    private void PostWithDeathCount(IServerPlayer byPlayer, string? sentence, string shownName, string? killerName)
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            Post(sentence, shownName, killerName, DeathCount(byPlayer));
        }
        catch (Exception e)
        {
            _log.Error("grug-bot could not post a death message: {0}", e);
        }
    }

    /// <summary>Builds the line (with the count when <paramref name="deathNumber"/> is 1 or more) and queues it.</summary>
    private void Post(string? sentence, string shownName, string? killerName, int deathNumber)
    {
        DeathLine line = DeathLine.For(sentence, shownName, killerName, deathNumber);
        if (line.IsFallback)
        {
            WarnFallbackOnce("the game gave no death sentence");
        }

        _discord.EnqueueSystem(line.Message);
    }

    /// <summary>
    /// The player's death count as thebasics keeps it, this death included, or 0 when there is none (thebasics isn't
    /// installed, or its player stats or Deaths stat are off). The count is kept with
    /// the player's saved data, so it can still be read if they left during the tick. Anything that goes wrong reading it
    /// warns once per run and gives 0. Never throws.
    /// </summary>
    private int DeathCount(IServerPlayer byPlayer)
    {
        int count;
        try
        {
            byte[]? data = byPlayer?.GetModdata(TheBasicsDeathCountKey);
            count = data is null ? 0 : Vintagestory.API.Util.SerializerUtil.Deserialize<int>(data, 0);
        }
        catch (Exception e)
        {
            WarnDeathCountOnce(e.GetType().Name + ": " + e.Message);
            return 0;
        }

        if (count < 1 && !_toldNoDeathCount)
        {
            _toldNoDeathCount = true;
            _log.Notification(
                "Relay.DeathCount is on, but thebasics has no death count for a player who died, so their death line has no count."
                + " Is thebasics installed, with its player stats and Deaths stat on? Shown once per run.");
        }

        return count;
    }

    private void WarnDeathCountOnce(string reason)
    {
        if (_warnedDeathCount)
        {
            return;
        }

        _warnedDeathCount = true;
        _log.Warning("grug-bot could not read the player's death count from thebasics ({0}), so death lines go without it. Shown once per run.", reason);
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
