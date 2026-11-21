using System;
using GrugBot.Core.Config;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Entry point of the grug-bot server mod. It loads or creates <c>ModConfig/grugbot.json</c>, works out
/// where the Discord token comes from, says clearly when something essential is missing, and otherwise
/// starts the Discord connection and the General chat relay.
/// Nothing in here may throw: a broken config must never stop the server from starting (01-architecture § Lifecycle).
/// </summary>
/// <remarks>
/// Logging rules: unexpected errors log the full exception (type, message and stack trace). One case is
/// deliberate: an unreadable config file logs only the message, because that is almost always a typo
/// and the message already gives the line and position. Text we don't control goes
/// through <c>{0}</c> format arguments, so stray curly braces in it can't break the log call.
/// </remarks>
public sealed class GrugBotModSystem : ModSystem
{
    /// <summary>
    /// Load late, after the usual mods (most keep the default of 0.1), so our <c>PlayerChat</c> handler is
    /// subscribed after theirs and sees whether they consumed a message (decision #24).
    /// </summary>
    private const double LateExecuteOrder = 100;

    private DiscordConnection? _discord;
    private GeneralChatListener? _generalChat;

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
        StopRelay();
        base.Dispose();
    }

    /// <summary>
    /// Subscribes to game events once, here and never on Discord's Ready (gotcha #11), then starts the
    /// Discord login without waiting for it (01-architecture § Lifecycle).
    /// </summary>
    private void StartRelay(ICoreServerAPI api, ILogger log, ResolvedToken token, GrugBotConfig config)
    {
        _discord = new DiscordConnection(log, token, config.ChatChannelId);
        _generalChat = new GeneralChatListener(api, log, _discord, config.EscapeMarkdown);
        _generalChat.Subscribe();
        _discord.Start();
    }

    /// <summary>Unsubscribes from game events and lets go of Discord. Safe to call more than once.</summary>
    private void StopRelay()
    {
        _generalChat?.Dispose();
        _generalChat = null;
        _discord?.Dispose();
        _discord = null;
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
