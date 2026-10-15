namespace OmniRemote.Core.Models;

public sealed class RdpConnectionProfile : ConnectionProfile
{
    public override ProtocolType Protocol => ProtocolType.Rdp;
    public override int Port { get; set; } = 3389;
    public required string Username { get; set; }
    public string? Domain { get; set; }
    public int DesktopWidth { get; set; } = 1280;
    public int DesktopHeight { get; set; } = 800;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
