using System;

namespace GrugBot.Core.TemporalStorms;

/// <summary>A temporal storm phase to post to Discord, with the storm's strength where the sentence names it.</summary>
/// <param name="Phase">Which phase was reached.</param>
/// <param name="Strength">
/// The storm's strength when the phase was reached. Only approaching and imminent name it; the waning
/// sentence doesn't.
/// </param>
public readonly record struct TemporalStormAnnouncement(TemporalStormPhase Phase, TemporalStormStrength Strength)
{
    /// <summary>
    /// The game's lang key for this phase. The keys are the vanilla English sentences themselves, so the key
    /// doubles as the English text when the game has no translation for it (05-message-format § Storm wording).
    /// </summary>
    public string LangKey => Phase switch
    {
        TemporalStormPhase.Approaching => "A " + StrengthWord + " temporal storm is approaching",
        TemporalStormPhase.Imminent => "A " + StrengthWord + " temporal storm is imminent",
        TemporalStormPhase.Waning => "The temporal storm seems to be waning",
        _ => throw new InvalidOperationException("Unknown temporal storm phase " + Phase + "."),
    };

    private string StrengthWord => Strength switch
    {
        TemporalStormStrength.Light => "light",
        TemporalStormStrength.Medium => "medium",
        TemporalStormStrength.Heavy => "heavy",
        _ => throw new InvalidOperationException("Unknown temporal storm strength " + Strength + "."),
    };
}
