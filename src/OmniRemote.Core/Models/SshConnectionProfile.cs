namespace OmniRemote.Core.Models;

public sealed class SshConnectionProfile : ConnectionProfile
{
    public override ProtocolType Protocol => ProtocolType.Ssh;
    public override int Port { get; set; } = 22;
    public required string Username { get; set; }
    public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.Password;
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Strictly verifies host key against known_hosts. On by default.
    /// Separate from BypassCertificateValidation — never conflate the two.
    /// </summary>
    public bool StrictHostKeyChecking { get; set; } = true;

    public override bool IsBypassActive => !StrictHostKeyChecking;

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(60);
}

public enum SshAuthMethod
{
    Password,
    PrivateKey,
    KeyboardInteractive,
}
