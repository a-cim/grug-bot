using GrugBot.Core.Players;

namespace GrugBot.Core.Formatting;

/// <summary>The bot's status on Discord: <c>players: {online}/{max}</c>, as th3essentials showed it (05-message-format § Bot presence).</summary>
public static class BotPresence
{
    /// <summary>The status text for <paramref name="count"/>, such as <c>players: 2/10</c>.</summary>
    public static string Format(PlayerCount count) => "players: " + count;
}
