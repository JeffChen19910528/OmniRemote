using OmniRemote.Core.Models;

namespace OmniRemote.Protocols.Abstractions;

public interface ICertificateValidationPolicy
{
    /// <summary>
    /// Returns true when the certificate is valid OR when the profile has explicit per-profile bypass enabled.
    /// Never disables validation globally.
    /// </summary>
    bool Validate(ConnectionProfile profile, object certificate, bool chainValid);
}
