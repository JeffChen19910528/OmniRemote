using OmniRemote.Core.Models;

namespace OmniRemote.Protocols.Abstractions;

/// <summary>
/// Single source of truth for credential-store keys. The protocol sessions load passwords
/// with these exact keys ("ssh:{id}", "sftp:{id}", "rdp:{id}").
/// </summary>
public static class CredentialKeys
{
    public static string? For(ConnectionProfile profile) => profile.Protocol switch
    {
        ProtocolType.Ssh => $"ssh:{profile.Id}",
        ProtocolType.Sftp => $"sftp:{profile.Id}",
        ProtocolType.Rdp => $"rdp:{profile.Id}",
        _ => null, // Telnet has no stored credentials
    };
}
