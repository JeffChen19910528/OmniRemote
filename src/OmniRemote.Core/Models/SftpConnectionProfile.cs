namespace OmniRemote.Core.Models;

public sealed class SftpConnectionProfile : ConnectionProfile
{
    public override ProtocolType Protocol => ProtocolType.Sftp;
    public override int Port { get; set; } = 22;
    public required string Username { get; set; }
    public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.Password;
    public string? PrivateKeyPath { get; set; }
    public bool StrictHostKeyChecking { get; set; } = true;

    public override bool IsBypassActive => !StrictHostKeyChecking;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
