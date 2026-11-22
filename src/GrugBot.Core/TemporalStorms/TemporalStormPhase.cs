namespace GrugBot.Core.TemporalStorms;

/// <summary>The three temporal storm phases Discord hears about, each once per storm (decision #42).</summary>
public enum TemporalStormPhase
{
    /// <summary>The next storm is between 0.35 and 0.03 days away.</summary>
    Approaching,

    /// <summary>The next storm is at most 0.02 days away.</summary>
    Imminent,

    /// <summary>Under 0.02 days of the storm remain, or the storm ended before that line was posted (decision #43).</summary>
    Waning,
}
