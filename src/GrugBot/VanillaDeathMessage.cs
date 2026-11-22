using System;
using System.Collections;
using System.Reflection;
using Vintagestory.API.Common;

namespace GrugBot;

/// <summary>
/// The game's own death sentence, as players see it in General (#8, gotcha #19). The game builds it in a private
/// <c>ServerSystemEntitySimulation.GetDeathMessage(client, damageSource)</c> in VintagestoryLib, which th3essentials
/// calls by reflection; this does the same, adapted from th3essentials (THIRD-PARTY-NOTICES.md).
/// </summary>
/// <remarks>
/// Pure reflection by member and type name, with no compile-time reference to VintagestoryLib: if a game update renames or
/// changes any of the members, <see cref="Find"/> says what's missing and the caller falls back to its own wording, instead
/// of the mod failing to load. No Harmony (decision #11). The members, as in 1.22.7:
/// <list type="bullet">
/// <item><c>ServerMain.Systems</c>, a non-public instance field holding the server systems (<c>api.World</c> is the <c>ServerMain</c>);</item>
/// <item>the system whose type is named <c>ServerSystemEntitySimulation</c>, and its private instance <c>GetDeathMessage</c>,
/// taking the server's client object and a <see cref="DamageSource"/> and returning the sentence;</item>
/// <item><c>ServerMain.GetClient(int clientId)</c>, which gives the client object for a player's <c>ClientId</c>.</item>
/// </list>
/// Only used on the game thread.
/// </remarks>
internal sealed class VanillaDeathMessage
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private const string SystemsFieldName = "Systems";
    private const string SimulationTypeName = "ServerSystemEntitySimulation";
    private const string GetDeathMessageName = "GetDeathMessage";
    private const string GetClientName = "GetClient";

    private readonly object _server;
    private readonly MethodInfo _getClient;
    private readonly object _simulation;
    private readonly MethodInfo _getDeathMessage;

    private VanillaDeathMessage(object server, MethodInfo getClient, object simulation, MethodInfo getDeathMessage)
    {
        _server = server;
        _getClient = getClient;
        _simulation = simulation;
        _getDeathMessage = getDeathMessage;
    }

    /// <summary>
    /// Finds the game's members on <paramref name="server"/> (<c>api.World</c>). Returns <c>null</c> with
    /// <paramref name="problem"/> saying which one is missing; never throws.
    /// </summary>
    public static VanillaDeathMessage? Find(object server, out string? problem)
    {
        try
        {
            Type serverType = server.GetType();

            FieldInfo? systemsField = FindField(serverType, SystemsFieldName);
            if (systemsField?.GetValue(server) is not IEnumerable systems)
            {
                problem = "the server has no " + SystemsFieldName + " list";
                return null;
            }

            object? simulation = null;
            foreach (object? system in systems)
            {
                if (system is not null && IsNamed(system.GetType(), SimulationTypeName))
                {
                    simulation = system;
                    break;
                }
            }

            if (simulation is null)
            {
                problem = "the server has no " + SimulationTypeName;
                return null;
            }

            MethodInfo? getDeathMessage = FindMethod(simulation.GetType(), GetDeathMessageName, IsGetDeathMessage);
            if (getDeathMessage is null)
            {
                problem = SimulationTypeName + "." + GetDeathMessageName + "(client, DamageSource) is missing";
                return null;
            }

            Type clientType = getDeathMessage.GetParameters()[0].ParameterType;
            MethodInfo? getClient = FindMethod(serverType, GetClientName, method => IsGetClient(method, clientType));
            if (getClient is null)
            {
                problem = serverType.Name + "." + GetClientName + "(int) is missing";
                return null;
            }

            problem = null;
            return new VanillaDeathMessage(server, getClient, simulation, getDeathMessage);
        }
        catch (Exception e)
        {
            problem = e.GetType().Name + ": " + e.Message;
            return null;
        }
    }

    /// <summary>
    /// The game's sentence for the death of the player with <paramref name="clientId"/>, or <c>null</c> when the game
    /// has no client for them or returns nothing. Exceptions from the game (wrapped in
    /// <see cref="TargetInvocationException"/>) reach the caller, which falls back.
    /// </summary>
    public string? Get(int clientId, DamageSource? damageSource)
    {
        object? client = _getClient.Invoke(_server, new object[] { clientId });
        if (client is null)
        {
            return null;
        }

        return _getDeathMessage.Invoke(_simulation, new object?[] { client, damageSource }) as string;
    }

    // (client, DamageSource) → string.
    private static bool IsGetDeathMessage(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        return method.ReturnType == typeof(string)
            && parameters.Length == 2
            && !parameters[0].ParameterType.IsValueType
            && parameters[1].ParameterType.IsAssignableFrom(typeof(DamageSource));
    }

    // (int) → something GetDeathMessage takes as its client.
    private static bool IsGetClient(MethodInfo method, Type clientType)
    {
        ParameterInfo[] parameters = method.GetParameters();
        return parameters.Length == 1
            && parameters[0].ParameterType == typeof(int)
            && clientType.IsAssignableFrom(method.ReturnType);
    }

    // Private members of a base class aren't returned for the derived type, so each type is searched in turn.
    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? t = type; t is not null; t = t.BaseType)
        {
            if (t.GetField(name, AnyInstance) is FieldInfo field)
            {
                return field;
            }
        }

        return null;
    }

    private static MethodInfo? FindMethod(Type type, string name, Func<MethodInfo, bool> matches)
    {
        for (Type? t = type; t is not null; t = t.BaseType)
        {
            foreach (MethodInfo method in t.GetMethods(AnyInstance))
            {
                if (string.Equals(method.Name, name, StringComparison.Ordinal) && matches(method))
                {
                    return method;
                }
            }
        }

        return null;
    }

    // The type itself or a base type, so a subclass of the system still counts.
    private static bool IsNamed(Type type, string name)
    {
        for (Type? t = type; t is not null; t = t.BaseType)
        {
            if (string.Equals(t.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
