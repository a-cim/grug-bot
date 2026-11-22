using System;
using GrugBot.Core.Formatting;
using GrugBot.Core.TemporalStorms;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace GrugBot;

/// <summary>
/// Tells Discord about temporal storms: approaching, imminent and waning, once each per storm (#9). Once a second,
/// on the game thread, it reads the public <c>SystemTemporalStability.StormData</c> fresh, never keeping the object
/// (decision #42). No Harmony, so it never gets in the way of other mods that patch the storm, such as Rustwall, and
/// it sees their changes. <see cref="TemporalStormTracker"/> in Core decides what to post.
/// </summary>
public sealed class TemporalStormWatcher : IDisposable
{
    private const int TickIntervalMs = 1000;

    private readonly ICoreServerAPI _api;
    private readonly ILogger _log;
    private readonly DiscordConnection _discord;
    private readonly TemporalStormTracker _tracker = new();

    private SystemTemporalStability? _stability;
    private long? _listenerId;
    private bool _checkedWorld;

    // Set when storms can't be watched this run (off in the world config, or an error); every later tick returns at once.
    private bool _off;

    public TemporalStormWatcher(ICoreServerAPI api, ILogger log, DiscordConnection discord)
    {
        _api = api;
        _log = log;
        _discord = discord;
    }

    /// <summary>Starts the once-a-second check. Called once, from <c>StartServerSide</c>; ticks only begin once the world runs.</summary>
    public void Subscribe()
    {
        if (_listenerId is not null)
        {
            return;
        }

        _listenerId = _api.Event.RegisterGameTickListener(OnTick, TickIntervalMs);
    }

    public void Dispose()
    {
        if (_listenerId is long id)
        {
            _api.Event.UnregisterGameTickListener(id);
            _listenerId = null;
        }
    }

    private void OnTick(float dt)
    {
        if (_off)
        {
            return;
        }

        try
        {
            if (!_checkedWorld)
            {
                // On the first tick rather than at start-up: the world config and mod systems are loaded by then.
                _checkedWorld = true;
                if (!CanWatch())
                {
                    _off = true;
                    return;
                }
            }

            // Fresh every time (decision #42): other mods may replace or change it.
            TemporalStormRunTimeData? data = _stability!.StormData;
            if (data is null)
            {
                return;
            }

            double now = _api.World.Calendar.TotalDays;
            var reading = new TemporalStormReading(
                data.nowStormActive,
                data.nextStormTotalDays - now,
                data.stormActiveTotalDays - now,
                ToStrength(data.nextStormStrength));

            if (_tracker.Update(reading) is TemporalStormAnnouncement announcement)
            {
                _discord.EnqueueSystem(SystemMessages.TemporalStorm(Translate(announcement.LangKey)));
            }
        }
        catch (Exception e)
        {
            // Once, not every second: storm messages stop for this run, everything else carries on.
            _off = true;
            _log.Error("grug-bot could not read the temporal storm state, so storm messages are off until the server restarts. Cause: {0}", e);
        }
    }

    /// <summary>
    /// False, after saying why, when this world has no storms to report: temporal stability off (test plan 4.6),
    /// storms off, or no storm system loaded.
    /// </summary>
    private bool CanWatch()
    {
        if (!_api.World.Config.GetBool("temporalStability", true))
        {
            _log.Notification("Temporal stability is off in this world, so grug-bot posts no temporal storm messages.");
            return false;
        }

        if (string.Equals(_api.World.Config.GetString("temporalStorms", null), "off", StringComparison.Ordinal))
        {
            _log.Notification("Temporal storms are off in this world, so grug-bot posts no temporal storm messages.");
            return false;
        }

        _stability = _api.ModLoader.GetModSystem<SystemTemporalStability>();
        if (_stability is null)
        {
            _log.Warning("grug-bot found no temporal stability system, so it posts no temporal storm messages.");
            return false;
        }

        return true;
    }

    private static TemporalStormStrength ToStrength(EnumTempStormStrength strength) => strength switch
    {
        EnumTempStormStrength.Medium => TemporalStormStrength.Medium,
        EnumTempStormStrength.Heavy => TemporalStormStrength.Heavy,
        // Light, and anything a later game version adds, as th3essentials did.
        _ => TemporalStormStrength.Light,
    };

    /// <summary>
    /// The game's sentence for <paramref name="key"/> in the server's language, as players see it in game. The keys
    /// are vanilla's English sentences, so when there's no translation the key itself is the English text.
    /// </summary>
    private static string Translate(string key)
    {
        // Vanilla looks these up without a domain; "survival:" is a second try in case the domain-less lookup misses.
        foreach (string candidate in new[] { key, "survival:" + key })
        {
            string? text = Lang.Get(candidate);
            if (!string.IsNullOrWhiteSpace(text)
                && !string.Equals(text, candidate, StringComparison.Ordinal)
                && !text.EndsWith(":" + key, StringComparison.Ordinal))
            {
                return text;
            }
        }

        return key;
    }
}
