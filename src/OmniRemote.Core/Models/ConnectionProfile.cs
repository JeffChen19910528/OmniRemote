namespace OmniRemote.Core.Models;

public abstract class ConnectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Host { get; set; }
    public abstract ProtocolType Protocol { get; }
    public abstract int Port { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }

    /// <summary>
    /// Per-profile opt-in to bypass TLS certificate validation. Off by default.
    /// Has no effect for SSH — SSH uses IHostIdentityPolicy / StrictHostKeyChecking instead.
    /// </summary>
    public bool BypassCertificateValidation { get; set; } = false;

    /// <summary>
    /// True when any security validation has been weakened for this profile.
    /// Drives the persistent visual warning in the UI.
    /// </summary>
    public virtual bool IsBypassActive => BypassCertificateValidation;
}
