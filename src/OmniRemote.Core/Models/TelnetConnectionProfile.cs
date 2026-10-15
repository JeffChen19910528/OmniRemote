namespace OmniRemote.Core.Models;

public sealed class TelnetConnectionProfile : ConnectionProfile
{
    public override ProtocolType Protocol => ProtocolType.Telnet;
    public override int Port { get; set; } = 23;
    public string TerminalEncoding { get; set; } = "utf-8";
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
