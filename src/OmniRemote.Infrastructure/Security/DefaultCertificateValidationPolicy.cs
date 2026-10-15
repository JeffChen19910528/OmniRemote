using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Security;

public sealed class DefaultCertificateValidationPolicy : ICertificateValidationPolicy
{
    private readonly ILogger<DefaultCertificateValidationPolicy> _logger;

    public DefaultCertificateValidationPolicy(ILogger<DefaultCertificateValidationPolicy> logger) =>
        _logger = logger;

    public bool Validate(ConnectionProfile profile, object certificate, bool chainValid)
    {
        if (chainValid) return true;

        if (profile.BypassCertificateValidation)
        {
            _logger.LogWarning(
                "Certificate validation bypassed for {Host} ({Protocol}) — per-profile bypass is enabled",
                profile.Host, profile.Protocol);
            return true;
        }

        _logger.LogError(
            "Certificate validation failed for {Host} ({Protocol}) and bypass is not enabled",
            profile.Host, profile.Protocol);
        return false;
    }
}
