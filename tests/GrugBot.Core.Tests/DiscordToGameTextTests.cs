using System;
using System.Collections.Generic;
using System.Linq;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class DiscordToGameTextTests
{
    private const ulong GrugId = 111111111111111111;
    private const ulong UggId = 222222222222222222;
    private const ulong RoleId = 333333333333333333;
    private const ulong ChannelId = 444444444444444444;

    private static readonly MentionLookups Names = Lookups(
        new Dictionary<ulong, string> { [GrugId] = "Grug", [UggId] = "Ugg" },
        new Dictionary<ulong, string> { [RoleId] = "Builders" },
        new Dictionary<ulong, string> { [ChannelId] = "general" });

    private static MentionLookups Lookups(
        Dictionary<ulong, string> users,
        Dictionary<ulong, string>? roles = null,
        Dictionary<ulong, string>? channels = null) => new(
            id => users.TryGetValue(id, out string? name) ? name : null,
            id => roles is not null && roles.TryGetValue(id, out string? name) ? name : null,
            id => channels is not null && channels.TryGetValue(id, out string? name) ? name : null);

    private static string Build(string content, int attachments = 0, int stickers = 0) =>
        DiscordToGameText.Build(content, Names, attachments, stickers);

    // Mentions (test plan 5.3, 5.4)

    [Theory]
    [InlineData("hi <@111111111111111111>", "hi @Grug")]
    [InlineData("hi <@!111111111111111111>", "hi @Grug")]
    [InlineData("<@111111111111111111> and <@222222222222222222>", "@Grug and @Ugg")]
    public void A_user_mention_becomes_at_their_name(string content, string expected)
    {
        Assert.Equal(expected, Build(content));
    }

    [Fact]
    public void A_role_mention_becomes_at_the_role()
    {
        Assert.Equal("ping @Builders", Build("ping <@&333333333333333333>"));
    }

    [Fact]
    public void A_channel_mention_becomes_hash_the_channel()
    {
        Assert.Equal("see #general", Build("see <#444444444444444444>"));
    }

    [Fact]
    public void A_user_whose_name_is_not_known_is_unknown_user()
    {
        // Decision #41: not in the mention data, and the lookup found nothing.
        Assert.Equal("hi @unknown-user", Build("hi <@999>"));
    }

    [Fact]
    public void An_unknown_role_or_channel_says_so()
    {
        Assert.Equal("@unknown-role in #unknown-channel", Build("<@&999> in <#998>"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_name_counts_as_unknown(string blank)
    {
        MentionLookups lookups = new(_ => blank, _ => blank, _ => blank);

        Assert.Equal(
            "@unknown-user @unknown-role #unknown-channel",
            DiscordToGameText.Build("<@1> <@&2> <#3>", lookups, 0, 0));
    }

    [Fact]
    public void With_no_lookups_every_mention_is_unknown()
    {
        Assert.Equal("@unknown-user", DiscordToGameText.Build("<@1>", MentionLookups.None, 0, 0));
    }

    [Fact]
    public void A_name_that_looks_like_a_mention_is_not_resolved_again()
    {
        MentionLookups lookups = Lookups(new Dictionary<ulong, string> { [1] = "<@2>", [2] = "Ugg" });

        Assert.Equal("@<@2>", DiscordToGameText.Build("<@1>", lookups, 0, 0));
    }

    [Fact]
    public void Resolved_names_are_escaped_with_the_rest_of_the_line()
    {
        MentionLookups lookups = Lookups(new Dictionary<ulong, string> { [1] = "Ugg <3 & </font>" });
        string text = DiscordToGameText.Build("hi <@1>", lookups, 0, 0);

        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Grug: </strong></font>hi @Ugg &lt;3 & &lt;/font&gt;",
            DiscordToGameFormatter.Format("Grug", text, "#7289DA"));
    }

    [Theory]
    [InlineData("<@99999999999999999999999>")] // too big for a snowflake
    [InlineData("<@>")]
    [InlineData("<@abc>")]
    [InlineData("<#general>")]
    [InlineData("@Grug")]
    [InlineData("<3 and 1 < 2 > 0")]
    [InlineData("</ban:123456789>")] // slash command mention
    [InlineData("<id:customize>")]
    public void Text_that_is_not_a_mention_is_left_as_typed(string content)
    {
        Assert.Equal(content, Build(content));
    }

    [Theory]
    [InlineData("<t:1700000000>")]
    [InlineData("<t:1700000000:R>")]
    [InlineData("at <t:1700000000:f> ok")]
    public void Timestamps_are_left_as_typed(string content)
    {
        // Decision #25.
        Assert.Equal(content, Build(content));
    }

    [Fact]
    public void User_mention_ids_are_found_once_each_in_order()
    {
        IReadOnlyList<ulong> ids = DiscordToGameText.FindUserMentionIds(
            "<@222222222222222222> <@!111111111111111111> <@222222222222222222>");

        Assert.Equal(new[] { UggId, GrugId }, ids);
    }

    [Fact]
    public void Roles_channels_and_emoji_are_not_user_mention_ids()
    {
        Assert.Empty(DiscordToGameText.FindUserMentionIds("<@&1> <#2> <:grug:3> <t:4> <@99999999999999999999999>"));
    }

    // Custom emoji

    [Theory]
    [InlineData("<:grug:123456789012345678>", ":grug:")]
    [InlineData("<a:grug_dance:123456789012345678>", ":grug_dance:")]
    [InlineData("nice <:thumbs_up2:1> <a:spin:2>!", "nice :thumbs_up2: :spin:!")]
    public void A_custom_emoji_becomes_colon_name_colon(string content, string expected)
    {
        Assert.Equal(expected, Build(content));
    }

    [Fact]
    public void Ordinary_emoji_are_left_as_they_are()
    {
        Assert.Equal("🦴 :) :grug:", Build("🦴 :) :grug:"));
    }

    // Attachments and stickers (test plan 5.7)

    [Fact]
    public void One_attachment_alone_is_attachment()
    {
        Assert.Equal("[Attachment]", Build("", attachments: 1));
    }

    [Fact]
    public void Several_attachments_alone_are_counted()
    {
        Assert.Equal("[3 Attachments]", Build("", attachments: 3));
    }

    [Theory]
    [InlineData(1, "look [Attachment]")]
    [InlineData(2, "look [2 Attachments]")]
    public void Text_with_attachments_has_the_placeholder_after_it(int attachments, string expected)
    {
        Assert.Equal(expected, Build("look", attachments: attachments));
    }

    [Fact]
    public void A_sticker_alone_is_sticker()
    {
        Assert.Equal("[Sticker]", Build("", stickers: 1));
    }

    [Fact]
    public void Text_with_a_sticker_has_the_placeholder_after_it()
    {
        Assert.Equal("hi [Sticker]", Build("hi", stickers: 1));
    }

    [Fact]
    public void Attachments_and_stickers_both_show()
    {
        Assert.Equal("hi [2 Attachments] [2 Stickers]", Build("hi", attachments: 2, stickers: 2));
    }

    [Fact]
    public void Blank_text_with_an_attachment_is_just_the_placeholder()
    {
        Assert.Equal("[Attachment]", Build(" \n ", attachments: 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\n\r\n")]
    public void A_message_with_nothing_to_show_is_empty_and_ignored(string content)
    {
        string text = Build(content);

        Assert.Equal("", text);
        Assert.Null(DiscordToGameFormatter.Format("Grug", text, "#7289DA"));
    }

    [Fact]
    public void Missing_content_is_treated_as_empty()
    {
        Assert.Equal("[Sticker]", DiscordToGameText.Build(null, Names, 0, 1));
    }

    // Newlines (test plan 5.5)

    [Fact]
    public void Every_kind_of_line_break_becomes_one_space()
    {
        Assert.Equal("a b c d e f", Build("a\nb\r\nc\rd e f"));
    }

    [Fact]
    public void Blank_lines_keep_one_space_per_line_break()
    {
        Assert.Equal("a  b", Build("a\n\nb"));
    }

    [Fact]
    public void Line_breaks_at_the_ends_are_trimmed()
    {
        Assert.Equal("hi there", Build("\nhi\nthere\n"));
    }

    // The length cap (test plan 5.6)

    [Fact]
    public void Text_of_exactly_the_cap_is_left_whole()
    {
        string content = new('a', DiscordToGameText.MaxLength);

        Assert.Equal(content, Build(content));
    }

    [Fact]
    public void Text_over_the_cap_is_cut_to_the_cap_ending_in_an_ellipsis()
    {
        string text = Build(new string('a', 600));

        Assert.Equal(DiscordToGameText.MaxLength, text.Length);
        Assert.Equal(new string('a', DiscordToGameText.MaxLength - 1) + "…", text);
    }

    [Fact]
    public void One_character_over_the_cap_is_cut()
    {
        string text = Build(new string('a', DiscordToGameText.MaxLength + 1));

        Assert.Equal(DiscordToGameText.MaxLength, text.Length);
        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Spaces_before_the_ellipsis_are_dropped()
    {
        string content = new string('a', DiscordToGameText.MaxLength - 3) + "   more";

        Assert.Equal(new string('a', DiscordToGameText.MaxLength - 3) + "…", Build(content));
    }

    [Fact]
    public void The_cap_counts_the_text_as_shown_not_as_posted()
    {
        // 1050 characters as posted, but only 250 once the mentions are names.
        string mentions = string.Concat(Enumerable.Repeat("<@111111111111111111>", 50));

        Assert.Equal(string.Concat(Enumerable.Repeat("@Grug", 50)), Build(mentions));
    }

    [Fact]
    public void Placeholders_are_kept_and_count_towards_the_cap()
    {
        string text = Build(new string('a', 600), attachments: 2);

        Assert.Equal(DiscordToGameText.MaxLength, text.Length);
        Assert.EndsWith("a… [2 Attachments]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cap_never_splits_an_emoji()
    {
        // 498 letters, then emoji (two UTF-16 halves each): the cut would fall between the halves of the first one.
        string content = new string('a', DiscordToGameText.MaxLength - 2) + "🦴🦴🦴";
        string text = Build(content);

        Assert.Equal(new string('a', DiscordToGameText.MaxLength - 2) + "…", text);
        Assert.False(char.IsHighSurrogate(text[text.Length - 2]));
    }

    [Fact]
    public void The_cap_applies_before_escaping_so_no_escape_is_cut_in_half()
    {
        string text = Build(new string('<', 600));
        string? line = DiscordToGameFormatter.Format("Grug", text, "#7289DA");

        Assert.Equal(new string('<', DiscordToGameText.MaxLength - 1) + "…", text);
        Assert.Equal(
            "<font color=\"#7289DA\"><strong>Grug: </strong></font>"
                + string.Concat(Enumerable.Repeat("&lt;", DiscordToGameText.MaxLength - 1)) + "…",
            line);
    }

    [Theory]
    [InlineData("hello", 10, "hello")]
    [InlineData("hello world", 5, "hell…")]
    [InlineData("hello world", 1, "…")]
    public void Cap_cuts_to_the_given_length(string text, int maxLength, string expected)
    {
        Assert.Equal(expected, DiscordToGameText.Cap(text, maxLength));
    }

    [Fact]
    public void Cap_needs_room_for_the_ellipsis()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiscordToGameText.Cap("hello", 0));
    }

    // Everything together

    [Fact]
    public void A_busy_message_follows_every_rule()
    {
        string text = Build(
            "<@111111111111111111> <:grug:1> look\nat <#444444444444444444> <t:1700000000:R>",
            attachments: 1);

        Assert.Equal("@Grug :grug: look at #general <t:1700000000:R> [Attachment]", text);
    }
}
