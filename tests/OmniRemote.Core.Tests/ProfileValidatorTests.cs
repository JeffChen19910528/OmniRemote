using OmniRemote.Core.Models;
using OmniRemote.Core.Validation;

namespace OmniRemote.Core.Tests;

public sealed class ProfileValidatorTests
{
    private readonly ProfileValidator _sut = new();

    // ── Base profile rules ────────────────────────────────────────────────

    [Fact]
    public void Valid_ssh_profile_passes()
    {
        var result = _sut.Validate(ValidSsh());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_fails(string name)
    {
        var p = ValidSsh(); p.Name = name;
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_host_fails(string host)
    {
        var p = ValidSsh(); p.Host = host;
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Host"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(99999)]
    public void Out_of_range_port_fails(int port)
    {
        var p = ValidSsh(); p.Port = port;
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Port"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(65535)]
    public void Valid_port_range_passes(int port)
    {
        var p = ValidSsh(); p.Port = port;
        Assert.True(_sut.Validate(p).IsValid);
    }

    // ── SSH-specific rules ────────────────────────────────────────────────

    [Fact]
    public void Ssh_empty_username_fails()
    {
        var p = ValidSsh(); p.Username = "";
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Username"));
    }

    [Fact]
    public void Ssh_private_key_auth_without_path_fails()
    {
        var p = ValidSsh();
        p.AuthMethod = SshAuthMethod.PrivateKey;
        p.PrivateKeyPath = null;
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PrivateKeyPath"));
    }

    [Fact]
    public void Ssh_private_key_auth_with_path_passes()
    {
        var p = ValidSsh();
        p.AuthMethod = SshAuthMethod.PrivateKey;
        p.PrivateKeyPath = "/home/user/.ssh/id_rsa";
        Assert.True(_sut.Validate(p).IsValid);
    }

    [Fact]
    public void Ssh_negative_connect_timeout_fails()
    {
        var p = ValidSsh(); p.ConnectTimeout = TimeSpan.FromSeconds(-1);
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ConnectTimeout"));
    }

    // ── SFTP-specific rules ───────────────────────────────────────────────

    [Fact]
    public void Valid_sftp_profile_passes()
    {
        Assert.True(_sut.Validate(ValidSftp()).IsValid);
    }

    [Fact]
    public void Sftp_empty_username_fails()
    {
        var p = ValidSftp(); p.Username = "  ";
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Username"));
    }

    // ── Telnet-specific rules ─────────────────────────────────────────────

    [Fact]
    public void Valid_telnet_profile_passes()
    {
        Assert.True(_sut.Validate(ValidTelnet()).IsValid);
    }

    // ── RDP-specific rules ────────────────────────────────────────────────

    [Fact]
    public void Valid_rdp_profile_passes()
    {
        Assert.True(_sut.Validate(ValidRdp()).IsValid);
    }

    [Fact]
    public void Rdp_empty_username_fails()
    {
        var p = ValidRdp(); p.Username = "";
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Username"));
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(1280, 0)]
    [InlineData(-1, 600)]
    public void Rdp_invalid_resolution_fails(int w, int h)
    {
        var p = ValidRdp(); p.DesktopWidth = w; p.DesktopHeight = h;
        Assert.False(_sut.Validate(p).IsValid);
    }

    // ── Multiple errors ───────────────────────────────────────────────────

    [Fact]
    public void Multiple_invalid_fields_return_all_errors()
    {
        var p = ValidSsh();
        p.Name = "";
        p.Host = "";
        p.Port = 0;
        p.Username = "";
        var result = _sut.Validate(p);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 4);
    }

    // ── Security: bypass default ──────────────────────────────────────────

    [Fact]
    public void Certificate_bypass_is_false_by_default()
    {
        Assert.False(ValidSsh().BypassCertificateValidation);
        Assert.False(ValidSftp().BypassCertificateValidation);
        Assert.False(ValidTelnet().BypassCertificateValidation);
        Assert.False(ValidRdp().BypassCertificateValidation);
    }

    [Fact]
    public void Strict_host_key_checking_is_true_by_default()
    {
        Assert.True(ValidSsh().StrictHostKeyChecking);
        Assert.True(ValidSftp().StrictHostKeyChecking);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static SshConnectionProfile ValidSsh() => new()
    {
        Name = "My SSH Server",
        Host = "192.168.1.10",
        Username = "admin",
    };

    private static SftpConnectionProfile ValidSftp() => new()
    {
        Name = "My SFTP Server",
        Host = "192.168.1.10",
        Username = "admin",
    };

    private static TelnetConnectionProfile ValidTelnet() => new()
    {
        Name = "Legacy Router",
        Host = "10.0.0.1",
    };

    private static RdpConnectionProfile ValidRdp() => new()
    {
        Name = "Windows VM",
        Host = "10.0.0.50",
        Username = "Administrator",
    };
}
