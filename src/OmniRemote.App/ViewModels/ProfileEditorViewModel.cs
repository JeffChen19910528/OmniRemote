using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniRemote.Core.Models;

namespace OmniRemote.App.ViewModels;

public sealed partial class ProfileEditorViewModel : ViewModelBase
{
    private bool _bypassCertConfirmed;
    private bool _hostKeyConfirmed;

    // ── Common fields ──────────────────────────────────────────────────────

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Host { get; set; } = string.Empty;
    [ObservableProperty] public partial int Port { get; set; } = 22;
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial ProtocolType Protocol { get; set; } = ProtocolType.Ssh;
    [ObservableProperty] public partial string? ValidationError { get; set; }

    // ── TLS bypass (RDP only) ──────────────────────────────────────────────

    [ObservableProperty] public partial bool BypassCertificateValidation { get; set; }
    [ObservableProperty] public partial bool IsBypassCertConfirmationVisible { get; set; }

    // ── SSH / SFTP host-key policy ─────────────────────────────────────────

    [ObservableProperty] public partial bool DisableStrictHostKeyChecking { get; set; }
    [ObservableProperty] public partial bool IsHostKeyConfirmationVisible { get; set; }

    // ── Visibility helpers ─────────────────────────────────────────────────

    public bool IsBypassCertSettingVisible => Protocol == ProtocolType.Rdp;
    public bool IsHostKeySettingVisible => Protocol == ProtocolType.Ssh || Protocol == ProtocolType.Sftp;
    public bool IsUsernameRequired => Protocol != ProtocolType.Telnet;

    public ProtocolType[] AvailableProtocols { get; } =
        [ProtocolType.Ssh, ProtocolType.Sftp, ProtocolType.Telnet, ProtocolType.Rdp];

    // ── Events ────────────────────────────────────────────────────────────

    public event EventHandler<ConnectionProfile>? ProfileSaved;
    public event EventHandler? Cancelled;

    // ── Constructors ──────────────────────────────────────────────────────

    public ProfileEditorViewModel() { }

    public ProfileEditorViewModel(ConnectionProfile existing)
    {
        // Set Protocol first so OnProtocolChanged can adjust Port defaults.
        // OnProtocolChanged resets the guard flags to false at its end,
        // so we re-enable them AFTER the protocol assignment.
        Protocol = existing.Protocol;

        _bypassCertConfirmed = true;
        _hostKeyConfirmed = true;

        Name = existing.Name;
        Host = existing.Host;
        Port = existing.Port;
        Description = existing.Description;
        BypassCertificateValidation = existing.BypassCertificateValidation;

        Username = existing switch
        {
            SshConnectionProfile ssh   => ssh.Username,
            SftpConnectionProfile sftp => sftp.Username,
            RdpConnectionProfile rdp   => rdp.Username,
            _ => string.Empty,
        };

        DisableStrictHostKeyChecking = existing switch
        {
            SshConnectionProfile ssh   => !ssh.StrictHostKeyChecking,
            SftpConnectionProfile sftp => !sftp.StrictHostKeyChecking,
            _ => false,
        };

        _bypassCertConfirmed = false;
        _hostKeyConfirmed = false;
    }

    // ── Property change handlers ───────────────────────────────────────────

    partial void OnProtocolChanged(ProtocolType value)
    {
        Port = value switch
        {
            ProtocolType.Ssh    => 22,
            ProtocolType.Sftp   => 22,
            ProtocolType.Telnet => 23,
            ProtocolType.Rdp    => 3389,
            _ => Port,
        };

        _bypassCertConfirmed = true;
        _hostKeyConfirmed = true;
        BypassCertificateValidation = false;
        DisableStrictHostKeyChecking = false;
        IsBypassCertConfirmationVisible = false;
        IsHostKeyConfirmationVisible = false;
        _bypassCertConfirmed = false;
        _hostKeyConfirmed = false;

        OnPropertyChanged(nameof(IsBypassCertSettingVisible));
        OnPropertyChanged(nameof(IsHostKeySettingVisible));
        OnPropertyChanged(nameof(IsUsernameRequired));
    }

    partial void OnBypassCertificateValidationChanged(bool value)
    {
        if (value && !_bypassCertConfirmed)
        {
            // Reset and require explicit confirmation before enabling bypass.
            _bypassCertConfirmed = true;
            BypassCertificateValidation = false;
            _bypassCertConfirmed = false;
            IsBypassCertConfirmationVisible = true;
        }
        else if (!value)
        {
            IsBypassCertConfirmationVisible = false;
        }
    }

    partial void OnDisableStrictHostKeyCheckingChanged(bool value)
    {
        if (value && !_hostKeyConfirmed)
        {
            _hostKeyConfirmed = true;
            DisableStrictHostKeyChecking = false;
            _hostKeyConfirmed = false;
            IsHostKeyConfirmationVisible = true;
        }
        else if (!value)
        {
            IsHostKeyConfirmationVisible = false;
        }
    }

    // ── Bypass confirmation commands ───────────────────────────────────────

    [RelayCommand]
    public void ConfirmBypassCert()
    {
        IsBypassCertConfirmationVisible = false;
        _bypassCertConfirmed = true;
        BypassCertificateValidation = true;
        _bypassCertConfirmed = false;
    }

    [RelayCommand]
    public void CancelBypassCert() => IsBypassCertConfirmationVisible = false;

    [RelayCommand]
    public void ConfirmDisableHostKey()
    {
        IsHostKeyConfirmationVisible = false;
        _hostKeyConfirmed = true;
        DisableStrictHostKeyChecking = true;
        _hostKeyConfirmed = false;
    }

    [RelayCommand]
    public void CancelDisableHostKey() => IsHostKeyConfirmationVisible = false;

    // ── Save / Cancel ──────────────────────────────────────────────────────

    [RelayCommand]
    public void Save()
    {
        ValidationError = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Name is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Host))
        {
            ValidationError = "Host is required.";
            return;
        }
        if (IsUsernameRequired && string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = "Username is required for this protocol.";
            return;
        }
        if (Port is < 1 or > 65535)
        {
            ValidationError = "Port must be between 1 and 65535.";
            return;
        }

        ProfileSaved?.Invoke(this, BuildProfile());
    }

    [RelayCommand]
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    // ── Profile builder ────────────────────────────────────────────────────

    public ConnectionProfile BuildProfile() => Protocol switch
    {
        ProtocolType.Ssh => new SshConnectionProfile
        {
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            StrictHostKeyChecking = !DisableStrictHostKeyChecking,
        },
        ProtocolType.Sftp => new SftpConnectionProfile
        {
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            StrictHostKeyChecking = !DisableStrictHostKeyChecking,
        },
        ProtocolType.Telnet => new TelnetConnectionProfile
        {
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Description = Description?.Trim(),
        },
        ProtocolType.Rdp => new RdpConnectionProfile
        {
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            BypassCertificateValidation = BypassCertificateValidation,
        },
        _ => throw new InvalidOperationException($"Unknown protocol: {Protocol}"),
    };
}
