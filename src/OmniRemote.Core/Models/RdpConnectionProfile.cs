namespace OmniRemote.Core.Models;

public sealed class RdpConnectionProfile : ConnectionProfile
{
    public override ProtocolType Protocol => ProtocolType.Rdp;
    public override int Port { get; set; } = 3389;
    public required string Username { get; set; }
    public string? Domain { get; set; }
    public bool Fullscreen { get; set; } = false;
    public int DesktopWidth { get; set; } = 1920;
    public int DesktopHeight { get; set; } = 1080;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
