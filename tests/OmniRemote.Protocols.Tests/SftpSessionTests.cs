using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using OmniRemote.Protocols.Sftp;

namespace OmniRemote.Protocols.Tests;

/// <summary>
/// Unit tests for SftpSession that do not require a live SFTP server.
/// Tests that require an actual connection are marked as integration tests
/// and documented in docs/TESTING.md.
/// </summary>
public sealed class SftpSessionTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"omniremote_sftp_test_{Guid.NewGuid():N}");

    public SftpSessionTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

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

    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Initial_state_is_Disconnected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public void Protocol_is_Sftp()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        Assert.Equal(ProtocolType.Sftp, session.Protocol);
    }

    [Fact]
    public void DisplayName_contains_username_and_host()
    {
        var profile = MakeProfile();
        var session = MakeSession(new AlwaysNullCredentialStore(), profile);
        Assert.Contains(profile.Username, session.DisplayName);
        Assert.Contains(profile.Host, session.DisplayName);
    }

    // ── Missing credentials ───────────────────────────────────────────────

    [Fact]
    public async Task Missing_credential_transitions_to_Failed_state()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
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
        var session = MakeSession(new AlwaysNullCredentialStore());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());
        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Operations before connect ─────────────────────────────────────────

    [Fact]
    public async Task ListDirectory_throws_when_not_connected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.ListDirectoryAsync("/home"));
    }

    [Fact]
    public async Task Upload_throws_when_not_connected()
    {
        var localFile = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(localFile, "content");

        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.UploadAsync(localFile, "/remote/test.txt"));
    }

    [Fact]
    public async Task Download_throws_when_not_connected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.DownloadAsync("/remote/test.txt", Path.Combine(_tempDir, "out.txt")));
    }

    [Fact]
    public async Task Rename_throws_when_not_connected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RenameAsync("/remote/a.txt", "/remote/b.txt"));
    }

    [Fact]
    public async Task CreateDirectory_throws_when_not_connected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.CreateDirectoryAsync("/remote/newdir"));
    }

    [Fact]
    public async Task Delete_throws_when_not_connected()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.DeleteAsync("/remote/file.txt", isDirectory: false));
    }

    // ── Path validation ───────────────────────────────────────────────────

    [Fact]
    public async Task ListDirectory_rejects_relative_path()
    {
        var session = MakeSession(new FixedPasswordCredentialStore("pw"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.ListDirectoryAsync("relative/path"));
    }

    [Fact]
    public async Task Upload_rejects_relative_remote_path()
    {
        var localFile = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(localFile, "content");

        var session = MakeSession(new FixedPasswordCredentialStore("pw"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.UploadAsync(localFile, "relative/path"));
    }

    [Fact]
    public async Task Download_rejects_path_traversal()
    {
        var session = MakeSession(new FixedPasswordCredentialStore("pw"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.DownloadAsync("/home/../etc/passwd", Path.Combine(_tempDir, "out.txt")));
    }

    [Fact]
    public async Task ListDirectory_rejects_empty_path()
    {
        var session = MakeSession(new FixedPasswordCredentialStore("pw"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.ListDirectoryAsync(""));
    }

    [Fact]
    public async Task ListDirectory_rejects_null_byte_in_path()
    {
        var session = MakeSession(new FixedPasswordCredentialStore("pw"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.ListDirectoryAsync("/home/user\0evil"));
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_when_never_connected_does_not_throw()
    {
        await using var session = MakeSession(new AlwaysNullCredentialStore());
    }

    // ── State events ──────────────────────────────────────────────────────

    [Fact]
    public async Task StateChanged_fires_Connecting_then_Failed_when_no_credential()
    {
        var session = MakeSession(new AlwaysNullCredentialStore());
        var events = new List<ConnectionState>();
        session.StateChanged += (_, s) => events.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Equal(2, events.Count);
        Assert.Equal(ConnectionState.Connecting, events[0]);
        Assert.Equal(ConnectionState.Failed, events[1]);
    }

    // ── Connection refused ────────────────────────────────────────────────

    [Fact]
    public async Task Connection_refused_transitions_to_Failed()
    {
        var profile = new SftpConnectionProfile
        {
            Name = "Test",
            Host = "127.0.0.1",
            Port = 19733, // high port, no server expected
            Username = "testuser",
            ConnectTimeout = TimeSpan.FromSeconds(3),
        };

        var session = MakeSession(new FixedPasswordCredentialStore("anypassword"), profile);
        var stateChanges = new List<ConnectionState>();
        session.StateChanged += (_, s) => stateChanges.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Failed, stateChanges);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static SftpConnectionProfile MakeProfile() => new()
    {
        Name = "Test SFTP",
        Host = "127.0.0.1",
        Username = "testuser",
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    private static SftpSession MakeSession(ICredentialStore credentials, SftpConnectionProfile? profile = null) =>
        new(
            profile ?? MakeProfile(),
            new AlwaysTrustHostIdentityPolicy(),
            credentials,
            new PassthroughRedactor(),
            NullLogger<SftpSession>.Instance);

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class AlwaysNullCredentialStore : ICredentialStore
    {
        public Task SaveAsync(string key, string secret, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> LoadAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedPasswordCredentialStore : ICredentialStore
    {
        private readonly string _password;
        public FixedPasswordCredentialStore(string password) => _password = password;
        public Task SaveAsync(string key, string secret, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> LoadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(_password);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AlwaysTrustHostIdentityPolicy : IHostIdentityPolicy
    {
        public Task<HostKeyTrustResult> CheckHostKeyAsync(
            string host, int port, string keyType, byte[] keyData, CancellationToken ct = default) =>
            Task.FromResult(HostKeyTrustResult.Trusted);

        public Task TrustHostKeyAsync(
            string host, int port, string keyType, byte[] keyData, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class PassthroughRedactor : ISessionLogRedactor
    {
        public string Redact(string message) => message;
    }
}
