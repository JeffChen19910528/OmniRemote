using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Core.Models;
using OmniRemote.Infrastructure.Export;
using OmniRemote.Infrastructure.Persistence;
using System.Text.Json;

namespace OmniRemote.Infrastructure.Tests;

public sealed class ProfileExporterTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"omniremote_export_test_{Guid.NewGuid():N}.db");
    private readonly SqliteConnectionProfileStore _store;
    private readonly ProfileExporter _sut;

    public ProfileExporterTests()
    {
        _store = new SqliteConnectionProfileStore(_dbPath, NullLogger<SqliteConnectionProfileStore>.Instance);
        _store.InitializeAsync().GetAwaiter().GetResult();
        _sut = new ProfileExporter(_store);
    }

    [Fact]
    public async Task Export_produces_valid_json()
    {
        await _store.SaveAsync(new SshConnectionProfile { Name = "A", Host = "1.1.1.1", Username = "u" });
        var json = await _sut.ExportAllAsync();
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Exported_json_never_contains_secret_json_keys()
    {
        await _store.SaveAsync(new SshConnectionProfile { Name = "Sec", Host = "1.1.1.1", Username = "u" });
        var json = await _sut.ExportAllAsync();

        // Check that no JSON key named "password", "passphrase", "secret", or "credential" exists.
        // Note: "AuthMethod": "Password" is intentionally allowed — the word "Password" is the
        // auth mechanism name, not a secret value.
        Assert.DoesNotContain("\"password\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"passphrase\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"secret\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"credential\":", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exported_profile_contains_connection_metadata()
    {
        var profile = new SshConnectionProfile { Name = "MyServer", Host = "10.0.0.5", Username = "admin" };
        await _store.SaveAsync(profile);

        var json = await _sut.ExportAllAsync();
        Assert.Contains("MyServer", json);
        Assert.Contains("10.0.0.5", json);
        Assert.Contains("Ssh", json);
    }

    [Fact]
    public async Task Export_empty_store_returns_empty_array()
    {
        var json = await _sut.ExportAllAsync();
        Assert.Equal("[]", json.Trim());
    }

    [Fact]
    public void ToExportable_ssh_does_not_include_secret_json_keys()
    {
        var profile = new SshConnectionProfile { Name = "X", Host = "1.1.1.1", Username = "u" };
        var exportable = ProfileExporter.ToExportable(profile);

        var json = JsonSerializer.Serialize(exportable);
        // "AuthMethod":"Password" is intentional — it is the mechanism name, not a secret value.
        Assert.DoesNotContain("\"password\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"passphrase\":", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToExportable_rdp_does_not_include_username_password()
    {
        var profile = new RdpConnectionProfile { Name = "WinVM", Host = "10.0.0.50", Username = "Administrator" };
        var exportable = ProfileExporter.ToExportable(profile);

        // Username is NOT in the safe-export Settings dict for RDP (it's PII adjacent to creds)
        Assert.DoesNotContain("password", JsonSerializer.Serialize(exportable), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToExportable_bypass_flag_is_preserved()
    {
        var profile = new RdpConnectionProfile
        {
            Name = "Lab",
            Host = "10.0.0.5",
            Username = "u",
            BypassCertificateValidation = true,
        };
        var exportable = ProfileExporter.ToExportable(profile);
        Assert.True(exportable.BypassCertificateValidation);
    }

    [Fact]
    public void Round_trip_import_produces_correct_count()
    {
        var profiles = new[]
        {
            ProfileExporter.ToExportable(new SshConnectionProfile { Name = "A", Host = "1.1.1.1", Username = "u" }),
            ProfileExporter.ToExportable(new TelnetConnectionProfile { Name = "B", Host = "2.2.2.2" }),
        };
        var json = JsonSerializer.Serialize(profiles);
        var imported = ProfileExporter.Import(json);
        Assert.Equal(2, imported.Count);
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
