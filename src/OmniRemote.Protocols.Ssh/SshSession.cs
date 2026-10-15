using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using Renci.SshNet;
using System.Security.Cryptography;

namespace OmniRemote.Protocols.Ssh;

public sealed class SshSession : ITerminalSession
{
    private readonly SshConnectionProfile _profile;
    private readonly IHostIdentityPolicy _hostIdentityPolicy;
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILogger<SshSession> _logger;

    private SshClient? _client;
    private ShellStream? _shellStream;
    private ConnectionState _state = ConnectionState.Disconnected;

    public Guid SessionId { get; } = Guid.NewGuid();
    public ProtocolType Protocol => ProtocolType.Ssh;
    public string DisplayName => $"SSH — {_profile.Username}@{_profile.Host}:{_profile.Port}";

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

    /// <inheritdoc/>
    public Func<HostKeyVerificationRequest, CancellationToken, Task<bool>>? HostKeyVerificationCallback { get; set; }

    /// <summary>Write terminal input here (bytes go to the remote shell).</summary>
    public Stream TerminalInput => _shellStream
        ?? throw new InvalidOperationException("Terminal is not open. Call ConnectAsync first.");

    /// <summary>Read terminal output from here (bytes arrive from the remote shell).</summary>
    public Stream TerminalOutput => _shellStream
        ?? throw new InvalidOperationException("Terminal is not open. Call ConnectAsync first.");

    public SshSession(
        SshConnectionProfile profile,
        IHostIdentityPolicy hostIdentityPolicy,
        ICredentialStore credentialStore,
        ISessionLogRedactor redactor,
        ILogger<SshSession> logger)
    {
        _profile = profile;
        _hostIdentityPolicy = hostIdentityPolicy;
        _credentialStore = credentialStore;
        _redactor = redactor;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectionState.Connecting;
        _logger.LogInformation("Connecting to SSH host {Host}:{Port}", _profile.Host, _profile.Port);

        try
        {
            var authMethod = await BuildAuthMethodAsync(cancellationToken);
            var connectionInfo = new ConnectionInfo(_profile.Host, _profile.Port, _profile.Username, authMethod)
            {
                Timeout = _profile.ConnectTimeout,
            };

            _client = new SshClient(connectionInfo);

            // SSH.NET fires HostKeyReceived synchronously during Connect().
            // We use .GetAwaiter().GetResult() to block the connection thread
            // (which is a background thread in Task.Run) while awaiting the policy
            // check and the optional user callback. The UI thread remains free.
            _client.HostKeyReceived += (_, args) =>
                HandleHostKeyReceived(args, cancellationToken);

            await Task.Run(() => _client.Connect(), cancellationToken);

            // Open the interactive shell stream
            _shellStream = _client.CreateShellStream(
                terminalName: "xterm-256color",
                columns: 80,
                rows: 24,
                width: 0,
                height: 0,
                bufferSize: 4096);

            State = ConnectionState.Connected;
            _logger.LogInformation("SSH session {SessionId} connected to {Host}", SessionId, _profile.Host);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Failed;
            _logger.LogError("SSH connection failed for {Host}: {Message}",
                _profile.Host, _redactor.Redact(ex.Message));
            throw;
        }
    }

    private void HandleHostKeyReceived(Renci.SshNet.Common.HostKeyEventArgs args, CancellationToken ct)
    {
        if (!_profile.StrictHostKeyChecking)
        {
            args.CanTrust = true;
            return;
        }

        var result = _hostIdentityPolicy
            .CheckHostKeyAsync(_profile.Host, _profile.Port, args.HostKeyName, args.HostKey, ct)
            .GetAwaiter().GetResult();

        switch (result)
        {
            case HostKeyTrustResult.Trusted:
                args.CanTrust = true;
                return;

            case HostKeyTrustResult.Changed:
                // A changed key is always rejected — could indicate a MITM attack.
                var fp = FingerprintString(args.HostKey);
                _logger.LogWarning(
                    "Host key CHANGED for {Host}:{Port} ({Type}) — fingerprint {FP}. Connection rejected.",
                    _profile.Host, _profile.Port, args.HostKeyName, fp);
                args.CanTrust = false;
                return;

            case HostKeyTrustResult.Unknown:
                HandleUnknownHostKey(args, ct);
                return;
        }
    }

    private void HandleUnknownHostKey(Renci.SshNet.Common.HostKeyEventArgs args, CancellationToken ct)
    {
        var fp = FingerprintString(args.HostKey);
        _logger.LogWarning(
            "Unknown host key for {Host}:{Port} ({Type}) fingerprint {FP}",
            _profile.Host, _profile.Port, args.HostKeyName, fp);

        if (HostKeyVerificationCallback is null)
        {
            _logger.LogWarning(
                "No HostKeyVerificationCallback set — rejecting unknown key for {Host}:{Port}",
                _profile.Host, _profile.Port);
            args.CanTrust = false;
            return;
        }

        var request = new HostKeyVerificationRequest(
            _profile.Host, _profile.Port, args.HostKeyName, fp, HostKeyTrustResult.Unknown);

        var accepted = HostKeyVerificationCallback(request, ct).GetAwaiter().GetResult();

        args.CanTrust = accepted;

        if (accepted)
        {
            _hostIdentityPolicy
                .TrustHostKeyAsync(_profile.Host, _profile.Port, args.HostKeyName, args.HostKey, ct)
                .GetAwaiter().GetResult();
            _logger.LogInformation(
                "User accepted and persisted host key for {Host}:{Port} ({Type})",
                _profile.Host, _profile.Port, args.HostKeyName);
        }
        else
        {
            _logger.LogInformation(
                "User rejected unknown host key for {Host}:{Port} — connection aborted",
                _profile.Host, _profile.Port);
        }
    }

    public void SetTerminalSize(uint columns, uint rows)
    {
        _shellStream?.ChangeWindowSize(columns, rows, 0, 0);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _shellStream?.Close();
        _shellStream = null;
        _client?.Disconnect();
        State = ConnectionState.Disconnected;
        _logger.LogInformation("SSH session {SessionId} disconnected", SessionId);
        return Task.CompletedTask;
    }

    private async Task<AuthenticationMethod> BuildAuthMethodAsync(CancellationToken cancellationToken)
    {
        return _profile.AuthMethod switch
        {
            SshAuthMethod.PrivateKey => BuildPrivateKeyAuth(),
            _ => await BuildPasswordAuthAsync(cancellationToken),
        };
    }

    private async Task<AuthenticationMethod> BuildPasswordAuthAsync(CancellationToken cancellationToken)
    {
        var credKey = $"ssh:{_profile.Id}";
        var password = await _credentialStore.LoadAsync(credKey, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No password stored for profile '{_profile.Name}'. Please re-enter credentials.");
        return new PasswordAuthenticationMethod(_profile.Username, password);
    }

    private AuthenticationMethod BuildPrivateKeyAuth()
    {
        if (string.IsNullOrWhiteSpace(_profile.PrivateKeyPath))
            throw new InvalidOperationException(
                "PrivateKeyPath is required for private-key authentication.");
        var keyFile = new PrivateKeyFile(_profile.PrivateKeyPath);
        return new PrivateKeyAuthenticationMethod(_profile.Username, keyFile);
    }

    private static string FingerprintString(byte[] keyData)
    {
        var hash = SHA256.HashData(keyData);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
    }

    public async ValueTask DisposeAsync()
    {
        _shellStream?.Dispose();
        _shellStream = null;
        if (_client is not null)
        {
            if (_client.IsConnected) _client.Disconnect();
            _client.Dispose();
            _client = null;
        }
        await Task.CompletedTask;
    }
}
