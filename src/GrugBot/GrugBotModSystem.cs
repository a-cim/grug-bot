using System;
using GrugBot.Core.Config;
using GrugBot.Core.Formatting;
using GrugBot.Core.Lifecycle;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Entry point of the grug-bot server mod. It loads or creates <c>ModConfig/grugbot.json</c>, works out
/// where the Discord token comes from, says clearly when something essential is missing, and otherwise
/// starts the Discord connection, the chat relay in both directions, the server's start and stop messages
/// and the temporal storm messages.
/// Nothing in here may throw: a broken config must never stop the server from starting (01-architecture § Lifecycle).
/// </summary>
/// <remarks>
/// Logging rules: unexpected errors log the full exception (type, message and stack trace). One case is
/// deliberate: an unreadable config file logs only the message, because that is almost always a typo
/// and the message already gives the line and position. Text we don't control goes
/// through <c>{0}</c> format arguments, so stray curly braces in it can't break the log call.
/// <para>
/// Shutdown: the game's <c>Shutdown</c> run phase and <see cref="Dispose"/> both lead to <see cref="ShutDownRelay"/>,
/// and only the first does anything. It marks the run as shutting down (<see cref="ServerRunState.IsShuttingDown"/>,
/// which leave messages read, decision #22), queues "Server shutdown", flushes with a timeout (gotcha #18), then
/// unsubscribes everything and disposes the Discord client. Whichever order the game calls them in, the flush comes
/// before the client is torn down.
/// </para>
/// </remarks>
public sealed class GrugBotModSystem : ModSystem
{
    /// <summary>
    /// Load late, after the usual mods (most keep the default of 0.1), so our <c>PlayerChat</c> handler is
    /// subscribed after theirs and sees whether they consumed a message (decision #24).
    /// </summary>
    private const double LateExecuteOrder = 100;

    /// <summary>
    /// How long shutdown waits for the last lines to reach Discord (gotcha #18). The server's only wait on
    /// Discord; it's skipped when Discord is down, refused us, or nothing is queued.
    /// </summary>
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(3);

    private ServerRunState? _runState;
    private bool _postStartStop;
    private DiscordConnection? _discord;
    private GeneralChatListener? _generalChat;
    private ChatChannelListener? _chatChannel;
    private JoinLeaveListener? _joinsLeaves;
    private TemporalStormWatcher? _temporalStorms;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override double ExecuteOrder() => LateExecuteOrder;

    public override void StartServerSide(ICoreServerAPI api)
    {
        ILogger log = Mod.Logger;

        try
        {
            GrugBotConfig? config = LoadOrCreateConfig(api, log);
            if (config is null)
            {
                return;
            }

            ResolvedToken token = DiscordTokenResolver.Resolve(
                Environment.GetEnvironmentVariable(DiscordTokenResolver.EnvironmentVariableName),
                config.DiscordToken);

            if (!token.IsMissing)
            {
                // DescribeSource never contains the token itself (decision #27).
                log.Notification("Using the Discord token from {0}.", token.DescribeSource());
            }

            string? missing = ConfigCheck.DescribeMissing(token, config);
            if (missing is not null)
            {
                log.Warning(missing);
                return;
            }

            StartRelay(api, log, token, config);
        }
        catch (Exception e)
        {
            log.Error("grug-bot failed to start and is off. The server carries on normally. Cause: {0}", e);
            StopRelay();
        }
    }

    public override void Dispose()
    {
        ShutDownRelay();
        base.Dispose();
    }

    /// <summary>
    /// Subscribes to game events once, here and never on Discord's Ready (gotcha #11), then starts the
    /// Discord login without waiting for it (01-architecture § Lifecycle).
    /// </summary>
    private void StartRelay(ICoreServerAPI api, ILogger log, ResolvedToken token, GrugBotConfig config)
    {
        _runState = new ServerRunState();
        _discord = new DiscordConnection(log, token, config.ChatChannelId, config.SystemChannelId);
        _generalChat = new GeneralChatListener(api, log, _discord, config.EscapeMarkdown);
        _generalChat.Subscribe();

        // Off stops Discord → Game only; Game → Discord carries on (test plan 5.10).
        if (config.Relay.DiscordToGame)
        {
            _chatChannel = new ChatChannelListener(api, log, _discord, config);
            _chatChannel.Subscribe();
        }
        else
        {
            log.Notification("Relay.DiscordToGame is off in {0}, so messages from Discord aren't shown in game.", GrugBotConfig.DisplayPath);
        }

        // Off stops both the start and the stop message (test plan 3.8). The run state is kept either way.
        _postStartStop = config.Relay.ServerStartStop;
        if (!_postStartStop)
        {
            log.Notification("Relay.ServerStartStop is off in {0}, so Discord isn't told when the server starts or stops.", GrugBotConfig.DisplayPath);
        }

        // Relay.Joins and Relay.Leaves switch their lines independently; the presence is always kept up to date (#7).
        _joinsLeaves = new JoinLeaveListener(api, log, _discord, _runState, config.Relay);
        _joinsLeaves.Subscribe();

        StartTemporalStorms(api, log, _discord, config);

        // The game has no way to take these back. After the relay has stopped they find nothing and return.
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);

        _discord.Start();
    }

    /// <summary>
    /// Starts the temporal storm messages (#9) unless <c>Relay.TemporalStorms</c> is off (test plan 4.6). A failure here
    /// turns off only the storm messages; the relay carries on.
    /// </summary>
    private void StartTemporalStorms(ICoreServerAPI api, ILogger log, DiscordConnection discord, GrugBotConfig config)
    {
        if (!config.Relay.TemporalStorms)
        {
            log.Notification("Relay.TemporalStorms is off in {0}, so Discord isn't told about temporal storms.", GrugBotConfig.DisplayPath);
            return;
        }

        try
        {
            _temporalStorms = new TemporalStormWatcher(api, log, discord);
            _temporalStorms.Subscribe();
        }
        catch (Exception e)
        {
            log.Error("grug-bot could not start its temporal storm messages; the rest of the relay carries on. Cause: {0}", e);
            _temporalStorms?.Dispose();
            _temporalStorms = null;
        }
    }

    /// <summary>
    /// The first game tick is about to run: the server is up. Queues "Server started", which waits there until
    /// Discord is ready (decision #23). The game enters this phase once per run; Discord's Ready, which can fire
    /// many times, never queues it (gotcha #11, test plan 6.2).
    /// </summary>
    private void OnRunGame()
    {
        try
        {
            if (_runState is null || _discord is null || !_runState.TryMarkStarted())
            {
                return;
            }

            if (_postStartStop)
            {
                _discord.EnqueueSystem(SystemMessages.ServerStarted);
            }
        }
        catch (Exception e)
        {
            Mod.Logger.Error("grug-bot could not queue the server started message: {0}", e);
        }
    }

    /// <summary>Shutdown has begun (the game's <c>Shutdown</c> run phase, test plan 6.1).</summary>
    private void OnShutdown() => ShutDownRelay();

    /// <summary>
    /// Says goodbye on Discord, then stops the relay. Runs on the game thread, from the <c>Shutdown</c> run phase
    /// or from <see cref="Dispose"/>, whichever comes first; the second call finds nothing to do. "Server shutdown"
    /// is only posted after a "Server started": a server that never got as far as running posts neither.
    /// </summary>
    private void ShutDownRelay()
    {
        ServerRunState? runState = _runState;
        DiscordConnection? discord = _discord;
        if (runState is null || discord is null)
        {
            return;
        }

        try
        {
            bool started = runState.HasStarted;
            if (runState.TryBeginShutdown())
            {
                if (started && _postStartStop)
                {
                    discord.EnqueueSystem(SystemMessages.ServerShutdown);
                }

                // Also posts chat lines still queued. Returns at once if Discord is down or refused us.
                discord.FlushBeforeShutdown(ShutdownFlushTimeout);
            }
        }
        catch (Exception e)
        {
            Mod.Logger.Error("grug-bot could not post the server shutdown message: {0}", e);
        }
        finally
        {
            StopRelay();
        }
    }

    /// <summary>
    /// Unsubscribes from game events and lets go of Discord, dropping anything still queued. Safe to call more
    /// than once. Shutdown goes through <see cref="ShutDownRelay"/> first, which posts what's queued.
    /// </summary>
    private void StopRelay()
    {
        _temporalStorms?.Dispose();
        _temporalStorms = null;
        _joinsLeaves?.Dispose();
        _joinsLeaves = null;
        _chatChannel?.Dispose();
        _chatChannel = null;
        _generalChat?.Dispose();
        _generalChat = null;
        _discord?.Dispose();
        _discord = null;
        _runState = null;
    }

    /// <summary>
    /// Loads the config (saving an older file back upgraded), or writes one with defaults if the file doesn't exist yet.
    /// Returns <c>null</c> (after logging why) if the existing file can't be read; that file is left untouched.
    /// </summary>
    private static GrugBotConfig? LoadOrCreateConfig(ICoreServerAPI api, ILogger log)
    {
        GrugBotConfig? config;
        try
        {
            config = api.LoadModConfig<GrugBotConfig>(GrugBotConfig.FileName);
        }
        catch (Exception e)
        {
            // Message only, on purpose: see the logging rules on this class.
            log.Error(
                "grug-bot could not read {0}, so the relay is off. Fix the file (or delete it to get a fresh one) and restart. Cause: {1}",
                GrugBotConfig.DisplayPath,
                e.Message);
            return null;
        }

        if (config is not null)
        {
            config.FillMissingSections();
            UpgradeOlderConfigFile(api, log);
            return config;
        }

        config = new GrugBotConfig();
        try
        {
            api.StoreModConfig<GrugBotConfig>(config, GrugBotConfig.FileName);
            log.Notification("Created {0} with default settings.", GrugBotConfig.DisplayPath);
        }
        catch (Exception e)
        {
            log.Error("grug-bot could not write {0}. Cause: {1}", GrugBotConfig.DisplayPath, e);
        }

        return config;
    }

    /// <summary>
    /// Saves back a config file written by an older grug-bot with the new settings added and every existing value kept,
    /// or warns once about a file from a newer grug-bot, or one it can't check; either is left untouched (issue #14).
    /// A file at the current version is not rewritten. <see cref="ConfigUpgrade"/> makes the decision and builds the
    /// file. The config already loaded has the same values either way, so a failure here is logged and the mod carries on.
    /// </summary>
    private static void UpgradeOlderConfigFile(ICoreServerAPI api, ILogger log)
    {
        try
        {
            // The raw JSON rather than GrugBotConfig, so values are copied as written and unknown fields are kept.
            // JsonObject.ToString() is declared nullable; with no file or no text there is nothing to upgrade.
            string? fileJson = api.LoadModConfig(GrugBotConfig.FileName)?.ToString();
            if (fileJson is null)
            {
                return;
            }

            ConfigUpgradeResult upgrade = ConfigUpgrade.Decide(fileJson);
            if (upgrade.UpgradedJson is not null)
            {
                api.StoreModConfig(new JsonObject(JToken.Parse(upgrade.UpgradedJson)), GrugBotConfig.FileName);
                // Describe names the versions only, never the token, and has no curly braces.
                log.Notification(upgrade.Describe());
            }
            else if (upgrade.Action is ConfigUpgradeAction.NewerThanMod or ConfigUpgradeAction.Unreadable)
            {
                // Versions only for a newer file, a fixed sentence for an unreadable one: never file content.
                log.Warning(upgrade.Describe());
            }
        }
        catch (Exception e)
        {
            log.Error(
                "grug-bot could not upgrade {0}; it carries on with the settings it read. Cause: {1}",
                GrugBotConfig.DisplayPath,
                e);
        }
    }
}
