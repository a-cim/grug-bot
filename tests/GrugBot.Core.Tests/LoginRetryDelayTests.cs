using System;
using GrugBot.Core.Connection;
using Xunit;

namespace GrugBot.Core.Tests;

public class LoginRetryDelayTests
{
    [Fact]
    public void The_first_retry_comes_soon()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), LoginRetryDelay.After(1));
    }

    [Fact]
    public void Delays_grow_and_stop_at_the_maximum()
    {
        TimeSpan previous = TimeSpan.Zero;
        for (int attempt = 1; attempt <= 20; attempt++)
        {
            TimeSpan delay = LoginRetryDelay.After(attempt);
            Assert.True(delay >= previous);
            Assert.True(delay <= LoginRetryDelay.Maximum);
            previous = delay;
        }

        Assert.Equal(LoginRetryDelay.Maximum, LoginRetryDelay.After(1000));
    }

    [Fact]
    public void The_longest_wait_leaves_room_inside_the_backlog_age_limit()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), LoginRetryDelay.Maximum);
        Assert.True(LoginRetryDelay.Maximum < GrugBot.Core.Sending.DiscordSendQueue.MaxAge);
    }

    [Fact]
    public void There_is_no_delay_before_the_first_try()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetryDelay.After(0));
    }
}
