using System;
using System.Collections.Generic;
using System.Text;

namespace GrugBot.Core.Sending;

/// <summary>
/// Joins queued lines into as few Discord messages as fit, and splits a line that is too long for one
/// message (05-message-format § Batching, decision #26).
/// </summary>
public static class DiscordMessageBatcher
{
    /// <summary>Discord's limit on a message's content, counted the way Discord.Net checks it (UTF-16 length).</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    /// Joins <paramref name="lines"/> with newlines, in order, starting a new message whenever the next
    /// line wouldn't fit. A line longer than <paramref name="maxLength"/> is split at a word boundary first
    /// (see <see cref="SplitLine"/>). Blank lines are dropped, since Discord rejects empty messages.
    /// </summary>
    public static List<string> Batch(IEnumerable<string> lines, int maxLength = MaxMessageLength)
    {
        CheckMaxLength(maxLength);

        var messages = new List<string>();
        var current = new StringBuilder();

        foreach (string line in lines)
        {
            foreach (string piece in SplitLine(line, maxLength))
            {
                if (current.Length > 0 && current.Length + 1 + piece.Length > maxLength)
                {
                    messages.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append('\n');
                }

                current.Append(piece);
            }
        }

        if (current.Length > 0)
        {
            messages.Add(current.ToString());
        }

        return messages;
    }

    /// <summary>
    /// Splits one line into pieces of at most <paramref name="maxLength"/> characters. Each cut is made at
    /// the last whitespace that fits, and that whitespace character is dropped. A run with no whitespace at
    /// all (a very long URL, say) is cut at the limit, never between the two halves of a surrogate pair.
    /// No text other than the whitespace at a cut is lost. Blank pieces are skipped.
    /// </summary>
    public static List<string> SplitLine(string line, int maxLength = MaxMessageLength)
    {
        CheckMaxLength(maxLength);

        var pieces = new List<string>();
        string rest = line;

        while (rest.Length > maxLength)
        {
            int cut = LastWhitespaceAtOrBefore(rest, maxLength);
            string piece;
            if (cut > 0)
            {
                piece = rest.Substring(0, cut);
                rest = rest.Substring(cut + 1);
            }
            else
            {
                int length = char.IsHighSurrogate(rest[maxLength - 1]) ? maxLength - 1 : maxLength;
                piece = rest.Substring(0, length);
                rest = rest.Substring(length);
            }

            AddIfNotBlank(pieces, piece);
        }

        AddIfNotBlank(pieces, rest);
        return pieces;
    }

    // Looks at index maxLength too: whitespace there means the first maxLength characters fit exactly.
    private static int LastWhitespaceAtOrBefore(string text, int maxLength)
    {
        for (int i = maxLength; i > 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AddIfNotBlank(List<string> pieces, string piece)
    {
        if (!string.IsNullOrWhiteSpace(piece))
        {
            pieces.Add(piece);
        }
    }

    private static void CheckMaxLength(int maxLength)
    {
        if (maxLength < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "A message must be allowed at least 2 characters.");
        }
    }
}
