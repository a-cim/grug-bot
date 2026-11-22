using System;
using System.Reflection;
using GrugBot.Core.Relaying;
using Vintagestory.API.Server;

namespace GrugBot;

/// <summary>
/// Reads thebasics' Proximity chat group ID from the running mod, by reflection only: grug-bot has no compile-time
/// reference to thebasics and copies none of its code (ADR 0002, gotcha #22). <see cref="TheBasicsGuard"/> decides
/// what the result means.
/// </summary>
internal static class TheBasicsProbe
{
    /// <summary>
    /// Looks for thebasics' Proximity chat mod system and reads its <c>ProximityChatGroupId</c>. Call it once the
    /// world is running (<c>RunGame</c>): thebasics sets the ID during its own startup, which has finished by then.
    /// Never throws; anything unexpected comes back as <see cref="TheBasicsLookupOutcome.Failed"/>.
    /// </summary>
    public static TheBasicsLookup Look(ICoreServerAPI api)
    {
        try
        {
            object? system = FindProximityChatSystem(api);
            if (system is null)
            {
                // Without the mod system, the mod ID tells "not installed" apart from "installed but not found".
                return api.ModLoader.IsModEnabled(TheBasicsGuard.ModId)
                    ? TheBasicsLookup.Failed(
                        "thebasics is enabled, but its mod system " + TheBasicsGuard.ProximityChatSystemTypeName
                        + " isn't loaded (an update may have renamed it, or its Proximity chat may be turned off).")
                    : TheBasicsLookup.NotInstalled();
            }

            PropertyInfo? property = system.GetType().GetProperty(
                TheBasicsGuard.ProximityChatGroupIdPropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property is null || property.PropertyType != typeof(int) || property.GetIndexParameters().Length != 0
                || property.GetGetMethod() is null)
            {
                return TheBasicsLookup.Failed(
                    TheBasicsGuard.ProximityChatSystemTypeName + " has no readable public int property "
                    + TheBasicsGuard.ProximityChatGroupIdPropertyName + " (an update may have changed it).");
            }

            object? value = property.GetValue(system);
            return value is int proximityChatGroupId
                ? TheBasicsLookup.Found(proximityChatGroupId)
                : TheBasicsLookup.Failed(TheBasicsGuard.ProximityChatGroupIdPropertyName + " did not return an int.");
        }
        catch (Exception e)
        {
            // A throwing getter arrives wrapped in TargetInvocationException; report what it threw.
            Exception cause = e is TargetInvocationException { InnerException: { } inner } ? inner : e;
            return TheBasicsLookup.Failed(
                "reading " + TheBasicsGuard.ProximityChatGroupIdPropertyName + " threw " + cause.GetType().FullName
                + ": " + cause.Message);
        }
    }

    /// <summary>
    /// The loaded mod system whose type has thebasics' full name, or <c>null</c>. Matched by name over all loaded mod
    /// systems, so nothing here needs thebasics' types.
    /// </summary>
    private static object? FindProximityChatSystem(ICoreServerAPI api)
    {
        foreach (object system in api.ModLoader.Systems)
        {
            if (string.Equals(system.GetType().FullName, TheBasicsGuard.ProximityChatSystemTypeName, StringComparison.Ordinal))
            {
                return system;
            }
        }

        return null;
    }
}
