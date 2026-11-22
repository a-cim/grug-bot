using GrugBot.Core.TemporalStorms;
using Xunit;

namespace GrugBot.Core.Tests;

public class TemporalStormAnnouncementTests
{
    [Theory]
    // The vanilla lang keys, which are also the English sentences (05-message-format).
    [InlineData(TemporalStormPhase.Approaching, TemporalStormStrength.Light, "A light temporal storm is approaching")]
    [InlineData(TemporalStormPhase.Approaching, TemporalStormStrength.Medium, "A medium temporal storm is approaching")]
    [InlineData(TemporalStormPhase.Approaching, TemporalStormStrength.Heavy, "A heavy temporal storm is approaching")]
    [InlineData(TemporalStormPhase.Imminent, TemporalStormStrength.Light, "A light temporal storm is imminent")]
    [InlineData(TemporalStormPhase.Imminent, TemporalStormStrength.Medium, "A medium temporal storm is imminent")]
    [InlineData(TemporalStormPhase.Imminent, TemporalStormStrength.Heavy, "A heavy temporal storm is imminent")]
    [InlineData(TemporalStormPhase.Waning, TemporalStormStrength.Light, "The temporal storm seems to be waning")]
    [InlineData(TemporalStormPhase.Waning, TemporalStormStrength.Heavy, "The temporal storm seems to be waning")]
    public void Lang_key_is_the_vanilla_sentence(TemporalStormPhase phase, TemporalStormStrength strength, string expected)
    {
        Assert.Equal(expected, new TemporalStormAnnouncement(phase, strength).LangKey);
    }
}
