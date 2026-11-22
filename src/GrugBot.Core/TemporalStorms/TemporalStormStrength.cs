namespace GrugBot.Core.TemporalStorms;

/// <summary>
/// How strong a temporal storm is. Mirrors the game's <c>EnumTempStormStrength</c>, which Core can't reference
/// (ADR 0001); the mod maps one to the other.
/// </summary>
public enum TemporalStormStrength
{
    Light,
    Medium,
    Heavy,
}
