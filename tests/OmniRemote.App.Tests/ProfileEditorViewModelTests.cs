using OmniRemote.App.ViewModels;
using OmniRemote.Core.Models;
using System.Linq;

namespace OmniRemote.App.Tests;

public sealed class ProfileEditorViewModelTests
{
    // ── Defaults ──────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_have_secure_settings()
    {
        var vm = new ProfileEditorViewModel();
        Assert.False(vm.BypassCertificateValidation);
        Assert.False(vm.DisableStrictHostKeyChecking);
        Assert.False(vm.IsBypassCertConfirmationVisible);
        Assert.False(vm.IsHostKeyConfirmationVisible);
    }

    // ── TLS bypass confirmation flow ──────────────────────────────────────

    [Fact]
    public void Setting_BypassCertificateValidation_true_shows_confirmation_and_resets()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };

        vm.BypassCertificateValidation = true;

        Assert.False(vm.BypassCertificateValidation);
        Assert.True(vm.IsBypassCertConfirmationVisible);
    }

    [Fact]
    public void ConfirmBypassCert_enables_bypass_and_hides_confirmation()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.BypassCertificateValidation = true; // triggers confirmation

        vm.ConfirmBypassCert();

        Assert.True(vm.BypassCertificateValidation);
        Assert.False(vm.IsBypassCertConfirmationVisible);
    }

    [Fact]
    public void CancelBypassCert_keeps_bypass_false_and_hides_confirmation()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.BypassCertificateValidation = true; // triggers confirmation

        vm.CancelBypassCert();

        Assert.False(vm.BypassCertificateValidation);
        Assert.False(vm.IsBypassCertConfirmationVisible);
    }

    [Fact]
    public void Setting_bypass_false_when_confirmed_hides_confirmation()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.BypassCertificateValidation = true;
        vm.ConfirmBypassCert(); // now true

        vm.BypassCertificateValidation = false;

        Assert.False(vm.BypassCertificateValidation);
        Assert.False(vm.IsBypassCertConfirmationVisible);
    }

    // ── Host-key confirmation flow ────────────────────────────────────────

    [Fact]
    public void Setting_DisableStrictHostKeyChecking_true_shows_confirmation_and_resets()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Ssh };

        vm.DisableStrictHostKeyChecking = true;

        Assert.False(vm.DisableStrictHostKeyChecking);
        Assert.True(vm.IsHostKeyConfirmationVisible);
    }

    [Fact]
    public void ConfirmDisableHostKey_disables_checking_and_hides_confirmation()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Ssh };
        vm.DisableStrictHostKeyChecking = true;

        vm.ConfirmDisableHostKey();

        Assert.True(vm.DisableStrictHostKeyChecking);
        Assert.False(vm.IsHostKeyConfirmationVisible);
    }

    [Fact]
    public void CancelDisableHostKey_keeps_checking_enabled_and_hides_confirmation()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Ssh };
        vm.DisableStrictHostKeyChecking = true;

        vm.CancelDisableHostKey();

        Assert.False(vm.DisableStrictHostKeyChecking);
        Assert.False(vm.IsHostKeyConfirmationVisible);
    }

    // ── Protocol switch resets bypass ─────────────────────────────────────

    [Fact]
    public void Protocol_change_resets_bypass_settings()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.BypassCertificateValidation = true;
        vm.ConfirmBypassCert();
        Assert.True(vm.BypassCertificateValidation);

        vm.Protocol = ProtocolType.Ssh;

        Assert.False(vm.BypassCertificateValidation);
        Assert.False(vm.DisableStrictHostKeyChecking);
        Assert.False(vm.IsBypassCertConfirmationVisible);
        Assert.False(vm.IsHostKeyConfirmationVisible);
    }

    // ── Visibility helpers ────────────────────────────────────────────────

    [Theory]
    [InlineData(ProtocolType.Rdp, true)]
    [InlineData(ProtocolType.Ssh, false)]
    [InlineData(ProtocolType.Sftp, false)]
    [InlineData(ProtocolType.Telnet, false)]
    public void IsBypassCertSettingVisible_only_for_Rdp(ProtocolType protocol, bool expected)
    {
        var vm = new ProfileEditorViewModel { Protocol = protocol };
        Assert.Equal(expected, vm.IsBypassCertSettingVisible);
    }

    [Theory]
    [InlineData(ProtocolType.Ssh, true)]
    [InlineData(ProtocolType.Sftp, true)]
    [InlineData(ProtocolType.Rdp, false)]
    [InlineData(ProtocolType.Telnet, false)]
    public void IsHostKeySettingVisible_for_Ssh_and_Sftp(ProtocolType protocol, bool expected)
    {
        var vm = new ProfileEditorViewModel { Protocol = protocol };
        Assert.Equal(expected, vm.IsHostKeySettingVisible);
    }

    [Fact]
    public void IsUsernameRequired_false_for_Telnet()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Telnet };
        Assert.False(vm.IsUsernameRequired);
    }

    // ── BuildProfile ──────────────────────────────────────────────────────

    [Fact]
    public void BuildProfile_SSH_StrictHostKeyChecking_on_by_default()
    {
        var vm = MakeValidVm(ProtocolType.Ssh);

        var profile = (SshConnectionProfile)vm.BuildProfile();

        Assert.True(profile.StrictHostKeyChecking);
    }

    [Fact]
    public void BuildProfile_SSH_reflects_DisableStrictHostKeyChecking()
    {
        var vm = MakeValidVm(ProtocolType.Ssh);
        vm.DisableStrictHostKeyChecking = true;
        vm.ConfirmDisableHostKey();

        var profile = (SshConnectionProfile)vm.BuildProfile();

        Assert.False(profile.StrictHostKeyChecking);
    }

    [Fact]
    public void BuildProfile_SFTP_reflects_DisableStrictHostKeyChecking()
    {
        var vm = MakeValidVm(ProtocolType.Sftp);
        vm.DisableStrictHostKeyChecking = true;
        vm.ConfirmDisableHostKey();

        var profile = (SftpConnectionProfile)vm.BuildProfile();

        Assert.False(profile.StrictHostKeyChecking);
    }

    [Fact]
    public void BuildProfile_RDP_BypassCertificateValidation_reflects_confirmed_bypass()
    {
        var vm = MakeValidVm(ProtocolType.Rdp);
        vm.BypassCertificateValidation = true;
        vm.ConfirmBypassCert();

        var profile = (RdpConnectionProfile)vm.BuildProfile();

        Assert.True(profile.BypassCertificateValidation);
    }

    [Fact]
    public void BuildProfile_Telnet_requires_no_username()
    {
        var vm = new ProfileEditorViewModel
        {
            Protocol = ProtocolType.Telnet,
            Name = "BBS", Host = "telnet.example.com",
        };

        var profile = vm.BuildProfile();

        Assert.IsType<TelnetConnectionProfile>(profile);
    }

    // ── Save validation ───────────────────────────────────────────────────

    [Fact]
    public void Save_with_empty_Name_sets_ValidationError()
    {
        var vm = new ProfileEditorViewModel { Host = "h.example.com", Username = "u" };
        vm.Save();
        Assert.NotNull(vm.ValidationError);
    }

    [Fact]
    public void Save_with_empty_Host_sets_ValidationError()
    {
        var vm = new ProfileEditorViewModel { Name = "Test", Username = "u" };
        vm.Save();
        Assert.NotNull(vm.ValidationError);
    }

    [Fact]
    public void Save_SSH_with_empty_Username_sets_ValidationError()
    {
        var vm = new ProfileEditorViewModel
        {
            Protocol = ProtocolType.Ssh, Name = "Test", Host = "h.example.com",
        };
        vm.Save();
        Assert.NotNull(vm.ValidationError);
    }

    [Fact]
    public void Save_fires_ProfileSaved_event_with_correct_profile()
    {
        var vm = MakeValidVm(ProtocolType.Ssh);
        ConnectionProfile? saved = null;
        vm.ProfileSaved += (_, p) => saved = p;

        vm.Save();

        Assert.NotNull(saved);
        Assert.Equal("test", saved!.Host);
        Assert.Equal(22, saved.Port);
    }

    [Fact]
    public void Cancel_fires_Cancelled_event()
    {
        var vm = new ProfileEditorViewModel();
        var fired = false;
        vm.Cancelled += (_, _) => fired = true;

        vm.Cancel();

        Assert.True(fired);
    }

    // ── Constructor with existing profile ─────────────────────────────────

    [Fact]
    public void Constructor_with_SSH_profile_populates_all_fields()
    {
        var profile = new SshConnectionProfile
        {
            Name = "Prod", Host = "prod.example.com", Port = 2222,
            Username = "deploy", StrictHostKeyChecking = false,
        };

        var vm = new ProfileEditorViewModel(profile);

        Assert.Equal("Prod", vm.Name);
        Assert.Equal("prod.example.com", vm.Host);
        Assert.Equal(2222, vm.Port);
        Assert.Equal("deploy", vm.Username);
        Assert.True(vm.DisableStrictHostKeyChecking);
        Assert.False(vm.IsHostKeyConfirmationVisible); // no confirmation during init
    }

    [Fact]
    public void Constructor_with_RDP_profile_populates_bypass_flag()
    {
        var profile = new RdpConnectionProfile
        {
            Name = "VDI", Host = "vdi.corp.local", Port = 3389,
            Username = "user", BypassCertificateValidation = true,
        };

        var vm = new ProfileEditorViewModel(profile);

        Assert.True(vm.BypassCertificateValidation);
        Assert.False(vm.IsBypassCertConfirmationVisible); // no confirmation during init
    }

    // ── IsBypassActive on ConnectionProfile ───────────────────────────────

    [Fact]
    public void SshConnectionProfile_IsBypassActive_false_when_strict_host_key_on()
    {
        var p = new SshConnectionProfile
        {
            Name = "x", Host = "x", Username = "u", StrictHostKeyChecking = true,
        };
        Assert.False(p.IsBypassActive);
    }

    [Fact]
    public void SshConnectionProfile_IsBypassActive_true_when_strict_host_key_off()
    {
        var p = new SshConnectionProfile
        {
            Name = "x", Host = "x", Username = "u", StrictHostKeyChecking = false,
        };
        Assert.True(p.IsBypassActive);
    }

    [Fact]
    public void RdpConnectionProfile_IsBypassActive_true_when_cert_bypass_on()
    {
        var p = new RdpConnectionProfile
        {
            Name = "x", Host = "x", Username = "u", BypassCertificateValidation = true,
        };
        Assert.True(p.IsBypassActive);
    }

    [Fact]
    public void TelnetConnectionProfile_IsBypassActive_false_always()
    {
        var p = new TelnetConnectionProfile { Name = "x", Host = "x" };
        Assert.False(p.IsBypassActive);
    }

    // ── RDP display settings ──────────────────────────────────────────────

    [Theory]
    [InlineData(ProtocolType.Rdp,    true)]
    [InlineData(ProtocolType.Ssh,    false)]
    [InlineData(ProtocolType.Sftp,   false)]
    [InlineData(ProtocolType.Telnet, false)]
    public void IsRdpDisplayVisible_only_for_Rdp(ProtocolType protocol, bool expected)
    {
        var vm = new ProfileEditorViewModel { Protocol = protocol };
        Assert.Equal(expected, vm.IsRdpDisplayVisible);
    }

    [Fact]
    public void IsRdpResolutionVisible_false_when_fullscreen()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp, RdpFullscreen = true };
        Assert.False(vm.IsRdpResolutionVisible);
    }

    [Fact]
    public void IsRdpResolutionVisible_true_when_windowed()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp, RdpFullscreen = false };
        Assert.True(vm.IsRdpResolutionVisible);
    }

    [Fact]
    public void IsRdpCustomResolutionVisible_true_only_for_custom_preset_and_not_fullscreen()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.SelectedRdpPreset = RdpResolutionPreset.Custom;
        Assert.True(vm.IsRdpCustomResolutionVisible);

        vm.RdpFullscreen = true;
        Assert.False(vm.IsRdpCustomResolutionVisible);
    }

    [Fact]
    public void Selecting_preset_updates_width_and_height()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        var preset = ProfileEditorViewModel.RdpPresets.First(p => p.Width == 1280 && p.Height == 720);
        vm.SelectedRdpPreset = preset;

        Assert.Equal(1280, vm.RdpDesktopWidth);
        Assert.Equal(720, vm.RdpDesktopHeight);
    }

    [Fact]
    public void Selecting_custom_preset_does_not_change_width_and_height()
    {
        var vm = new ProfileEditorViewModel { Protocol = ProtocolType.Rdp };
        vm.RdpDesktopWidth = 1600;
        vm.RdpDesktopHeight = 900;
        vm.SelectedRdpPreset = RdpResolutionPreset.Custom;

        Assert.Equal(1600, vm.RdpDesktopWidth);
        Assert.Equal(900, vm.RdpDesktopHeight);
    }

    [Fact]
    public void BuildProfile_RDP_reflects_fullscreen_and_resolution()
    {
        var vm = MakeValidVm(ProtocolType.Rdp);
        vm.RdpFullscreen = true;

        var profile = (RdpConnectionProfile)vm.BuildProfile();

        Assert.True(profile.Fullscreen);
    }

    [Fact]
    public void BuildProfile_RDP_windowed_stores_chosen_resolution()
    {
        var vm = MakeValidVm(ProtocolType.Rdp);
        vm.RdpFullscreen = false;
        vm.SelectedRdpPreset = RdpResolutionPreset.Custom;
        vm.RdpDesktopWidth = 1366;
        vm.RdpDesktopHeight = 768;

        var profile = (RdpConnectionProfile)vm.BuildProfile();

        Assert.False(profile.Fullscreen);
        Assert.Equal(1366, profile.DesktopWidth);
        Assert.Equal(768, profile.DesktopHeight);
    }

    [Fact]
    public void Constructor_with_RDP_profile_populates_display_settings()
    {
        var profile = new RdpConnectionProfile
        {
            Name = "VDI", Host = "vdi.corp.local", Username = "user",
            Fullscreen = true, DesktopWidth = 1920, DesktopHeight = 1080,
        };

        var vm = new ProfileEditorViewModel(profile);

        Assert.True(vm.RdpFullscreen);
        Assert.Equal(1920, vm.RdpDesktopWidth);
        Assert.Equal(1080, vm.RdpDesktopHeight);
    }

    [Fact]
    public void Constructor_with_RDP_non_preset_resolution_selects_custom()
    {
        var profile = new RdpConnectionProfile
        {
            Name = "VDI", Host = "vdi.corp.local", Username = "user",
            DesktopWidth = 1440, DesktopHeight = 810,
        };

        var vm = new ProfileEditorViewModel(profile);

        Assert.True(vm.SelectedRdpPreset?.IsCustom);
        Assert.Equal(1440, vm.RdpDesktopWidth);
        Assert.Equal(810, vm.RdpDesktopHeight);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ProfileEditorViewModel MakeValidVm(ProtocolType protocol) =>
        new ProfileEditorViewModel
        {
            Protocol = protocol,
            Name = "Test Server",
            Host = "test",
            Port = 22,
            Username = protocol == ProtocolType.Telnet ? string.Empty : "user",
        };
}

public sealed class ProfileEditorCredentialTests
{
    [Fact]
    public void Editing_existing_profile_keeps_its_id_so_saved_password_stays_linked()
    {
        var existing = new OmniRemote.Core.Models.SshConnectionProfile { Name = "a", Host = "h", Username = "u" };
        var editor = new OmniRemote.App.ViewModels.ProfileEditorViewModel(existing);

        Assert.Equal(existing.Id, editor.BuildProfile().Id);
    }

    [Fact]
    public void Password_field_hidden_only_for_telnet()
    {
        var editor = new OmniRemote.App.ViewModels.ProfileEditorViewModel
        {
            Protocol = OmniRemote.Core.Models.ProtocolType.Telnet,
        };
        Assert.False(editor.IsPasswordVisible);
        editor.Protocol = OmniRemote.Core.Models.ProtocolType.Rdp;
        Assert.True(editor.IsPasswordVisible);
    }
}
