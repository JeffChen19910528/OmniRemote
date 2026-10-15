using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using OmniRemote.Protocols.Ssh;

namespace OmniRemote.Protocols.Tests;

/// <summary>
/// Unit tests for SshSession that do not require a live SSH server.
/// Tests that require an actual connection are marked as integration tests
/// and documented in docs/TESTING.md.
/// </summary>
public sealed class SshSessionTests
{
    // ── Security defaults ─────────────────────────────────────────────────

    [Fact]
    public void StrictHostKeyChecking_is_true_by_default()
    {
        var profile = MakeProfile();
        Assert.True(profile.StrictHostKeyChecking);
    }

    [Fact]
    public void BypassCertificateValidation_is_false_by_default()
    {
        var profile = MakeProfile();
        Assert.False(profile.BypassCertificateValidation);
    }

    [Fact]
    public void HostKeyVerificationCallback_is_null_by_default()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Assert.Null(session.HostKeyVerificationCallback);
    }

    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Initial_state_is_Disconnected()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public void Protocol_is_Ssh()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Assert.Equal(ProtocolType.Ssh, session.Protocol);
    }

    [Fact]
    public void DisplayName_contains_username_and_host()
    {
        var profile = MakeProfile();
        var session = MakeSession(profile, new AlwaysNullCredentialStore());
        Assert.Contains(profile.Username, session.DisplayName);
        Assert.Contains(profile.Host, session.DisplayName);
    }

    // ── Missing credentials ───────────────────────────────────────────────

    [Fact]
    public async Task Missing_credential_transitions_to_Failed_state()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());

        var stateChanges = new List<ConnectionState>();
        session.StateChanged += (_, s) => stateChanges.Add(s);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Failed, stateChanges);
        Assert.Equal(ConnectionState.Failed, session.State);
    }

    [Fact]
    public async Task Missing_credential_throws_descriptive_message()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());
        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Private key auth: missing path ────────────────────────────────────

    [Fact]
    public async Task PrivateKey_auth_without_path_transitions_to_Failed()
    {
        var profile = MakeProfile();
        profile.AuthMethod = SshAuthMethod.PrivateKey;
        profile.PrivateKeyPath = null;

        var session = MakeSession(profile, new AlwaysNullCredentialStore());
        var stateChanges = new List<ConnectionState>();
        session.StateChanged += (_, s) => stateChanges.Add(s);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Failed, stateChanges);
    }

    // ── State events ──────────────────────────────────────────────────────

    [Fact]
    public async Task StateChanged_fires_once_for_Connecting_then_Failed_when_no_credential()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        var events = new List<ConnectionState>();
        session.StateChanged += (_, s) => events.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Equal(2, events.Count);
        Assert.Equal(ConnectionState.Connecting, events[0]);
        Assert.Equal(ConnectionState.Failed, events[1]);
    }

    // ── Terminal access before connect ────────────────────────────────────

    [Fact]
    public void TerminalInput_throws_when_not_connected()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Assert.Throws<InvalidOperationException>(() => _ = session.TerminalInput);
    }

    [Fact]
    public void TerminalOutput_throws_when_not_connected()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Assert.Throws<InvalidOperationException>(() => _ = session.TerminalOutput);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_when_never_connected_does_not_throw()
    {
        await using var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        // No exception expected
    }

    // ── Connection refused (integration — needs no server on port) ────────

    [Fact]
    public async Task Connection_refused_transitions_to_Failed()
    {
        var profile = new SshConnectionProfile
        {
            Name = "Test",
            Host = "127.0.0.1",
            Port = 19722, // high port, no server expected
            Username = "testuser",
            ConnectTimeout = TimeSpan.FromSeconds(3),
        };

        var session = MakeSession(profile, new FixedPasswordCredentialStore("anypassword"));
        var stateChanges = new List<ConnectionState>();
        session.StateChanged += (_, s) => stateChanges.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Failed, stateChanges);
    }

    // ── HostKeyVerificationCallback wiring ───────────────────────────────

    [Fact]
    public void HostKeyVerificationCallback_can_be_set_and_read()
    {
        var session = MakeSession(MakeProfile(), new AlwaysNullCredentialStore());
        Func<HostKeyVerificationRequest, CancellationToken, Task<bool>> cb =
            (_, _) => Task.FromResult(true);

        session.HostKeyVerificationCallback = cb;
        Assert.Same(cb, session.HostKeyVerificationCallback);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static SshConnectionProfile MakeProfile() => new()
    {
        Name = "Test SSH",
        Host = "127.0.0.1",
        Username = "testuser",
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    private static SshSession MakeSession(SshConnectionProfile profile, ICredentialStore credentials) =>
        new(profile,
            new NeverTrustHostIdentityPolicy(),
            credentials,
            new PassthroughRedactor(),
            NullLogger<SshSession>.Instance);

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class AlwaysNullCredentialStore : ICredentialStore
    {
        public Task SaveAsync(string key, string secret, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<string?> LoadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
        public Task DeleteAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedPasswordCredentialStore : ICredentialStore
    {
        private readonly string _password;
        public FixedPasswordCredentialStore(string password) => _password = password;
        public Task SaveAsync(string key, string secret, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<string?> LoadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(_password);
        public Task DeleteAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class NeverTrustHostIdentityPolicy : IHostIdentityPolicy
    {
        public Task<HostKeyTrustResult> CheckHostKeyAsync(
            string host, int port, string keyType, byte[] keyData, CancellationToken ct = default) =>
            Task.FromResult(HostKeyTrustResult.Unknown);

        public Task TrustHostKeyAsync(
            string host, int port, string keyType, byte[] keyData, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class PassthroughRedactor : ISessionLogRedactor
    {
        public string Redact(string message) => message;
    }
}
