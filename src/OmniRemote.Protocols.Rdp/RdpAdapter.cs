using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Protocols.Rdp;

public sealed class RdpAdapter : IRemoteProtocolAdapter
{
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolType SupportedProtocol => ProtocolType.Rdp;

    public RdpAdapter(ICredentialStore credentialStore, ISessionLogRedactor redactor, ILoggerFactory loggerFactory)
    {
        _credentialStore = credentialStore;
        _redactor = redactor;
        _loggerFactory = loggerFactory;
    }

    public bool CanHandle(ConnectionProfile profile) => profile is RdpConnectionProfile;

    public Task<IRemoteSession> CreateSessionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile is not RdpConnectionProfile rdp)
            throw new ArgumentException($"Expected {nameof(RdpConnectionProfile)}", nameof(profile));

        IRemoteSession session = new RdpSession(
            rdp, _credentialStore, _redactor, _loggerFactory.CreateLogger<RdpSession>());
        return Task.FromResult(session);
    }
}
