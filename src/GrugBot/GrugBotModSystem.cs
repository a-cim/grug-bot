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
/// Log calls that include text we don't control (exception messages) use the format-string overloads
/// with <c>{0}</c>, so stray curly braces in that text can't break the log call.
/// </remarks>
public sealed class GrugBotModSystem : ModSystem
{
    /// <summary>The loaded config, or <c>null</c> if it could not be read.</summary>
    internal GrugBotConfig? Config { get; private set; }

    /// <summary>The Discord token and its source, or <c>null</c> until the config is loaded.</summary>
    internal ResolvedToken? Token { get; private set; }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        ILogger log = Mod.Logger;

        try
        {
            GrugBotConfig? config = LoadOrCreateConfig(api, log);
            Config = config;
            if (config is null)
            {
                return;
            }

            ResolvedToken token = DiscordTokenResolver.Resolve(
                Environment.GetEnvironmentVariable(DiscordTokenResolver.EnvironmentVariableName),
                config.DiscordToken);
            Token = token;

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
            log.Error("grug-bot failed to start and is off. The server carries on normally. Cause: {0}", e.ToString());
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
            log.Error("grug-bot could not write {0}. Cause: {1}", GrugBotConfig.DisplayPath, e.Message);
        }

        return config;
    }
}
