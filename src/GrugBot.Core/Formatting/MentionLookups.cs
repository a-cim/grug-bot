using System;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Where <see cref="DiscordToGameText"/> gets the names for the users, roles and channels a Discord message
/// mentions. Each lookup takes a Discord ID and returns the name without its <c>@</c> or <c>#</c>, or
/// <c>null</c> if it doesn't know that ID. A lookup must answer at once: the mod looks up any user it doesn't
/// have yet before building the text (decision #41).
/// </summary>
public sealed class MentionLookups
{
    /// <summary>Knows no names, so every mention shows as unknown.</summary>
    public static readonly MentionLookups None = new(_ => null, _ => null, _ => null);

    /// <param name="userName">The Discord name (<c>CONTEXT.md</c>) of a mentioned user.</param>
    /// <param name="roleName">The name of a mentioned role.</param>
    /// <param name="channelName">The name of a mentioned channel.</param>
    public MentionLookups(Func<ulong, string?> userName, Func<ulong, string?> roleName, Func<ulong, string?> channelName)
    {
        UserName = userName ?? throw new ArgumentNullException(nameof(userName));
        RoleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
        ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
    }

    public Func<ulong, string?> UserName { get; }

    public Func<ulong, string?> RoleName { get; }

    public Func<ulong, string?> ChannelName { get; }
}
