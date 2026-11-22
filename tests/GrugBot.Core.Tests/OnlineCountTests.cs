using GrugBot.Core.Players;
using Xunit;

namespace GrugBot.Core.Tests;

public class OnlineCountTests
{
    private static readonly OnlinePlayer Grug = new("uid-grug", IsAdmitted: true);
    private static readonly OnlinePlayer Ugg = new("uid-ugg", IsAdmitted: true);
    private static readonly OnlinePlayer StillConnecting = new("uid-new", IsAdmitted: false);

    [Fact]
    public void Now_counts_only_admitted_players()
    {
        Assert.Equal(2, OnlineCount.Now(new[] { Grug, StillConnecting, Ugg }));
    }

    [Fact]
    public void Now_is_zero_with_nobody_online()
    {
        Assert.Equal(0, OnlineCount.Now(System.Array.Empty<OnlinePlayer>()));
    }

    [Fact]
    public void A_joining_player_listed_as_admitted_is_counted_once()
    {
        // Test plan 3.1: Ugg joins while Grug plays.
        Assert.Equal(2, OnlineCount.WithJoining(new[] { Grug, Ugg }, Ugg.PlayerUid));
    }

    [Fact]
    public void A_joining_player_is_counted_even_before_the_game_marks_them_admitted()
    {
        var joining = new OnlinePlayer("uid-ugg", IsAdmitted: false);

        Assert.Equal(2, OnlineCount.WithJoining(new[] { Grug, joining }, joining.PlayerUid));
    }

    [Fact]
    public void A_joining_player_missing_from_the_list_is_still_counted()
    {
        Assert.Equal(2, OnlineCount.WithJoining(new[] { Grug }, "uid-ugg"));
    }

    [Fact]
    public void Players_still_connecting_are_not_counted_in_a_join()
    {
        Assert.Equal(2, OnlineCount.WithJoining(new[] { Grug, Ugg, StillConnecting }, Ugg.PlayerUid));
    }

    [Fact]
    public void A_leaving_player_the_game_still_lists_is_counted_out()
    {
        // Gotcha #17, test plan 3.2: the game still lists Ugg while saying Ugg disconnected.
        Assert.Equal(1, OnlineCount.WithoutLeaving(new[] { Grug, Ugg }, Ugg.PlayerUid));
    }

    [Fact]
    public void A_leaving_player_already_off_the_list_is_not_subtracted_twice()
    {
        Assert.Equal(1, OnlineCount.WithoutLeaving(new[] { Grug }, Ugg.PlayerUid));
    }

    [Fact]
    public void The_last_player_leaving_leaves_zero()
    {
        Assert.Equal(0, OnlineCount.WithoutLeaving(new[] { Grug }, Grug.PlayerUid));
    }

    [Fact]
    public void Player_ids_are_compared_exactly()
    {
        Assert.Equal(1, OnlineCount.WithoutLeaving(new[] { Grug }, "UID-GRUG"));
    }

    [Fact]
    public void A_count_shows_as_online_slash_max()
    {
        Assert.Equal("2/10", new PlayerCount(2, 10).ToString());
    }
}
