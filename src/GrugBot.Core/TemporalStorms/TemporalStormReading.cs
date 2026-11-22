namespace GrugBot.Core.TemporalStorms;

/// <summary>
/// One look at the game's temporal storm data, as plain numbers. The mod builds one a second from
/// <c>SystemTemporalStability.StormData</c> and the calendar (decision #42).
/// </summary>
/// <param name="IsStormActive">The game's <c>nowStormActive</c>.</param>
/// <param name="DaysUntilNextStorm">
/// <c>nextStormTotalDays</c> minus the calendar's <c>TotalDays</c>. Below zero while a storm runs, since the
/// running storm is the one it points at until the game prepares the next.
/// </param>
/// <param name="ActiveDaysLeft">
/// <c>stormActiveTotalDays</c> minus <c>TotalDays</c>: what's left of the running storm. Only meaningful while
/// <paramref name="IsStormActive"/> is true. Can drop below zero before the game notices the storm is over,
/// for example when another mod shortens the storm.
/// </param>
/// <param name="NextStormStrength">The game's <c>nextStormStrength</c>.</param>
public readonly record struct TemporalStormReading(
    bool IsStormActive,
    double DaysUntilNextStorm,
    double ActiveDaysLeft,
    TemporalStormStrength NextStormStrength);
