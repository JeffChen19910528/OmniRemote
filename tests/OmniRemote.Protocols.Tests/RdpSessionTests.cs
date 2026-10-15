using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using OmniRemote.Protocols.Rdp;
using System.Runtime.InteropServices;

namespace OmniRemote.Protocols.Tests;

/// <summary>
/// Unit tests for RdpSession and RdpBackendLocator.
/// Tests that require a real RDP server are documented in docs/TESTING.md as BLOCKED.
/// </summary>
public sealed class RdpSessionTests
{
    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Initial_state_is_Disconnected()
    {
        var session = MakeSession(new NullBackendLocator());
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    [Fact]
    public void Protocol_is_Rdp()
    {
        var session = MakeSession(new NullBackendLocator());
        Assert.Equal(ProtocolType.Rdp, session.Protocol);
    }

    [Fact]
    public void DisplayName_contains_username_and_host()
    {
        var profile = MakeProfile();
        var session = MakeSession(new NullBackendLocator(), profile);
        Assert.Contains(profile.Username, session.DisplayName);
        Assert.Contains(profile.Host, session.DisplayName);
    }

    // ── Missing backend ───────────────────────────────────────────────────

    [Fact]
    public async Task Missing_backend_transitions_to_Failed()
    {
        var session = MakeSession(new NullBackendLocator());
        var states = new List<ConnectionState>();
        session.StateChanged += (_, s) => states.Add(s);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());

        Assert.Equal(ConnectionState.Failed, session.State);
        // No Connecting state — backend check fires before state change
        Assert.DoesNotContain(ConnectionState.Connecting, states);
    }

    [Fact]
    public async Task Missing_backend_error_message_mentions_install_instructions()
    {
        var session = MakeSession(new NullBackendLocator());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ConnectAsync());
        Assert.Contains("xfreerdp", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Quick-exit backend (backend found but exits immediately) ──────────

    [Fact]
    public async Task Backend_that_exits_immediately_transitions_to_Failed()
    {
        // The fake launcher ignores the RDP ProcessStartInfo and spawns a real process
        // that exits immediately, simulating a backend that fails right after launch.
        var fakeLauncher = new ImmediatelyExitingProcessLauncher();
        if (!fakeLauncher.IsSupported)
        {
            // BLOCKED: no suitable immediate-exit executable on this platform
            return;
        }

        // Backend locator returns something non-null so ConnectAsync proceeds past the preflight check.
        var session = MakeSession(new FixedBackendLocator("/fake/rdp"), launcher: fakeLauncher);
        var states = new List<ConnectionState>();
        session.StateChanged += (_, s) => states.Add(s);

        await Assert.ThrowsAnyAsync<Exception>(() => session.ConnectAsync());

        Assert.Contains(ConnectionState.Connecting, states);
        Assert.Contains(ConnectionState.Failed, states);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_when_never_connected_does_not_throw()
    {
        await using var session = MakeSession(new NullBackendLocator());
    }

    // ── Disconnect before connect ─────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_before_connect_does_not_throw()
    {
        var session = MakeSession(new NullBackendLocator());
        await session.DisconnectAsync();
        Assert.Equal(ConnectionState.Disconnected, session.State);
    }

    // ── BypassCertificateValidation default ──────────────────────────────

    [Fact]
    public void BypassCertificateValidation_is_false_by_default()
    {
        var profile = MakeProfile();
        Assert.False(profile.BypassCertificateValidation);
    }

    // ── RdpBackendLocator ─────────────────────────────────────────────────

    [Fact]
    public void BackendLocator_returns_null_when_PATH_is_empty()
    {
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "");

            // On Linux: no xfreerdp on empty PATH → null
            // On Windows: mstsc path is absolute (not PATH-dependent), so skip this assertion
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var locator = new RdpBackendLocator();
                // Can't reliably null mstsc on Linux (it won't be present anyway), just verify no throw
                var result = locator.FindBackend();
                Assert.Null(result);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
        }
    }

    [Fact]
    public void BackendLocator_finds_executable_on_custom_PATH()
    {
        // Create a temp dir with a fake xfreerdp executable
        var tempDir = Path.Combine(Path.GetTempPath(), $"rdp_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "xfreerdp.exe" : "xfreerdp";
            File.WriteAllText(Path.Combine(tempDir, exeName), "");

            var originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            Environment.SetEnvironmentVariable("PATH", tempDir + separator + originalPath);
            try
            {
                Assert.True(RdpBackendLocator.IsOnPath("xfreerdp"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BackendLocator_mstsc_found_on_Windows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var locator = new RdpBackendLocator();
        var backend = locator.FindBackend();
        Assert.NotNull(backend);
        Assert.Contains("mstsc", backend, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static RdpConnectionProfile MakeProfile() => new()
    {
        Name = "Test RDP",
        Host = "127.0.0.1",
        Username = "testuser",
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    private static RdpSession MakeSession(
        IRdpBackendLocator locator,
        RdpConnectionProfile? profile = null,
        IProcessLauncher? launcher = null) =>
        new(
            profile ?? MakeProfile(),
            new AlwaysNullCredentialStore(),
            new PassthroughRedactor(),
            NullLogger<RdpSession>.Instance,
            locator,
            launcher ?? new DefaultProcessLauncher());

    private static string? GetImmediatelyExitingBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            return File.Exists(cmd) ? cmd : null;
        }
        if (File.Exists("/bin/false")) return "/bin/false";
        if (File.Exists("/usr/bin/false")) return "/usr/bin/false";
        return null;
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class NullBackendLocator : IRdpBackendLocator
    {
        public string? FindBackend() => null;
    }

    private sealed class FixedBackendLocator : IRdpBackendLocator
    {
        private readonly string _path;
        public FixedBackendLocator(string path) => _path = path;
        public string? FindBackend() => _path;
    }

    private sealed class AlwaysNullCredentialStore : ICredentialStore
    {
        public Task SaveAsync(string key, string secret, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> LoadAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PassthroughRedactor : ISessionLogRedactor
    {
        public string Redact(string message) => message;
    }

    /// <summary>
    /// Ignores the ProcessStartInfo from RdpSession and spawns a real process that exits
    /// immediately with a non-zero exit code, simulating a backend failure at launch.
    /// </summary>
    private sealed class ImmediatelyExitingProcessLauncher : IProcessLauncher
    {
        public bool IsSupported { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
                : File.Exists("/bin/false") || File.Exists("/usr/bin/false");

        public System.Diagnostics.Process? Start(System.Diagnostics.ProcessStartInfo _)
        {
            System.Diagnostics.ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                psi = new System.Diagnostics.ProcessStartInfo(cmd) { UseShellExecute = false };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("exit 1");
            }
            else
            {
                var falseBin = File.Exists("/bin/false") ? "/bin/false" : "/usr/bin/false";
                psi = new System.Diagnostics.ProcessStartInfo(falseBin) { UseShellExecute = false };
            }
            return System.Diagnostics.Process.Start(psi);
        }
    }
}
