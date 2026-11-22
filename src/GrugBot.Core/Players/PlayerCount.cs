using System.Globalization;

namespace GrugBot.Core.Players;

/// <summary>
/// How many players are online out of how many the server lets in: the <c>{online}/{max}</c> in join and leave
/// lines and in the bot presence (05-message-format). <see cref="Max"/> is the server's <c>MaxClients</c>.
/// </summary>
public readonly record struct PlayerCount(int Online, int Max)
{
    /// <summary>The count as Discord shows it, such as <c>2/10</c>.</summary>
    public override string ToString() =>
        Online.ToString(CultureInfo.InvariantCulture) + "/" + Max.ToString(CultureInfo.InvariantCulture);
}
