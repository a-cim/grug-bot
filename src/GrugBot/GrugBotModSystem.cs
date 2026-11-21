using System;
using GrugBot.Core.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Entry point of the grug-bot server mod. For now it loads or creates <c>ModConfig/grugbot.json</c>,
/// works out where the Discord token comes from, and says clearly when something essential is missing.
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
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

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

            // The Discord connection arrives in a later ticket (issue #3).
        }
        catch (Exception e)
        {
            log.Error("grug-bot failed to start and is off. The server carries on normally. Cause: {0}", e);
        }
    }

    /// <summary>
    /// Loads the config, or writes one with defaults if the file doesn't exist yet.
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
}
