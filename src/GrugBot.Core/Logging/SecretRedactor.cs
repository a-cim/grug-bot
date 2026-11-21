using System;

namespace GrugBot.Core.Logging;

/// <summary>
/// Last line of defence for decision #27: removes the Discord token from text before it's logged,
/// in case a library message or exception ever includes it.
/// </summary>
public static class SecretRedactor
{
    public const string Replacement = "[redacted]";

    // Shorter "secrets" would blank out ordinary words; real bot tokens are around 70 characters.
    private const int MinimumSecretLength = 8;

    /// <summary>Returns <paramref name="text"/> with every occurrence of <paramref name="secret"/> replaced.</summary>
    public static string Redact(string text, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < MinimumSecretLength)
        {
            return text;
        }

        return text.Replace(secret, Replacement, StringComparison.Ordinal);
    }
}
