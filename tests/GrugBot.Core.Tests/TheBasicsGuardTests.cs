using GrugBot.Core.Relaying;
using Xunit;

namespace GrugBot.Core.Tests;

/// <summary>The thebasics safety check (issue #11, test plan 7.1 and 7.2).</summary>
public class TheBasicsGuardTests
{
    private const int General = 0;

    [Fact]
    public void Without_thebasics_General_is_relayed_with_one_info_line()
    {
        // Test plan 7.1.
        TheBasicsGuardDecision decision = TheBasicsGuard.Decide(TheBasicsLookup.NotInstalled(), General);

        Assert.True(decision.RelayGeneral);
        Assert.False(decision.IsWarning);
        Assert.Contains("isn't installed", decision.LogLine);
    }

    [Fact]
    public void When_thebasics_makes_General_its_Proximity_chat_group_General_is_not_relayed()
    {
        // Test plan 7.2: one clear warning that names the thebasics setting.
        TheBasicsGuardDecision decision = TheBasicsGuard.Decide(TheBasicsLookup.Found(General), General);

        Assert.False(decision.RelayGeneral);
        Assert.True(decision.IsWarning);
        Assert.Contains("UseGeneralChannelAsProximityChat", decision.LogLine);
        Assert.Contains("does not relay General", decision.LogLine);
        Assert.Contains("in thebasics' config under ModConfig", decision.LogLine);
        // Decision #48: Discord → Game stays on; the warning says how to turn it off too.
        Assert.Contains("Relay.DiscordToGame", decision.LogLine);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(-3)]
    public void When_thebasics_has_its_own_Proximity_chat_group_General_is_relayed(int proximityChatGroupId)
    {
        TheBasicsGuardDecision decision = TheBasicsGuard.Decide(TheBasicsLookup.Found(proximityChatGroupId), General);

        Assert.True(decision.RelayGeneral);
        Assert.False(decision.IsWarning);
        Assert.Contains("Proximity chat group is " + proximityChatGroupId, decision.LogLine);
    }

    [Fact]
    public void General_is_compared_with_the_ID_the_game_gives()
    {
        Assert.False(TheBasicsGuard.Decide(TheBasicsLookup.Found(7), generalChatGroupId: 7).RelayGeneral);
        Assert.True(TheBasicsGuard.Decide(TheBasicsLookup.Found(0), generalChatGroupId: 7).RelayGeneral);
    }

    [Fact]
    public void A_failed_lookup_leaves_the_relay_on_and_warns_with_the_cause()
    {
        // Don't silence the relay because thebasics updated (decision #37's spirit).
        TheBasicsGuardDecision decision = TheBasicsGuard.Decide(
            TheBasicsLookup.Failed("the property ProximityChatGroupId was not found"),
            General);

        Assert.True(decision.RelayGeneral);
        Assert.True(decision.IsWarning);
        Assert.Contains("could not check", decision.LogLine);
        Assert.Contains("UseGeneralChannelAsProximityChat", decision.LogLine);
        Assert.EndsWith("Cause: the property ProximityChatGroupId was not found", decision.LogLine);
    }

    [Fact]
    public void The_fixed_text_has_no_curly_braces()
    {
        // The mod passes the line as a format argument anyway; this keeps our own wording safe as a format string too.
        TheBasicsLookup[] lookups =
        [
            TheBasicsLookup.NotInstalled(),
            TheBasicsLookup.Found(General),
            TheBasicsLookup.Found(5),
            TheBasicsLookup.Failed("x"),
        ];

        foreach (TheBasicsLookup lookup in lookups)
        {
            string line = TheBasicsGuard.Decide(lookup, General).LogLine;
            Assert.DoesNotContain("{", line);
            Assert.DoesNotContain("}", line);
        }
    }

    [Fact]
    public void The_lookup_names_match_thebasics()
    {
        // Verified against thebasics v5.9.1 on 2026-11-16. A change here means the runtime lookup searches for something else.
        Assert.Equal("thebasics", TheBasicsGuard.ModId);
        Assert.Equal("thebasics.ModSystems.ProximityChat.RPProximityChatSystem", TheBasicsGuard.ProximityChatSystemTypeName);
        Assert.Equal("ProximityChatGroupId", TheBasicsGuard.ProximityChatGroupIdPropertyName);
    }
}
