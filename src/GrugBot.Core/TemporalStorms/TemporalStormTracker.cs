namespace GrugBot.Core.TemporalStorms;

/// <summary>
/// Decides when a temporal storm phase should be posted, from one <see cref="TemporalStormReading"/> a second.
/// Approaching, imminent and waning are each posted at most once per storm, with th3essentials' thresholds
/// (decision #42), and every storm gets exactly one waning line, even one cut short (decision #43).
/// One instance per server run; call it from one thread (the game thread).
/// </summary>
/// <remarks>
/// <para>
/// <b>Storm running:</b> the game's active flag is set and some of the storm is left. A storm counts as over as
/// soon as nothing is left, even before the game clears its flag: another mod (on the owner's server, Rustwall's
/// drifter kills) can pull the end time into the past, past the whole waning window. If the waning line hasn't
/// been posted by then, it's posted at that moment.
/// </para>
/// <para>
/// <b>First reading:</b> the tracker can't tell a restart from a first start, and a restart mid-storm must not
/// repeat earlier phases (test plan 4.5). So the first reading posts nothing and marks every phase whose window
/// has already been reached as done: a restart while a storm approaches doesn't post "approaching" again, a
/// restart during a storm posts only its waning line, later. The cost: a server that starts (rather than
/// restarts) inside a window skips that one line, which the game itself announced in game anyway.
/// </para>
/// <para>
/// <b>Missed windows aren't posted late.</b> When a storm starts before its approaching or imminent line was
/// posted (<c>/nexttempstorm</c>, or time moved past the window), those lines are skipped: the storm is
/// already here.
/// </para>
/// <para>
/// <b>Next storm:</b> approaching and imminent are armed again when a storm ends, and whenever the next storm
/// is at least <see cref="ApproachingFromDaysLeft"/> away, so a storm moved further off is announced again
/// when it comes back into range. The strength is read when each line is posted, so a storm of a different
/// strength than the last is named correctly.
/// </para>
/// </remarks>
public sealed class TemporalStormTracker
{
    /// <summary>Approaching while the next storm is less than this many days away (exclusive).</summary>
    public const double ApproachingFromDaysLeft = 0.35;

    /// <summary>Approaching while the next storm is more than this many days away (exclusive).</summary>
    public const double ApproachingUntilDaysLeft = 0.03;

    /// <summary>Imminent while the next storm is more than 0 and at most this many days away.</summary>
    public const double ImminentFromDaysLeft = 0.02;

    /// <summary>Waning once less than this many days of the running storm remain (exclusive).</summary>
    public const double WaningFromActiveDaysLeft = 0.02;

    private bool _seenFirstReading;
    private bool _stormRunning;
    private bool _approachingDone;
    private bool _imminentDone;
    private bool _waningDone;

    /// <summary>
    /// Takes the latest reading and returns the phase to post now, or <c>null</c> when there's nothing to post.
    /// At most one phase per reading.
    /// </summary>
    public TemporalStormAnnouncement? Update(TemporalStormReading reading)
    {
        bool running = reading.IsStormActive && reading.ActiveDaysLeft > 0;

        if (!_seenFirstReading)
        {
            _seenFirstReading = true;
            _stormRunning = running;
            MarkPassedPhasesDone(reading, running);
            return null;
        }

        if (_stormRunning && !running)
        {
            // The storm is over, naturally or cut short. Its waning line is owed if it hasn't been posted (decision #43).
            _stormRunning = false;
            bool waningOwed = !_waningDone;
            ArmForNextStorm();
            return waningOwed ? Waning(reading) : null;
        }

        if (running)
        {
            if (!_stormRunning)
            {
                // A new storm. Lines for windows it skipped past aren't posted late.
                _stormRunning = true;
                _approachingDone = true;
                _imminentDone = true;
                _waningDone = false;
            }

            if (!_waningDone && reading.ActiveDaysLeft < WaningFromActiveDaysLeft)
            {
                _waningDone = true;
                return Waning(reading);
            }

            return null;
        }

        return BetweenStorms(reading);
    }

    private TemporalStormAnnouncement? BetweenStorms(TemporalStormReading reading)
    {
        double daysLeft = reading.DaysUntilNextStorm;

        if (daysLeft >= ApproachingFromDaysLeft)
        {
            ArmForNextStorm();
            return null;
        }

        if (IsImminent(daysLeft))
        {
            if (_imminentDone)
            {
                return null;
            }

            _imminentDone = true;
            _approachingDone = true;
            return new TemporalStormAnnouncement(TemporalStormPhase.Imminent, reading.NextStormStrength);
        }

        if (IsApproaching(daysLeft) && !_approachingDone)
        {
            _approachingDone = true;
            return new TemporalStormAnnouncement(TemporalStormPhase.Approaching, reading.NextStormStrength);
        }

        return null;
    }

    private void MarkPassedPhasesDone(TemporalStormReading reading, bool running)
    {
        if (running)
        {
            _approachingDone = true;
            _imminentDone = true;
            _waningDone = reading.ActiveDaysLeft < WaningFromActiveDaysLeft;
            return;
        }

        double daysLeft = reading.DaysUntilNextStorm;
        _approachingDone = daysLeft < ApproachingFromDaysLeft;
        _imminentDone = daysLeft <= ImminentFromDaysLeft;
    }

    private void ArmForNextStorm()
    {
        _approachingDone = false;
        _imminentDone = false;
    }

    private static bool IsApproaching(double daysLeft) =>
        daysLeft > ApproachingUntilDaysLeft && daysLeft < ApproachingFromDaysLeft;

    private static bool IsImminent(double daysLeft) => daysLeft > 0 && daysLeft <= ImminentFromDaysLeft;

    // Until the game prepares the next storm, its "next storm" strength is the running storm's.
    private static TemporalStormAnnouncement Waning(TemporalStormReading reading) =>
        new(TemporalStormPhase.Waning, reading.NextStormStrength);
}
