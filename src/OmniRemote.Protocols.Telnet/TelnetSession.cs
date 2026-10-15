using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System.Net.Sockets;

namespace OmniRemote.Protocols.Telnet;

/// <summary>
/// Telnet session. Always displays an unencrypted-traffic warning.
/// Never present Telnet as suitable for transmitting sensitive credentials.
/// </summary>
public sealed class TelnetSession : ITerminalSession
{
    /// <summary>
    /// Persistent warning text that must be shown to the user at all times while a Telnet
    /// session is active. Telnet transmits all data, including passwords, in plaintext.
    /// </summary>
    public const string UnencryptedWarning =
        "WARNING: Telnet transmits all data including credentials in plaintext. " +
        "Use only on trusted, isolated networks.";

    private readonly TelnetConnectionProfile _profile;
    private readonly ILogger<TelnetSession> _logger;
    private TcpClient? _tcpClient;
    private TelnetStream? _telnetStream;
    private ConnectionState _state = ConnectionState.Disconnected;

    public Guid SessionId { get; } = Guid.NewGuid();
    public OmniRemote.Core.Models.ProtocolType Protocol => OmniRemote.Core.Models.ProtocolType.Telnet;
    public string? SecurityWarning => UnencryptedWarning;
    public string DisplayName => $"Telnet — {_profile.Host}:{_profile.Port} [UNENCRYPTED]";

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// Not applicable for Telnet. Always null — Telnet has no host-key authentication.
    /// </summary>
    public Func<HostKeyVerificationRequest, CancellationToken, Task<bool>>? HostKeyVerificationCallback
    {
        get => null;
        set { /* Telnet does not perform host-key verification */ }
    }

    /// <summary>Write bytes here to send input to the remote host.</summary>
    public Stream TerminalInput => _telnetStream
        ?? throw new InvalidOperationException("Telnet session is not connected. Call ConnectAsync first.");

    /// <summary>Read bytes from here to receive output from the remote host.</summary>
    public Stream TerminalOutput => _telnetStream
        ?? throw new InvalidOperationException("Telnet session is not connected. Call ConnectAsync first.");

    public TelnetSession(TelnetConnectionProfile profile, ILogger<TelnetSession> logger)
    {
        _profile = profile;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(UnencryptedWarning);
        State = ConnectionState.Connecting;

        try
        {
            _tcpClient = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_profile.ConnectTimeout);

            await _tcpClient.ConnectAsync(_profile.Host, _profile.Port, cts.Token);

            var networkStream = _tcpClient.GetStream();
            _telnetStream = new TelnetStream(networkStream, _logger);

            State = ConnectionState.Connected;
            _logger.LogInformation(
                "Telnet session {SessionId} connected to {Host}:{Port}",
                SessionId, _profile.Host, _profile.Port);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Failed;
            _logger.LogError(
                "Telnet connection failed for {Host}:{Port}: {Message}",
                _profile.Host, _profile.Port, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Notifies the remote host of a terminal resize via Telnet NAWS (RFC 1073).
    /// No-op if NAWS was not negotiated or session is not connected.
    /// </summary>
    public void SetTerminalSize(uint columns, uint rows)
    {
        _telnetStream?.SendWindowSize(columns, rows);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _telnetStream?.Close();
        _telnetStream = null;
        _tcpClient?.Close();
        _tcpClient = null;
        State = ConnectionState.Disconnected;
        _logger.LogInformation("Telnet session {SessionId} disconnected", SessionId);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _telnetStream?.Dispose();
        _telnetStream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        await Task.CompletedTask;
    }
}
