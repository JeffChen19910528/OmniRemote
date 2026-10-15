using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Protocols.Telnet;

public sealed class TelnetAdapter : IRemoteProtocolAdapter
{
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolType SupportedProtocol => ProtocolType.Telnet;

    public TelnetAdapter(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public bool CanHandle(ConnectionProfile profile) => profile is TelnetConnectionProfile;

    public Task<IRemoteSession> CreateSessionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile is not TelnetConnectionProfile telnet)
            throw new ArgumentException($"Expected {nameof(TelnetConnectionProfile)}", nameof(profile));

        ITerminalSession session = new TelnetSession(telnet, _loggerFactory.CreateLogger<TelnetSession>());
        return Task.FromResult<IRemoteSession>(session);
    }
}
