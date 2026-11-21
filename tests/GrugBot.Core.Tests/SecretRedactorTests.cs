using GrugBot.Core.Logging;
using Xunit;

namespace GrugBot.Core.Tests;

public class SecretRedactorTests
{
    private const string Token = "secret-token-value";

    [Fact]
    public void Every_copy_of_the_token_is_replaced()
    {
        string redacted = SecretRedactor.Redact("a " + Token + " b " + Token, Token);

        Assert.Equal("a [redacted] b [redacted]", redacted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void Text_is_unchanged_without_a_usable_secret(string? secret)
    {
        Assert.Equal("a short text", SecretRedactor.Redact("a short text", secret));
    }
}
