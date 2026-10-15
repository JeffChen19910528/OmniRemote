using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using OmniRemote.Protocols.Telnet;

namespace OmniRemote.Protocols.Tests;

/// <summary>
/// Unit tests for TelnetSession that do not require a live Telnet server.
/// Integration tests requiring a real server are documented in docs/TESTING.md.
/// </summary>
public sealed class TelnetSessionTests
{
    // ── Security: unencrypted warning ─────────────────────────────────────

    [Fact]
    public void UnencryptedWarning_constant_is_set_and_non_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(TelnetSession.UnencryptedWarning));
    }

    [Fact]
    public void UnencryptedWarning_mentions_plaintext()
    {
        Assert.Contains("plaintext", TelnetSession.UnencryptedWarning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisplayName_contains_UNENCRYPTED_marker()
    {
        var session = MakeSession();
        Assert.Contains("UNENCRYPTED", session.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisplayName_contains_host_and_port()
    {
        var profile = MakeProfile();
        var session = MakeSession(profile);
        Assert.Contains(profile.Host, session.DisplayName);
        Assert.Contains(profile.Port.ToString(), session.DisplayName);
    }

    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Initial_state_is_Disconnected()
    {
        var session = MakeSession();
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public void Protocol_is_Telnet()
    {
        var session = MakeSession();
        Assert.Equal(ProtocolType.Telnet, session.Protocol);
    }

    // ── Host key callback (not applicable to Telnet) ──────────────────────

    [Fact]
    public void HostKeyVerificationCallback_is_always_null()
    {
        var session = MakeSession();
        Assert.Null(session.HostKeyVerificationCallback);
    }

    [Fact]
    public void Setting_HostKeyVerificationCallback_is_ignored()
    {
        var session = MakeSession();
        session.HostKeyVerificationCallback = (_, _) => Task.FromResult(true);
        Assert.Null(session.HostKeyVerificationCallback);
    }

    // ── Terminal access before connect ────────────────────────────────────

    [Fact]
    public void TerminalInput_throws_when_not_connected()
    {
        var session = MakeSession();
        Assert.Throws<InvalidOperationException>(() => _ = session.TerminalInput);
    }

    [Fact]
    public void TerminalOutput_throws_when_not_connected()
    {
        var session = MakeSession();
        Assert.Throws<InvalidOperationException>(() => _ = session.TerminalOutput);
    }

    // ── SetTerminalSize before connect ────────────────────────────────────

    [Fact]
    public void SetTerminalSize_is_safe_to_call_when_not_connected()
    {
        var session = MakeSession();
        session.SetTerminalSize(120, 30); // should not throw
    }

    // ── Missing/refused connection ────────────────────────────────────────

    [Fact]
    public async Task Connection_refused_transitions_to_Failed()
    {
        var profile = new TelnetConnectionProfile
        {
            Name = "Test Telnet",
            Host = "127.0.0.1",
            Port = 19744, // high port, no server expected
            ConnectTimeout = TimeSpan.FromSeconds(3),
        };

        var session = MakeSession(profile);
        var stateChanges = new List<ConnectionState>();
        session.StateChanged += (_, s) => stateChanges.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Failed, stateChanges);
        Assert.Equal(ConnectionState.Failed, session.State);
    }

    [Fact]
    public async Task StateChanged_fires_Connecting_then_Failed_on_refused_connection()
    {
        var profile = new TelnetConnectionProfile
        {
            Name = "Test Telnet",
            Host = "127.0.0.1",
            Port = 19745,
            ConnectTimeout = TimeSpan.FromSeconds(3),
        };

        var session = MakeSession(profile);
        var events = new List<ConnectionState>();
        session.StateChanged += (_, s) => events.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Equal(2, events.Count);
        Assert.Equal(ConnectionState.Connecting, events[0]);
        Assert.Equal(ConnectionState.Failed, events[1]);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_when_never_connected_does_not_throw()
    {
        await using var session = MakeSession();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static TelnetConnectionProfile MakeProfile() => new()
    {
        Name = "Test Telnet",
        Host = "127.0.0.1",
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    private static TelnetSession MakeSession(TelnetConnectionProfile? profile = null) =>
        new(profile ?? MakeProfile(), NullLogger<TelnetSession>.Instance);
}
