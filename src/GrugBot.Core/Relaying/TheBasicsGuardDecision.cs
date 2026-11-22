namespace GrugBot.Core.Relaying;

/// <summary>The outcome of <see cref="TheBasicsGuard.Decide"/>: whether General is relayed to Discord, and the one line to log about it.</summary>
public sealed class TheBasicsGuardDecision
{
    internal TheBasicsGuardDecision(bool relayGeneral, bool isWarning, string logLine)
    {
        RelayGeneral = relayGeneral;
        IsWarning = isWarning;
        LogLine = logLine;
    }

    /// <summary>Whether what players say in General still goes to the chat channel (Game → Discord).</summary>
    public bool RelayGeneral { get; }

    /// <summary>Log <see cref="LogLine"/> as a warning; otherwise as a notification (info).</summary>
    public bool IsWarning { get; }

    /// <summary>
    /// The one line to log. It can contain text from an exception, so pass it as a format argument (<c>"{0}"</c>),
    /// never as the format string itself.
    /// </summary>
    public string LogLine { get; }

    public override string ToString() => (RelayGeneral ? "Relay General" : "Don't relay General") + ": " + LogLine;
}
