namespace OmniRemote.Protocols.Abstractions;

public interface IHostIdentityPolicy
{
    /// <summary>
    /// Returns true if the host key is trusted (known-hosts match).
    /// Returns false if unknown or changed — callers must present a decision to the user.
    /// Never auto-trusts a changed key.
    /// </summary>
    Task<HostKeyTrustResult> CheckHostKeyAsync(string host, int port, string keyType, byte[] keyData, CancellationToken cancellationToken = default);

    Task TrustHostKeyAsync(string host, int port, string keyType, byte[] keyData, CancellationToken cancellationToken = default);
}

public enum HostKeyTrustResult
{
    Trusted,
    Unknown,
    Changed,
}
