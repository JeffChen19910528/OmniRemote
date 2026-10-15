using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Protocols.Ssh;

public sealed class SshAdapter : IRemoteProtocolAdapter
{
    private readonly IHostIdentityPolicy _hostIdentityPolicy;
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolType SupportedProtocol => ProtocolType.Ssh;

    public SshAdapter(
        IHostIdentityPolicy hostIdentityPolicy,
        ICredentialStore credentialStore,
        ISessionLogRedactor redactor,
        ILoggerFactory loggerFactory)
    {
        _hostIdentityPolicy = hostIdentityPolicy;
        _credentialStore = credentialStore;
        _redactor = redactor;
        _loggerFactory = loggerFactory;
    }

    public bool CanHandle(ConnectionProfile profile) => profile is SshConnectionProfile;

    public Task<IRemoteSession> CreateSessionAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile is not SshConnectionProfile ssh)
            throw new ArgumentException($"Expected {nameof(SshConnectionProfile)}", nameof(profile));

        IRemoteSession session = new SshSession(
            ssh,
            _hostIdentityPolicy,
            _credentialStore,
            _redactor,
            _loggerFactory.CreateLogger<SshSession>());

        return Task.FromResult(session);
    }
}
