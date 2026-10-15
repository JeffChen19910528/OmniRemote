using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.Services;

/// <summary>
/// Concrete <see cref="IAdapterRegistry"/>. Selects the first registered adapter that
/// can handle the profile's protocol and delegates session creation to it.
/// Registered as a singleton so adapter state is shared across all sessions.
/// </summary>
public sealed class AdapterRegistry : IAdapterRegistry
{
    private readonly IReadOnlyList<IRemoteProtocolAdapter> _adapters;

    public AdapterRegistry(IEnumerable<IRemoteProtocolAdapter> adapters) =>
        _adapters = adapters.ToList();

    public bool CanHandle(ConnectionProfile profile) =>
        _adapters.Any(a => a.CanHandle(profile));

    public Task<IRemoteSession> CreateSessionAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapters.FirstOrDefault(a => a.CanHandle(profile))
            ?? throw new InvalidOperationException(
                $"No adapter registered for protocol '{profile.Protocol}'. " +
                "Ensure the protocol package is wired up in the DI container.");

        return adapter.CreateSessionAsync(profile, cancellationToken);
    }
}
