namespace GrugBot.Core.Formatting;

/// <summary>
/// The system message for one player death (#8): the game's own sentence when there is one, else our fallback
/// (05-message-format § Deaths, gotcha #19). Every death gets its own line, even two identical ones in a row
/// (test plan 3.5).
/// </summary>
/// <param name="Message">The finished system message, ready for the system channel.</param>
/// <param name="IsFallback">True when the game's sentence couldn't be had and our own wording was used.</param>
public readonly record struct DeathLine(string Message, bool IsFallback)
{
    /// <summary>
    /// Picks the line for a death: <see cref="SystemMessages.PlayerDied"/> for the game's sentence, or
    /// <see cref="SystemMessages.PlayerDiedFallback"/> when there is none or nothing is left of it as plain text.
    /// </summary>
    /// <param name="vanillaSentence">The game's death message, or <c>null</c> when it couldn't be had.</param>
    /// <param name="shownName">The name other players see for the player who died (decision #36), for the fallback.</param>
    /// <param name="killerName">The name of the entity that caused the death, if known, for the fallback.</param>
    public static DeathLine For(string? vanillaSentence, string shownName, string? killerName)
    {
        if (vanillaSentence is not null && Vtml.ToPlainText(vanillaSentence).Trim().Length > 0)
        {
            return new DeathLine(SystemMessages.PlayerDied(vanillaSentence), false);
        }

        return new DeathLine(SystemMessages.PlayerDiedFallback(shownName, killerName), true);
    }
}
