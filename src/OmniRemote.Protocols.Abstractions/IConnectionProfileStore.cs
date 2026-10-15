using OmniRemote.Core.Models;

namespace OmniRemote.Protocols.Abstractions;

public interface IConnectionProfileStore
{
    Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ConnectionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
