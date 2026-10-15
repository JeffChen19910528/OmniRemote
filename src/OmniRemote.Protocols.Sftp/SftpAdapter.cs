using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Protocols.Sftp;

public sealed class SftpAdapter : IRemoteProtocolAdapter
{
    private readonly IHostIdentityPolicy _hostIdentityPolicy;
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolType SupportedProtocol => ProtocolType.Sftp;

    public SftpAdapter(
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

    public bool CanHandle(ConnectionProfile profile) => profile is SftpConnectionProfile;

    public Task<IRemoteSession> CreateSessionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile is not SftpConnectionProfile sftp)
            throw new ArgumentException($"Expected {nameof(SftpConnectionProfile)}", nameof(profile));

        ISftpSession session = new SftpSession(
            sftp,
            _hostIdentityPolicy,
            _credentialStore,
            _redactor,
            _loggerFactory.CreateLogger<SftpSession>());

        return Task.FromResult<IRemoteSession>(session);
    }
}
