using OmniRemote.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OmniRemote.Protocols.Abstractions;

/// <summary>
/// Selects and delegates to the correct <see cref="IRemoteProtocolAdapter"/> for a given profile.
/// Callers (e.g. ViewModels) depend on this interface rather than the full adapter list,
/// keeping them isolated from protocol implementation details.
/// </summary>
public interface IAdapterRegistry
{
    bool CanHandle(ConnectionProfile profile);

    Task<IRemoteSession> CreateSessionAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default);
}
