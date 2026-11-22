namespace GrugBot.Core.Players;

/// <summary>
/// One player in the game's list of online players, reduced to what <see cref="OnlineCount"/> needs.
/// </summary>
/// <param name="PlayerUid">The player's account ID, which identifies the player joining or leaving.</param>
/// <param name="IsAdmitted">
/// True when the player has got past the server's <c>MaxClients</c> check, so counts as online. The game also lists
/// players who are still connecting; they don't count (gotcha #17).
/// </param>
public readonly record struct OnlinePlayer(string PlayerUid, bool IsAdmitted);
