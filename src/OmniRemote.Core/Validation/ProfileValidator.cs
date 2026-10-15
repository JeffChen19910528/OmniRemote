using OmniRemote.Core.Models;

namespace OmniRemote.Core.Validation;

public sealed class ProfileValidator
{
    public ProfileValidationResult Validate(ConnectionProfile profile)
    {
        var errors = new List<string>();

        ValidateBase(profile, errors);

        switch (profile)
        {
            case SshConnectionProfile ssh:   ValidateSsh(ssh, errors);   break;
            case SftpConnectionProfile sftp: ValidateSftp(sftp, errors); break;
            case RdpConnectionProfile rdp:   ValidateRdp(rdp, errors);   break;
            case TelnetConnectionProfile:    break;
        }

        return errors.Count == 0 ? ProfileValidationResult.Valid : ProfileValidationResult.Fail(errors);
    }

    private static void ValidateBase(ConnectionProfile p, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
            errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(p.Host))
            errors.Add("Host is required.");
        if (p.Port is < 1 or > 65535)
            errors.Add($"Port must be between 1 and 65535 (got {p.Port}).");
    }

    private static void ValidateSsh(SshConnectionProfile p, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(p.Username))
            errors.Add("Username is required for SSH.");
        if (p.AuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(p.PrivateKeyPath))
            errors.Add("PrivateKeyPath is required when AuthMethod is PrivateKey.");
        if (p.ConnectTimeout <= TimeSpan.Zero)
            errors.Add("ConnectTimeout must be positive.");
    }

    private static void ValidateSftp(SftpConnectionProfile p, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(p.Username))
            errors.Add("Username is required for SFTP.");
        if (p.AuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(p.PrivateKeyPath))
            errors.Add("PrivateKeyPath is required when AuthMethod is PrivateKey.");
    }

    private static void ValidateRdp(RdpConnectionProfile p, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(p.Username))
            errors.Add("Username is required for RDP.");
        if (p.DesktopWidth < 1)
            errors.Add("DesktopWidth must be positive.");
        if (p.DesktopHeight < 1)
            errors.Add("DesktopHeight must be positive.");
    }
}
