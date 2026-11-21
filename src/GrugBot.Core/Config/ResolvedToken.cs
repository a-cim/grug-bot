namespace GrugBot.Core.Config;

/// <summary>
/// The Discord token and where it came from. Deliberately a class, not a record: a record's
/// generated <c>ToString</c> would print the token, and this one must never be logged (decision #27).
/// </summary>
public sealed class ResolvedToken
{
    internal ResolvedToken(string? value, TokenSource source)
    {
        Value = value;
        Source = source;
    }

    /// <summary>The token itself, or <c>null</c> when <see cref="Source"/> is <see cref="TokenSource.None"/>. Never log it.</summary>
    public string? Value { get; }

    public TokenSource Source { get; }

    public bool IsMissing => Source == TokenSource.None;

    /// <summary>A log-safe description of where the token came from. Never contains the token.</summary>
    public string DescribeSource() => Source switch
    {
        TokenSource.EnvironmentVariable => "the " + DiscordTokenResolver.EnvironmentVariableName + " environment variable",
        TokenSource.ConfigFile => "DiscordToken in " + GrugBotConfig.DisplayPath,
        _ => "nowhere (no token set)",
    };

    /// <summary>Returns the source only, so an accidental log of this object can't leak the token.</summary>
    public override string ToString() => "Discord token from " + DescribeSource();
}
