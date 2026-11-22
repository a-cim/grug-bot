using System;
using GrugBot.Core.Formatting;
using GrugBot.Core.Players;
using GrugBot.Core.Sending;
using Xunit;

namespace GrugBot.Core.Tests;

public class PresenceScheduleTests
{
    private static readonly PlayerCount One = new(1, 10);
    private static readonly PlayerCount Two = new(2, 10);
    private static readonly PlayerCount Three = new(3, 10);

    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void The_presence_reads_players_online_slash_max()
    {
        // 05-message-format § Bot presence.
        Assert.Equal("players: 2/10", BotPresence.Format(Two));
    }

    [Fact]
    public void Nothing_is_due_before_any_count()
    {
        var schedule = new PresenceSchedule();

        Assert.Null(schedule.TakeDue(At(100)));
    }

    [Fact]
    public void The_count_at_startup_is_due_at_once()
    {
        var schedule = new PresenceSchedule();
        schedule.ShowAtOnce(new PlayerCount(0, 10), At(0));

        Assert.Equal(new PlayerCount(0, 10), schedule.TakeDue(At(0)));
    }

    [Fact]
    public void A_join_or_leave_is_shown_five_seconds_later()
    {
        // Decision #26.
        var schedule = new PresenceSchedule();
        schedule.Changed(Two, At(10));

        Assert.Null(schedule.TakeDue(At(14.9)));
        Assert.Equal(Two, schedule.TakeDue(At(15)));
    }

    [Fact]
    public void Each_join_or_leave_starts_the_wait_again_and_only_the_last_count_is_shown()
    {
        // Test plan 3.7: several joins and leaves in quick succession make one update.
        var schedule = new PresenceSchedule();
        schedule.Changed(Two, At(0));
        schedule.Changed(Three, At(3));
        schedule.Changed(Two, At(6));

        Assert.Null(schedule.TakeDue(At(10)));
        Assert.Equal(Two, schedule.TakeDue(At(11)));
        Assert.Null(schedule.TakeDue(At(30)));
    }

    [Fact]
    public void A_count_already_shown_is_not_sent_again()
    {
        var schedule = new PresenceSchedule();
        schedule.ShowAtOnce(One, At(0));
        schedule.TakeDue(At(0));

        // A join and a leave that cancel out.
        schedule.Changed(Two, At(1));
        schedule.Changed(One, At(2));

        Assert.Null(schedule.TakeDue(At(10)));
    }

    [Fact]
    public void After_a_reconnect_the_latest_count_is_shown_again_at_once()
    {
        var schedule = new PresenceSchedule();
        schedule.ShowAtOnce(One, At(0));
        schedule.TakeDue(At(0));

        schedule.ShowAgain(At(50));

        Assert.Equal(One, schedule.TakeDue(At(50)));
    }

    [Fact]
    public void A_reconnect_before_any_count_shows_nothing()
    {
        var schedule = new PresenceSchedule();
        schedule.ShowAgain(At(0));

        Assert.Null(schedule.TakeDue(At(100)));
    }

    [Fact]
    public void A_reconnect_during_the_wait_shows_the_latest_count_at_once()
    {
        var schedule = new PresenceSchedule();
        schedule.Changed(Two, At(0));
        schedule.ShowAgain(At(1));

        Assert.Equal(Two, schedule.TakeDue(At(1)));
    }
}
