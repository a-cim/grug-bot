using System;
using System.Collections.Generic;

namespace GrugBot.Core.Players;

/// <summary>
/// The <c>{online}</c> in join and leave lines and in the bot presence: the admitted players in the game's list
/// of online players (05-message-format, gotcha #17).
/// </summary>
/// <remarks>
/// The player joining or leaving is counted by who they are, not by where the game's list stands: the joining
/// player counts once, the leaving player doesn't count. The game still lists a leaving player while it says they
/// disconnected, so this comes to the "minus one" of gotcha #17, and it stays right if a later game version takes
/// them off the list first, or hasn't marked a joining player as admitted yet.
/// </remarks>
public static class OnlineCount
{
    /// <summary>The admitted players, for the bot presence at startup.</summary>
    public static int Now(IEnumerable<OnlinePlayer> players) => CountAdmittedExcept(players, null);

    /// <summary>For a join line: the admitted players other than <paramref name="joiningUid"/>, plus that player.</summary>
    public static int WithJoining(IEnumerable<OnlinePlayer> players, string joiningUid) =>
        CountAdmittedExcept(players, joiningUid) + 1;

    /// <summary>
    /// For a leave line: the admitted players other than <paramref name="leavingUid"/>, who the game still lists
    /// while they disconnect (gotcha #17, test plan 3.2).
    /// </summary>
    public static int WithoutLeaving(IEnumerable<OnlinePlayer> players, string leavingUid) =>
        CountAdmittedExcept(players, leavingUid);

    private static int CountAdmittedExcept(IEnumerable<OnlinePlayer> players, string? exceptUid)
    {
        int count = 0;
        foreach (OnlinePlayer player in players)
        {
            if (player.IsAdmitted && !string.Equals(player.PlayerUid, exceptUid, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
