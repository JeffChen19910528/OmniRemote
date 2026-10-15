using OmniRemote.Core.Models;

namespace OmniRemote.Protocols.Abstractions;

public interface IRemoteProtocolAdapter
{
    ProtocolType SupportedProtocol { get; }
    bool CanHandle(ConnectionProfile profile);
    Task<IRemoteSession> CreateSessionAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
}
