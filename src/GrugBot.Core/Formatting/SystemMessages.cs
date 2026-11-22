using System;
using System.Globalization;
using GrugBot.Core.Players;

namespace GrugBot.Core.Formatting;

/// <summary>
/// Builds system messages: the bold-italic Discord lines grug-bot posts for a server event, like
/// <c>***✅ Server started***</c> (decision #10, 05-message-format § Game → Discord). Players tell them
/// apart from chat by that look, as they did with th3essentials.
/// </summary>
/// <remarks>
/// Each server event gets its own member here, so every system message is built in one place: joins and
/// leaves (#7), deaths (#8), storms (#9) and the "messages skipped" line (#10) go next to the start and stop
/// lines below, each built with <see cref="Format"/>. Emoji are literal characters, not <c>:shortcodes:</c>.
/// </remarks>
public static class SystemMessages
{
    /// <summary>The warning sign emoji, written as escapes so no editor can mangle it.</summary>
    private const string WarningEmoji = "\u26A0\uFE0F";

    /// <summary>Posted once per server run, when the first game tick is about to run (test plan 6.2).</summary>
    public static string ServerStarted { get; } = Format("✅ Server started");

    /// <summary>Posted when the server shuts down cleanly. A crash posts nothing (ADR 0001, test plan 6.1, 6.6).</summary>
    public static string ServerShutdown { get; } = Format("⛔ Server shutdown");

    /// <summary>
    /// A temporal storm phase (#9): <c>***⛈️ {sentence}***</c>, where the sentence is the game's own, translated
    /// from the phase's lang key (05-message-format, test plan 4.1–4.4). It comes from the game's lang files, not a
    /// player, but is Markdown-escaped anyway so a translation can't break the formatting.
    /// </summary>
    public static string TemporalStorm(string sentence) => Format("⛈️ " + DiscordMarkdown.Escape(sentence.Trim()));

    /// <summary>
    /// Lines dropped from a channel's queue while Discord was unreachable (decision #23, test plan 6.4), led by the
    /// warning sign emoji (U+26A0 U+FE0F): <c>***{warning} 12 messages skipped while Discord was unreachable***</c>,
    /// or <c>1 message</c> for one. The send queue posts it once, after reconnecting, ahead of the lines it kept.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    public static string MessagesSkipped(int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Only post this when something was skipped.");
        }

        string messages = count == 1 ? " message" : " messages";
        return Format(WarningEmoji + " " + count.ToString(CultureInfo.InvariantCulture) + messages + " skipped while Discord was unreachable");
    }

    /// <summary>
    /// Wraps <paramref name="text"/> in bold-italic markers: <c>***{text}***</c>. Surrounding whitespace is
    /// trimmed, since Discord doesn't format <c>*** text***</c>. Mentions are neutralised as a second line of
    /// defence (gotcha #8).
    /// </summary>
    /// <param name="text">
    /// The finished text. Anything in it that came from a player, such as a shown name, must already be
    /// escaped with <see cref="DiscordMarkdown.Escape"/>, or a stray <c>*</c> in it would break the formatting.
    /// </param>
    /// <exception cref="ArgumentException">The text is empty or only whitespace: Discord would show stray asterisks.</exception>
    public static string Format(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A system message needs some text.", nameof(text));
        }

        return "***" + DiscordMentions.Neutralise(trimmed) + "***";
    }

    /// <summary>
    /// A player is now in game: <c>***📥 Player Ugg connected (2/10)***</c>, counting the joining player (test plan 3.1).
    /// </summary>
    /// <param name="shownName">The name other players see for the player (decision #36). It is escaped here.</param>
    public static string PlayerConnected(string shownName, PlayerCount count) =>
        Format("📥 Player " + EscapeName(shownName) + " connected (" + count + ")");

    /// <summary>
    /// A player left: <c>***📤 Player Grug disconnected (1/10)***</c>, with the leaving player already counted out
    /// (gotcha #17, test plan 3.2).
    /// </summary>
    /// <param name="shownName">The name other players see for the player (decision #36). It is escaped here.</param>
    public static string PlayerDisconnected(string shownName, PlayerCount count) =>
        Format("📤 Player " + EscapeName(shownName) + " disconnected (" + count + ")");

    /// <summary>
    /// A player died (#8): <c>***{sentence}***</c>, where the sentence is the game's own death message, such as
    /// <c>***Player Ugg got killed by a nightmare drifter***</c> (05-message-format § Deaths, test plan 3.4). Like other
    /// text from the game it is turned into plain text (VTML stripped, entities unescaped) and Markdown-escaped, so it
    /// reads as the in-game line.
    /// </summary>
    /// <param name="sentence">The game's death message.</param>
    /// <param name="deathNumber">
    /// How many times the player has died, this death included (#32). With 1 or more the line reads like
    /// <c>***Player Ugg drowned. This is death number 7.***</c>, adding the full stop only when the sentence doesn't already end in
    /// <c>.</c>, <c>!</c> or <c>?</c>. 0 or less adds nothing.
    /// </param>
    /// <exception cref="ArgumentException">Nothing is left of the sentence as plain text; <see cref="DeathLine.For"/> falls back instead.</exception>
    public static string PlayerDied(string sentence, int deathNumber = 0) =>
        Format(WithDeathNumber(DiscordMarkdown.Escape(Vtml.ToPlainText(sentence).Trim()), deathNumber));

    /// <summary>
    /// Our own death sentence, for when the game's can't be had (gotcha #19): <c>***Player Ugg died***</c>, or
    /// <c>***Player Ugg was killed by Nightmare drifter***</c> when the entity that caused the death is known. The
    /// wording follows the old Discord Bot mod's fallback.
    /// </summary>
    /// <param name="shownName">The name other players see for the player who died (decision #36). It is escaped here.</param>
    /// <param name="killerName">
    /// The name of the entity that caused the death, or <c>null</c> (or blank) when there is none or it's the player
    /// themselves. It is escaped here.
    /// </param>
    /// <param name="deathNumber">How many times the player has died, as for <see cref="PlayerDied"/>; 0 or less for no count.</param>
    public static string PlayerDiedFallback(string shownName, string? killerName, int deathNumber = 0)
    {
        string killer = killerName is null ? "" : Vtml.ToPlainText(killerName).Trim();
        string what = killer.Length == 0 ? " died" : " was killed by " + DiscordMarkdown.Escape(killer);
        return Format(WithDeathNumber("Player " + EscapeName(shownName) + what, deathNumber));
    }

    /// <summary>
    /// Ends a death sentence with the player's death count (#32, decision #52): <c>Player Ugg got pancaked</c> becomes
    /// <c>Player Ugg got pancaked. This is death number 7.</c> A full stop is added first only when the sentence doesn't
    /// already end in <c>.</c>, <c>!</c> or <c>?</c>, ignoring trailing whitespace. A count of 0 or less (thebasics isn't
    /// installed, isn't counting deaths, or the count couldn't be read) and a blank sentence are returned unchanged.
    /// </summary>
    private static string WithDeathNumber(string sentence, int deathNumber)
    {
        string trimmed = sentence.TrimEnd();
        if (deathNumber < 1 || trimmed.Length == 0)
        {
            return sentence;
        }

        char last = trimmed[trimmed.Length - 1];
        string stop = last is '.' or '!' or '?' ? "" : ".";
        return trimmed + stop + " This is death number " + deathNumber.ToString(CultureInfo.InvariantCulture) + ".";
    }

    /// <summary>
    /// A shown name as plain text with its Markdown escaped, as in chat lines, so a name like <c>Grug_Ugg</c>
    /// can't break the bold-italic markers.
    /// </summary>
    private static string EscapeName(string shownName) => DiscordMarkdown.Escape(Vtml.ToPlainText(shownName).Trim());
}
