using OmniRemote.Core.Export;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System.Text.Json;

namespace OmniRemote.Infrastructure.Export;

public sealed class ProfileExporter
{
    private readonly IConnectionProfileStore _store;

    public ProfileExporter(IConnectionProfileStore store) => _store = store;

    /// <summary>
    /// Exports all saved profiles as JSON. Secrets are never included.
    /// </summary>
    public async Task<string> ExportAllAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _store.GetAllAsync(cancellationToken);
        var exportable = profiles.Select(ToExportable).ToList();
        return JsonSerializer.Serialize(exportable, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Imports profiles from previously exported JSON. No secrets are imported.
    /// Caller must save credentials separately after import.
    /// </summary>
    public static IReadOnlyList<ExportableProfile> Import(string json)
    {
        return JsonSerializer.Deserialize<List<ExportableProfile>>(json) ?? [];
    }

    public static ExportableProfile ToExportable(ConnectionProfile profile)
    {
        var settings = BuildSettings(profile);
        return new ExportableProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Host = profile.Host,
            Port = profile.Port,
            Protocol = profile.Protocol.ToString(),
            Description = profile.Description,
            Tags = profile.Tags,
            BypassCertificateValidation = profile.BypassCertificateValidation,
            Settings = settings,
        };
    }

    private static Dictionary<string, object?> BuildSettings(ConnectionProfile profile) =>
        profile switch
        {
            SshConnectionProfile ssh => new()
            {
                ["AuthMethod"] = ssh.AuthMethod.ToString(),
                ["StrictHostKeyChecking"] = ssh.StrictHostKeyChecking,
                ["ConnectTimeoutSeconds"] = (int)ssh.ConnectTimeout.TotalSeconds,
                ["KeepAliveIntervalSeconds"] = (int)ssh.KeepAliveInterval.TotalSeconds,
                // PrivateKeyPath exported for portability; contains path only, not key material
                ["PrivateKeyPath"] = ssh.PrivateKeyPath,
            },
            SftpConnectionProfile sftp => new()
            {
                ["AuthMethod"] = sftp.AuthMethod.ToString(),
                ["StrictHostKeyChecking"] = sftp.StrictHostKeyChecking,
                ["ConnectTimeoutSeconds"] = (int)sftp.ConnectTimeout.TotalSeconds,
                ["PrivateKeyPath"] = sftp.PrivateKeyPath,
            },
            TelnetConnectionProfile telnet => new()
            {
                ["TerminalEncoding"] = telnet.TerminalEncoding,
                ["ConnectTimeoutSeconds"] = (int)telnet.ConnectTimeout.TotalSeconds,
            },
            RdpConnectionProfile rdp => new()
            {
                ["Domain"] = rdp.Domain,
                ["DesktopWidth"] = rdp.DesktopWidth,
                ["DesktopHeight"] = rdp.DesktopHeight,
                ["ConnectTimeoutSeconds"] = (int)rdp.ConnectTimeout.TotalSeconds,
            },
            _ => [],
        };
}
