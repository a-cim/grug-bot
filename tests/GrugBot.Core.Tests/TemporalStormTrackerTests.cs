using System.Collections.Generic;
using GrugBot.Core.TemporalStorms;
using Xunit;

namespace GrugBot.Core.Tests;

public class TemporalStormTrackerTests
{
    private const TemporalStormStrength Heavy = TemporalStormStrength.Heavy;

    // Readings between storms: only the days until the next storm matter.
    private static TemporalStormReading Before(double daysLeft, TemporalStormStrength strength = Heavy) =>
        new(false, daysLeft, -1, strength);

    // Readings during a storm: the storm started `sinceStart` days ago and has `activeDaysLeft` to go.
    private static TemporalStormReading During(double activeDaysLeft, double sinceStart = 0.1, TemporalStormStrength strength = Heavy) =>
        new(true, -sinceStart, activeDaysLeft, strength);

    // After the game has ended a storm and prepared the next one.
    private static TemporalStormReading After(double daysUntilNext = 5, TemporalStormStrength strength = Heavy) =>
        new(false, daysUntilNext, -0.01, strength);

    private static TemporalStormAnnouncement Approaching(TemporalStormStrength strength = Heavy) =>
        new(TemporalStormPhase.Approaching, strength);

    private static TemporalStormAnnouncement Imminent(TemporalStormStrength strength = Heavy) =>
        new(TemporalStormPhase.Imminent, strength);

    private static TemporalStormAnnouncement Waning(TemporalStormStrength strength = Heavy) =>
        new(TemporalStormPhase.Waning, strength);

    /// <summary>Feeds every reading in order and returns what was announced, skipping readings that announced nothing.</summary>
    private static List<TemporalStormAnnouncement> Run(TemporalStormTracker tracker, params TemporalStormReading[] readings)
    {
        var announced = new List<TemporalStormAnnouncement>();
        foreach (TemporalStormReading reading in readings)
        {
            if (tracker.Update(reading) is TemporalStormAnnouncement announcement)
            {
                announced.Add(announcement);
            }
        }

        return announced;
    }

    private static List<TemporalStormAnnouncement> Run(params TemporalStormReading[] readings) =>
        Run(new TemporalStormTracker(), readings);

    /// <summary>A whole storm, ticking down from well before it to well after, as the game reports it.</summary>
    private static TemporalStormReading[] NaturalStorm(TemporalStormStrength strength = Heavy) =>
        new[]
        {
            Before(0.5, strength),
            Before(0.34, strength),
            Before(0.2, strength),
            Before(0.04, strength),
            Before(0.025, strength),
            Before(0.02, strength),
            Before(0.01, strength),
            Before(0, strength),
            During(0.3, 0.001, strength),
            During(0.1, 0.2, strength),
            During(0.019, 0.28, strength),
            During(0.005, 0.3, strength),
            During(-0.001, 0.3, strength),
            After(5, strength),
            After(4.9, strength),
        };

    [Fact]
    public void A_storm_gets_approaching_imminent_and_waning_once_each_in_order()
    {
        // Test plan 4.1–4.3.
        Assert.Equal(new[] { Approaching(), Imminent(), Waning() }, Run(NaturalStorm()));
    }

    [Fact]
    public void Repeated_readings_inside_a_window_post_once()
    {
        List<TemporalStormAnnouncement> announced = Run(
            Before(1),
            Before(0.3), Before(0.3), Before(0.29),
            Before(0.015), Before(0.015), Before(0.01),
            During(0.3), During(0.01), During(0.01), During(0.005));

        Assert.Equal(new[] { Approaching(), Imminent(), Waning() }, announced);
    }

    [Theory]
    [InlineData(0.35, false)]
    [InlineData(0.3499, true)]
    [InlineData(0.0301, true)]
    [InlineData(0.03, false)]
    public void Approaching_needs_strictly_between_0_03_and_0_35_days_left(double daysLeft, bool posts)
    {
        var tracker = new TemporalStormTracker();
        tracker.Update(Before(1));

        TemporalStormAnnouncement? announced = tracker.Update(Before(daysLeft));

        Assert.Equal(posts ? Approaching() : (TemporalStormAnnouncement?)null, announced);
    }

    [Theory]
    [InlineData(0.025, false)]
    [InlineData(0.02, true)]
    [InlineData(0.0001, true)]
    [InlineData(0, false)]
    [InlineData(-0.001, false)]
    public void Imminent_needs_more_than_0_and_at_most_0_02_days_left(double daysLeft, bool posts)
    {
        var tracker = new TemporalStormTracker();
        tracker.Update(Before(1));

        TemporalStormAnnouncement? announced = tracker.Update(Before(daysLeft));

        Assert.Equal(posts ? Imminent() : (TemporalStormAnnouncement?)null, announced);
    }

    [Theory]
    [InlineData(0.02, false)]
    [InlineData(0.0199, true)]
    [InlineData(0.0001, true)]
    public void Waning_needs_under_0_02_days_of_the_storm_left(double activeDaysLeft, bool posts)
    {
        var tracker = new TemporalStormTracker();
        tracker.Update(Before(1));
        tracker.Update(During(0.3));

        TemporalStormAnnouncement? announced = tracker.Update(During(activeDaysLeft));

        Assert.Equal(posts ? Waning() : (TemporalStormAnnouncement?)null, announced);
    }

    [Fact]
    public void A_storm_cut_short_past_the_waning_window_still_gets_one_waning_line_when_it_ends()
    {
        // Decision #43, test plan 4.4: kills pull the end into the past while the game still says active.
        List<TemporalStormAnnouncement> announced = Run(
            Before(1), Before(0.2), Before(0.01),
            During(0.3),
            During(0.2),
            During(-0.04),
            During(-0.04),
            After(5),
            After(4.9));

        Assert.Equal(new[] { Approaching(), Imminent(), Waning() }, announced);
    }

    [Fact]
    public void A_storm_cut_short_into_the_waning_window_gets_its_waning_line_there_and_not_again()
    {
        List<TemporalStormAnnouncement> announced = Run(
            Before(1),
            During(0.3),
            During(0.01),
            During(-0.001),
            After(5));

        Assert.Equal(new[] { Waning() }, announced);
    }

    [Fact]
    public void A_storm_the_game_ends_by_clearing_its_flag_still_gets_its_waning_line()
    {
        // Active → not active without ever reading the waning window or a negative time left.
        List<TemporalStormAnnouncement> announced = Run(
            Before(1),
            During(0.2),
            new TemporalStormReading(false, -0.3, 0.1, Heavy),
            After(5));

        Assert.Equal(new[] { Waning() }, announced);
    }

    [Fact]
    public void The_waning_line_is_posted_the_moment_the_storm_ends()
    {
        var tracker = new TemporalStormTracker();
        tracker.Update(Before(1));
        tracker.Update(During(0.2));

        Assert.Equal((TemporalStormAnnouncement?)Waning(), tracker.Update(During(-0.03)));
        Assert.Null(tracker.Update(During(-0.03)));
        Assert.Null(tracker.Update(After(5)));
    }

    [Fact]
    public void Restart_mid_storm_posts_only_the_waning_line_later()
    {
        // Test plan 4.5: the first reading after a restart is already inside the storm.
        List<TemporalStormAnnouncement> announced = Run(
            During(0.2),
            During(0.1),
            During(0.01),
            During(-0.001),
            After(5));

        Assert.Equal(new[] { Waning() }, announced);
    }

    [Fact]
    public void Restart_mid_storm_that_is_then_cut_short_still_gets_its_waning_line()
    {
        Assert.Equal(new[] { Waning() }, Run(During(0.2), During(-0.05), After(5)));
    }

    [Fact]
    public void Restart_inside_the_waning_window_does_not_repeat_the_waning_line()
    {
        Assert.Empty(Run(During(0.01), During(0.005), During(-0.001), After(5)));
    }

    [Fact]
    public void Restart_while_a_storm_approaches_does_not_repeat_approaching()
    {
        Assert.Equal(new[] { Imminent(), Waning() }, Run(Before(0.2), Before(0.1), Before(0.01), During(0.3), During(0.01)));
    }

    [Fact]
    public void Restart_while_a_storm_is_imminent_does_not_repeat_imminent()
    {
        Assert.Equal(new[] { Waning() }, Run(Before(0.01), Before(0.005), During(0.3), During(0.01)));
    }

    [Fact]
    public void Restart_between_the_approaching_and_imminent_windows_still_posts_imminent()
    {
        Assert.Equal(new[] { Imminent() }, Run(Before(0.025), Before(0.02)));
    }

    [Fact]
    public void The_first_reading_posts_nothing_even_inside_a_window()
    {
        var tracker = new TemporalStormTracker();

        Assert.Null(tracker.Update(Before(0.2)));
    }

    [Fact]
    public void The_next_storm_after_one_ends_gets_its_own_lines()
    {
        var readings = new List<TemporalStormReading>();
        readings.AddRange(NaturalStorm());
        readings.AddRange(NaturalStorm());

        Assert.Equal(
            new[] { Approaching(), Imminent(), Waning(), Approaching(), Imminent(), Waning() },
            Run(readings.ToArray()));
    }

    [Fact]
    public void The_next_storm_after_one_cut_short_gets_its_own_lines()
    {
        List<TemporalStormAnnouncement> announced = Run(
            Before(1), Before(0.2), Before(0.01),
            During(0.3), During(-0.05),
            After(3), After(0.2), After(0.01),
            During(0.3), During(0.01));

        Assert.Equal(new[] { Approaching(), Imminent(), Waning(), Approaching(), Imminent(), Waning() }, announced);
    }

    [Fact]
    public void The_next_storm_is_announced_even_when_it_is_prepared_already_inside_the_approaching_window()
    {
        // A short gap between storms: the next one is prepared less than 0.35 days away.
        List<TemporalStormAnnouncement> announced = Run(Before(1), During(0.3), During(-0.001), After(0.3), After(0.01));

        Assert.Equal(new[] { Waning(), Approaching(), Imminent() }, announced);
    }

    [Fact]
    public void Strength_is_read_for_each_storm()
    {
        var readings = new List<TemporalStormReading>();
        readings.AddRange(NaturalStorm(TemporalStormStrength.Light));
        readings.AddRange(NaturalStorm(TemporalStormStrength.Medium));

        Assert.Equal(
            new[]
            {
                Approaching(TemporalStormStrength.Light), Imminent(TemporalStormStrength.Light), Waning(TemporalStormStrength.Light),
                Approaching(TemporalStormStrength.Medium), Imminent(TemporalStormStrength.Medium), Waning(TemporalStormStrength.Medium),
            },
            Run(readings.ToArray()));
    }

    [Fact]
    public void Strength_is_read_when_each_line_is_posted()
    {
        // The game can settle the strength after the approaching line; imminent names it as it is then.
        List<TemporalStormAnnouncement> announced = Run(
            Before(1, TemporalStormStrength.Light),
            Before(0.2, TemporalStormStrength.Light),
            Before(0.01, TemporalStormStrength.Heavy));

        Assert.Equal(new[] { Approaching(TemporalStormStrength.Light), Imminent(TemporalStormStrength.Heavy) }, announced);
    }

    [Fact]
    public void A_storm_started_early_skips_the_lines_it_jumped_past()
    {
        // /nexttempstorm starting the storm now: approaching and imminent aren't posted late.
        Assert.Equal(new[] { Waning() }, Run(Before(3), Before(0), During(0.3), During(0.01)));
    }

    [Fact]
    public void A_storm_started_after_approaching_skips_imminent()
    {
        Assert.Equal(new[] { Approaching(), Waning() }, Run(Before(1), Before(0.2), During(0.3), During(0.01)));
    }

    [Fact]
    public void Time_jumping_past_approaching_still_posts_imminent()
    {
        Assert.Equal(new[] { Imminent() }, Run(Before(1), Before(0.01), Before(0.005)));
    }

    [Fact]
    public void A_storm_moved_further_away_is_announced_again_when_it_comes_back_into_range()
    {
        List<TemporalStormAnnouncement> announced = Run(Before(1), Before(0.2), Before(2), Before(0.2), Before(0.01));

        Assert.Equal(new[] { Approaching(), Approaching(), Imminent() }, announced);
    }

    [Fact]
    public void Nothing_is_posted_while_the_next_storm_is_far_off()
    {
        Assert.Empty(Run(Before(5), Before(3), Before(1), Before(0.36), Before(0.35)));
    }
}
