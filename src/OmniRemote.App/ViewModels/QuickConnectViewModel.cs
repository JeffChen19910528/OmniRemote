using CommunityToolkit.Mvvm.ComponentModel;
using OmniRemote.App.Services;
using OmniRemote.Core.Models;

namespace OmniRemote.App.ViewModels;

public sealed partial class QuickConnectViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Host { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Port { get; set; } = 22;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Password { get; set; }

    /// <summary>Save this connection (and its password, in the OS keychain) as a profile.</summary>
    [ObservableProperty]
    public partial bool Remember { get; set; } = true;

    [ObservableProperty]
    public partial ProtocolType Protocol { get; set; } = ProtocolType.Ssh;

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    public ProtocolType[] AvailableProtocols { get; } =
        [ProtocolType.Ssh, ProtocolType.Sftp, ProtocolType.Telnet, ProtocolType.Rdp];

    partial void OnProtocolChanged(ProtocolType value)
    {
        Port = value switch
        {
            ProtocolType.Ssh  => 22,
            ProtocolType.Sftp => 22,
            ProtocolType.Telnet => 23,
            ProtocolType.Rdp  => 3389,
            _ => Port,
        };
    }

    public ConnectionProfile? BuildProfile()
    {
        ValidationError = null;

        var loc = LocalizationService.Instance;
        if (string.IsNullOrWhiteSpace(Host))
        {
            ValidationError = loc.ErrorHostRequired;
            return null;
        }
        if (Port is < 1 or > 65535)
        {
            ValidationError = loc.ErrorPortRange;
            return null;
        }

        var name = $"{Protocol}/{Host}:{Port}";

        return Protocol switch
        {
            ProtocolType.Ssh  => new SshConnectionProfile  { Name = name, Host = Host, Port = Port, Username = Username },
            ProtocolType.Sftp => new SftpConnectionProfile { Name = name, Host = Host, Port = Port, Username = Username },
            ProtocolType.Telnet => new TelnetConnectionProfile { Name = name, Host = Host, Port = Port },
            ProtocolType.Rdp  => new RdpConnectionProfile  { Name = name, Host = Host, Port = Port, Username = Username },
            _ => null,
        };
    }
}
