using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Infrastructure.Security;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Tests;

public sealed class FileBasedHostIdentityPolicyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"omniremote_ssh_test_{Guid.NewGuid():N}");
    private string KnownHostsPath => Path.Combine(_dir, "known_hosts");

    private FileBasedHostIdentityPolicy BuildSut() =>
        new(KnownHostsPath, NullLogger<FileBasedHostIdentityPolicy>.Instance);

    // ── Unknown key ───────────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_key_returns_Unknown()
    {
        var sut = BuildSut();
        var result = await sut.CheckHostKeyAsync("192.168.1.1", 22, "ssh-rsa", RandomKey());
        Assert.Equal(HostKeyTrustResult.Unknown, result);
    }

    // ── Trusted key ───────────────────────────────────────────────────────

    [Fact]
    public async Task Trusted_key_returns_Trusted()
    {
        var sut = BuildSut();
        var key = RandomKey();
        await sut.TrustHostKeyAsync("10.0.0.1", 22, "ssh-ed25519", key);

        var result = await sut.CheckHostKeyAsync("10.0.0.1", 22, "ssh-ed25519", key);
        Assert.Equal(HostKeyTrustResult.Trusted, result);
    }

    [Fact]
    public async Task Different_key_type_is_treated_independently()
    {
        var sut = BuildSut();
        var key = RandomKey();
        await sut.TrustHostKeyAsync("10.0.0.1", 22, "ssh-rsa", key);

        // Same host/port but different key type → still Unknown
        var result = await sut.CheckHostKeyAsync("10.0.0.1", 22, "ssh-ed25519", key);
        Assert.Equal(HostKeyTrustResult.Unknown, result);
    }

    [Fact]
    public async Task Same_host_different_port_is_treated_independently()
    {
        var sut = BuildSut();
        var key = RandomKey();
        await sut.TrustHostKeyAsync("10.0.0.1", 22, "ssh-rsa", key);

        var result = await sut.CheckHostKeyAsync("10.0.0.1", 2222, "ssh-rsa", key);
        Assert.Equal(HostKeyTrustResult.Unknown, result);
    }

    // ── Changed key ───────────────────────────────────────────────────────

    [Fact]
    public async Task Changed_key_returns_Changed()
    {
        var sut = BuildSut();
        var original = RandomKey();
        var changed = RandomKey();

        await sut.TrustHostKeyAsync("10.0.0.2", 22, "ssh-rsa", original);

        var result = await sut.CheckHostKeyAsync("10.0.0.2", 22, "ssh-rsa", changed);
        Assert.Equal(HostKeyTrustResult.Changed, result);
    }

    // ── Persistence ───────────────────────────────────────────────────────

    [Fact]
    public async Task Trusted_key_is_persisted_to_disk()
    {
        var sut = BuildSut();
        var key = RandomKey();
        await sut.TrustHostKeyAsync("persistent.host", 22, "ssh-ed25519", key);

        Assert.True(File.Exists(KnownHostsPath));
        var content = await File.ReadAllTextAsync(KnownHostsPath);
        Assert.Contains("persistent.host", content);
    }

    [Fact]
    public async Task Reloading_reads_persisted_keys()
    {
        var key = RandomKey();

        // First instance: trust and persist
        var sut1 = BuildSut();
        await sut1.TrustHostKeyAsync("reload.host", 22, "ssh-rsa", key);

        // Second instance: reads from disk
        var sut2 = BuildSut();
        var result = await sut2.CheckHostKeyAsync("reload.host", 22, "ssh-rsa", key);
        Assert.Equal(HostKeyTrustResult.Trusted, result);
    }

    [Fact]
    public async Task Works_with_nonexistent_known_hosts_file()
    {
        // File does not exist yet
        Assert.False(File.Exists(KnownHostsPath));
        var sut = BuildSut();
        var result = await sut.CheckHostKeyAsync("any.host", 22, "ssh-rsa", RandomKey());
        Assert.Equal(HostKeyTrustResult.Unknown, result);
    }

    // ── Security: cannot be bypassed for changed keys ─────────────────────

    [Fact]
    public async Task Trusting_a_key_does_not_overwrite_a_different_key_type_entry()
    {
        var sut = BuildSut();
        var rsaKey = RandomKey();
        var ed25519Key = RandomKey();

        await sut.TrustHostKeyAsync("10.0.0.3", 22, "ssh-rsa", rsaKey);
        await sut.TrustHostKeyAsync("10.0.0.3", 22, "ssh-ed25519", ed25519Key);

        // Both independent entries should be trusted
        Assert.Equal(HostKeyTrustResult.Trusted,
            await sut.CheckHostKeyAsync("10.0.0.3", 22, "ssh-rsa", rsaKey));
        Assert.Equal(HostKeyTrustResult.Trusted,
            await sut.CheckHostKeyAsync("10.0.0.3", 22, "ssh-ed25519", ed25519Key));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static byte[] RandomKey()
    {
        var key = new byte[32];
#pragma warning disable CA5394 // test data only — not used for cryptographic purposes
        Random.Shared.NextBytes(key);
#pragma warning restore CA5394
        return key;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
