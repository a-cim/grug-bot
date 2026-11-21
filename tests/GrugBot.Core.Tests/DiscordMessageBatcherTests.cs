using System;
using System.Collections.Generic;
using System.Linq;
using GrugBot.Core.Sending;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordMessageBatcherTests
{
    [Fact]
    public void Lines_are_joined_into_one_message()
    {
        // Test plan 2.7.
        List<string> messages = DiscordMessageBatcher.Batch(new[] { "**Grug:** hi", "**Ugg:** hello", "**Grug:** bye" });

        Assert.Equal(new[] { "**Grug:** hi\n**Ugg:** hello\n**Grug:** bye" }, messages);
    }

    [Fact]
    public void A_new_message_starts_when_the_next_line_would_not_fit()
    {
        Assert.Equal(new[] { "12345", "67890" }, DiscordMessageBatcher.Batch(new[] { "12345", "67890" }, maxLength: 10));
        Assert.Equal(new[] { "12345\n67890" }, DiscordMessageBatcher.Batch(new[] { "12345", "67890" }, maxLength: 11));
    }

    [Fact]
    public void Messages_stay_within_the_Discord_limit()
    {
        string line = new('x', 999);

        List<string> messages = DiscordMessageBatcher.Batch(new[] { line, line, line });

        Assert.Equal(2, messages.Count);
        Assert.Equal(line + "\n" + line, messages[0]);
        Assert.Equal(line, messages[1]);
        Assert.All(messages, m => Assert.True(m.Length <= DiscordMessageBatcher.MaxMessageLength));
    }

    [Fact]
    public void Blank_lines_are_dropped()
    {
        Assert.Equal(new[] { "a" }, DiscordMessageBatcher.Batch(new[] { "", "  ", "a" }));
        Assert.Empty(DiscordMessageBatcher.Batch(Array.Empty<string>()));
    }

    [Fact]
    public void A_long_line_is_split_at_word_boundaries_without_losing_anything()
    {
        // Test plan 2.8.
        string line = LongLineOfWords(600);
        Assert.True(line.Length > DiscordMessageBatcher.MaxMessageLength);

        List<string> pieces = DiscordMessageBatcher.SplitLine(line);

        Assert.True(pieces.Count >= 2);
        Assert.All(pieces, p => Assert.True(p.Length <= DiscordMessageBatcher.MaxMessageLength));
        Assert.Equal(line, string.Join(" ", pieces));
        Assert.All(pieces, p => Assert.False(p.StartsWith(' ') || p.EndsWith(' ')));
    }

    [Fact]
    public void A_cut_can_fall_exactly_on_the_limit()
    {
        Assert.Equal(new[] { "abcde", "fgh" }, DiscordMessageBatcher.SplitLine("abcde fgh", maxLength: 5));
    }

    [Fact]
    public void A_line_without_spaces_is_cut_at_the_limit()
    {
        List<string> pieces = DiscordMessageBatcher.SplitLine(new string('x', 4500));

        Assert.Equal(new[] { 2000, 2000, 500 }, pieces.Select(p => p.Length));
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        // The emoji is two UTF-16 characters, at index 4 and 5.
        Assert.Equal(new[] { "abcd", "😀efg", "h" }, DiscordMessageBatcher.SplitLine("abcd😀efgh", maxLength: 5));
    }

    [Fact]
    public void A_split_line_is_batched_with_the_lines_around_it()
    {
        string longLine = LongLineOfWords(600);

        List<string> messages = DiscordMessageBatcher.Batch(new[] { "**Ugg:** before", longLine, "**Ugg:** after" });

        Assert.All(messages, m => Assert.True(m.Length <= DiscordMessageBatcher.MaxMessageLength));
        Assert.StartsWith("**Ugg:** before", messages[0], StringComparison.Ordinal);
        Assert.EndsWith("**Ugg:** after", messages[^1], StringComparison.Ordinal);
        Assert.Equal(
            ("**Ugg:** before " + longLine + " **Ugg:** after").Split(' '),
            string.Join(" ", messages).Replace('\n', ' ').Split(' '));
    }

    [Fact]
    public void The_limit_must_leave_room_for_text()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiscordMessageBatcher.SplitLine("abc", maxLength: 1));
    }

    private static string LongLineOfWords(int count) =>
        "**Grug:** " + string.Join(" ", Enumerable.Range(0, count).Select(i => "word" + i));
}
