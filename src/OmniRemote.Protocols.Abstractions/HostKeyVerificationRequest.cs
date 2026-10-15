namespace OmniRemote.Protocols.Abstractions;

/// <summary>
/// Passed to the host-key verification callback when an SSH host key cannot be
/// automatically trusted. The callback must return true to accept the key (which
/// will then be saved to known-hosts) or false to abort the connection.
/// </summary>
public sealed record HostKeyVerificationRequest(
    string Host,
    int Port,
    string KeyType,
    string Fingerprint,
    HostKeyTrustResult TrustResult);
