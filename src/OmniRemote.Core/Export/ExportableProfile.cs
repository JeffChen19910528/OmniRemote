namespace OmniRemote.Core.Export;

/// <summary>
/// Safe export DTO — contains connection metadata only. Secrets are never included.
/// Callers must not add password, key, or token fields to this type.
/// </summary>
public sealed class ExportableProfile
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public required string Protocol { get; init; }
    public string? Description { get; init; }
    public string? Tags { get; init; }
    public bool BypassCertificateValidation { get; init; }

    /// <summary>Protocol-specific settings that are safe to export (no secrets).</summary>
    public Dictionary<string, object?> Settings { get; init; } = [];
}
