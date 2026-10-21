using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniRemote.App.Services;
using OmniRemote.Core.Models;

namespace OmniRemote.App.ViewModels;

public sealed partial class ProfileEditorViewModel : ViewModelBase
{
    private bool _bypassCertConfirmed;
    private bool _hostKeyConfirmed;
    private readonly Guid? _existingId;

    // ── Common fields ──────────────────────────────────────────────────────

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Host { get; set; } = string.Empty;
    [ObservableProperty] public partial int Port { get; set; } = 22;
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial ProtocolType Protocol { get; set; } = ProtocolType.Ssh;
    [ObservableProperty] public partial string? ValidationError { get; set; }

    // ── Saved credentials ──────────────────────────────────────────────────
    // Password is never persisted by the editor itself; MainWindowViewModel hands it to
    // ICredentialStore (OS keychain) after the profile is saved.

    [ObservableProperty] public partial string? Password { get; set; }
    [ObservableProperty] public partial bool RememberPassword { get; set; } = true;
    [ObservableProperty] public partial bool HasSavedPassword { get; set; }

    public bool IsPasswordVisible => Protocol != ProtocolType.Telnet;

    // ── TLS bypass (RDP only) ──────────────────────────────────────────────

    [ObservableProperty] public partial bool BypassCertificateValidation { get; set; }
    [ObservableProperty] public partial bool IsBypassCertConfirmationVisible { get; set; }

    // ── SSH / SFTP host-key policy ─────────────────────────────────────────

    [ObservableProperty] public partial bool DisableStrictHostKeyChecking { get; set; }
    [ObservableProperty] public partial bool IsHostKeyConfirmationVisible { get; set; }

    // ── RDP display settings ───────────────────────────────────────────────

    private static readonly RdpResolutionPreset _defaultPreset = new("1920 × 1080", 1920, 1080);

    public static RdpResolutionPreset[] RdpPresets { get; } =
    [
        new("1280 × 720",  1280,  720),
        new("1280 × 800",  1280,  800),
        new("1280 × 1024", 1280, 1024),
        new("1366 × 768",  1366,  768),
        new("1600 × 900",  1600,  900),
        _defaultPreset,
        new("2560 × 1440", 2560, 1440),
        RdpResolutionPreset.Custom,
    ];

    [ObservableProperty] public partial bool RdpFullscreen { get; set; }
    [ObservableProperty] public partial int RdpDesktopWidth { get; set; } = 1920;
    [ObservableProperty] public partial int RdpDesktopHeight { get; set; } = 1080;
    [ObservableProperty] public partial RdpResolutionPreset? SelectedRdpPreset { get; set; } = _defaultPreset;

    partial void OnRdpFullscreenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRdpResolutionVisible));
        OnPropertyChanged(nameof(IsRdpCustomResolutionVisible));
    }

    partial void OnSelectedRdpPresetChanged(RdpResolutionPreset? value)
    {
        if (value is { IsCustom: false })
        {
            RdpDesktopWidth = value.Width;
            RdpDesktopHeight = value.Height;
        }
        OnPropertyChanged(nameof(IsRdpCustomResolutionVisible));
    }

    // ── Visibility helpers ─────────────────────────────────────────────────

    public bool IsBypassCertSettingVisible => Protocol == ProtocolType.Rdp;
    public bool IsHostKeySettingVisible => Protocol == ProtocolType.Ssh || Protocol == ProtocolType.Sftp;
    public bool IsUsernameRequired => Protocol != ProtocolType.Telnet;
    public bool IsRdpDisplayVisible => Protocol == ProtocolType.Rdp;
    public bool IsRdpResolutionVisible => Protocol == ProtocolType.Rdp && !RdpFullscreen;
    public bool IsRdpCustomResolutionVisible => IsRdpResolutionVisible && SelectedRdpPreset?.IsCustom == true;

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
        _existingId = existing.Id;

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

        if (existing is RdpConnectionProfile rdpProfile)
        {
            RdpFullscreen = rdpProfile.Fullscreen;
            RdpDesktopWidth = rdpProfile.DesktopWidth;
            RdpDesktopHeight = rdpProfile.DesktopHeight;
            SelectedRdpPreset = Array.Find(RdpPresets, p => !p.IsCustom && p.Width == rdpProfile.DesktopWidth && p.Height == rdpProfile.DesktopHeight)
                ?? RdpResolutionPreset.Custom;
        }

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
        OnPropertyChanged(nameof(IsPasswordVisible));
        OnPropertyChanged(nameof(IsRdpDisplayVisible));
        OnPropertyChanged(nameof(IsRdpResolutionVisible));
        OnPropertyChanged(nameof(IsRdpCustomResolutionVisible));
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

        var loc = LocalizationService.Instance;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = loc.ErrorNameRequired;
            return;
        }
        if (string.IsNullOrWhiteSpace(Host))
        {
            ValidationError = loc.ErrorHostRequired;
            return;
        }
        if (IsUsernameRequired && string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = loc.ErrorUsernameRequired;
            return;
        }
        if (Port is < 1 or > 65535)
        {
            ValidationError = loc.ErrorPortRange;
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
            Id = _existingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            StrictHostKeyChecking = !DisableStrictHostKeyChecking,
        },
        ProtocolType.Sftp => new SftpConnectionProfile
        {
            Id = _existingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            StrictHostKeyChecking = !DisableStrictHostKeyChecking,
        },
        ProtocolType.Telnet => new TelnetConnectionProfile
        {
            Id = _existingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Description = Description?.Trim(),
        },
        ProtocolType.Rdp => new RdpConnectionProfile
        {
            Id = _existingId ?? Guid.NewGuid(),
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Description = Description?.Trim(),
            BypassCertificateValidation = BypassCertificateValidation,
            Fullscreen = RdpFullscreen,
            DesktopWidth = RdpDesktopWidth,
            DesktopHeight = RdpDesktopHeight,
        },
        _ => throw new InvalidOperationException($"Unknown protocol: {Protocol}"),
    };
}

public sealed record RdpResolutionPreset(string Label, int Width, int Height)
{
    public bool IsCustom => Width == 0;
    public static readonly RdpResolutionPreset Custom = new("Custom", 0, 0);
    public override string ToString() => Label;
}
