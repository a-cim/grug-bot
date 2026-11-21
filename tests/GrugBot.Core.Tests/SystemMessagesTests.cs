using System;
using GrugBot.Core.Formatting;
using Xunit;

namespace GrugBot.Core.Tests;

public class SystemMessagesTests
{
    [Fact]
    public void Server_started_is_bold_italic_with_a_literal_emoji()
    {
        // 05-message-format, test plan 6.2.
        Assert.Equal("***✅ Server started***", SystemMessages.ServerStarted);
    }

    [Fact]
    public void Server_shutdown_is_bold_italic_with_a_literal_emoji()
    {
        // 05-message-format, test plan 6.1.
        Assert.Equal("***⛔ Server shutdown***", SystemMessages.ServerShutdown);
    }

    [Fact]
    public void Format_wraps_the_text_in_bold_italic_markers()
    {
        // Decision #10.
        Assert.Equal("***📥 Player Ugg connected (2/10)***", SystemMessages.Format("📥 Player Ugg connected (2/10)"));
    }

    [Fact]
    public void Format_trims_the_text_so_the_markers_still_format()
    {
        Assert.Equal("***⛔ Server shutdown***", SystemMessages.Format("  ⛔ Server shutdown \n"));
    }

    [Fact]
    public void Format_breaks_up_mentions()
    {
        string line = SystemMessages.Format("Player @everyone connected");

        Assert.Equal("***Player @​everyone connected***", line);
        Assert.DoesNotContain("@everyone", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_refuses_empty_text(string text)
    {
        Assert.Throws<ArgumentException>(() => SystemMessages.Format(text));
    }
}
