using OmniRemote.Core.Models;

namespace OmniRemote.Protocols.Abstractions;

public interface IRemoteSession : IAsyncDisposable
{
    Guid SessionId { get; }
    ConnectionState State { get; }
    ProtocolType Protocol { get; }
    string DisplayName { get; }

    event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// Non-null only for sessions that carry a persistent security caveat (e.g. Telnet).
    /// The UI must display this text prominently for as long as the session is open.
    /// </summary>
    string? SecurityWarning => null;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
