using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Infrastructure.Persistence;

namespace OmniRemote.Infrastructure.Tests;

public sealed class SqliteConnectionProfileStoreTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"omniremote_test_{Guid.NewGuid():N}.db");
    private readonly SqliteConnectionProfileStore _sut;

    public SqliteConnectionProfileStoreTests()
    {
        _sut = new SqliteConnectionProfileStore(_dbPath, NullLogger<SqliteConnectionProfileStore>.Instance);
        _sut.InitializeAsync().GetAwaiter().GetResult();
    }

    // ── Save & GetById ────────────────────────────────────────────────────

    [Fact]
    public async Task Save_and_retrieve_ssh_profile()
    {
        var profile = new SshConnectionProfile
        {
            Name = "Test SSH",
            Host = "10.0.0.1",
            Username = "root",
        };

        await _sut.SaveAsync(profile);
        var loaded = await _sut.GetByIdAsync(profile.Id);

        Assert.NotNull(loaded);
        var ssh = Assert.IsType<SshConnectionProfile>(loaded);
        Assert.Equal("Test SSH", ssh.Name);
        Assert.Equal("10.0.0.1", ssh.Host);
        Assert.Equal("root", ssh.Username);
        Assert.Equal(22, ssh.Port);
        Assert.True(ssh.StrictHostKeyChecking);
        Assert.False(ssh.BypassCertificateValidation);
    }

    [Fact]
    public async Task Save_and_retrieve_telnet_profile()
    {
        var profile = new TelnetConnectionProfile { Name = "Router", Host = "10.0.0.254" };
        await _sut.SaveAsync(profile);
        var loaded = await _sut.GetByIdAsync(profile.Id);

        var telnet = Assert.IsType<TelnetConnectionProfile>(loaded);
        Assert.Equal(23, telnet.Port);
    }

    [Fact]
    public async Task Save_and_retrieve_rdp_profile()
    {
        var profile = new RdpConnectionProfile { Name = "WinVM", Host = "10.0.0.50", Username = "Administrator" };
        await _sut.SaveAsync(profile);
        var loaded = await _sut.GetByIdAsync(profile.Id);

        var rdp = Assert.IsType<RdpConnectionProfile>(loaded);
        Assert.Equal(3389, rdp.Port);
        Assert.Equal("Administrator", rdp.Username);
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_returns_all_saved_profiles()
    {
        await _sut.SaveAsync(new SshConnectionProfile { Name = "A", Host = "1.1.1.1", Username = "u" });
        await _sut.SaveAsync(new TelnetConnectionProfile { Name = "B", Host = "2.2.2.2" });
        await _sut.SaveAsync(new RdpConnectionProfile { Name = "C", Host = "3.3.3.3", Username = "u" });

        var all = await _sut.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetAll_on_empty_store_returns_empty_list()
    {
        var all = await _sut.GetAllAsync();
        Assert.Empty(all);
    }

    // ── Update (upsert) ───────────────────────────────────────────────────

    [Fact]
    public async Task Saving_existing_id_updates_profile()
    {
        var profile = new SshConnectionProfile { Name = "Old Name", Host = "1.1.1.1", Username = "u" };
        await _sut.SaveAsync(profile);

        profile.Name = "New Name";
        await _sut.SaveAsync(profile);

        var loaded = await _sut.GetByIdAsync(profile.Id);
        Assert.Equal("New Name", loaded!.Name);

        // Only one record should exist
        var all = await _sut.GetAllAsync();
        Assert.Single(all);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_removes_profile()
    {
        var profile = new SshConnectionProfile { Name = "Temp", Host = "1.1.1.1", Username = "u" };
        await _sut.SaveAsync(profile);

        await _sut.DeleteAsync(profile.Id);

        var loaded = await _sut.GetByIdAsync(profile.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Delete_nonexistent_id_does_not_throw()
    {
        await _sut.DeleteAsync(Guid.NewGuid());
    }

    // ── Security: secrets not in store ───────────────────────────────────

    [Fact]
    public async Task Loaded_profile_type_has_no_password_property()
    {
        // SshConnectionProfile intentionally has no password property.
        // Passwords live in ICredentialStore (OS-backed), never in the SQLite store.
        var profile = new SshConnectionProfile { Name = "Sec", Host = "1.2.3.4", Username = "u" };
        await _sut.SaveAsync(profile);

        var loaded = await _sut.GetByIdAsync(profile.Id);
        Assert.NotNull(loaded);
        var props = loaded.GetType().GetProperties();
        Assert.DoesNotContain(props, p =>
            p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Passphrase", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    // ── Cancellation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.GetAllAsync(cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
