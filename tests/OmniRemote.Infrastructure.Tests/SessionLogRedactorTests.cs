using OmniRemote.Infrastructure.Logging;

namespace OmniRemote.Infrastructure.Tests;

public sealed class SessionLogRedactorTests
{
    private readonly SessionLogRedactor _sut = new();

    [Fact]
    public void Unrelated_message_is_unchanged()
    {
        const string msg = "Connected to 192.168.1.1 on port 22";
        Assert.Equal(msg, _sut.Redact(msg));
    }

    [Theory]
    [InlineData("password=SuperSecret123")]
    [InlineData("password: SuperSecret123")]
    [InlineData("Password=SuperSecret123")]
    [InlineData("PASSWORD=SuperSecret123")]
    public void Password_value_is_redacted(string input)
    {
        var result = _sut.Redact(input);
        Assert.DoesNotContain("SuperSecret123", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Theory]
    [InlineData("token=abc123xyz")]
    [InlineData("token: abc123xyz")]
    [InlineData("Token=abc123xyz")]
    public void Token_value_is_redacted(string input)
    {
        var result = _sut.Redact(input);
        Assert.DoesNotContain("abc123xyz", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redacted_message_preserves_surrounding_context()
    {
        var result = _sut.Redact("Auth failed: password=hunter2 at step 3");
        Assert.Contains("Auth failed", result);
        Assert.Contains("at step 3", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void Empty_string_returns_empty()
    {
        Assert.Equal(string.Empty, _sut.Redact(string.Empty));
    }

    [Fact]
    public void Multiple_secrets_in_one_message_all_redacted()
    {
        var result = _sut.Redact("password=abc token=xyz connection=ok");
        Assert.DoesNotContain("abc", result);
        Assert.DoesNotContain("xyz", result);
        Assert.Contains("connection=ok", result);
    }
}
