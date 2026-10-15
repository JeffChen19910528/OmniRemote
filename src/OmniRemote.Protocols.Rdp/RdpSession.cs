using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System.Diagnostics;

namespace OmniRemote.Protocols.Rdp;

/// <summary>
/// Launches an RDP backend process: bundled/system FreeRDP, or mstsc as a Windows fallback.
/// Arguments are built via ArgumentList — never via string concatenation of user input.
/// </summary>
public sealed class RdpSession : IRemoteSession
{
    // If the backend process exits within this window after launch, the connection failed.
    private static readonly TimeSpan QuickExitThreshold = TimeSpan.FromSeconds(2);

    private readonly RdpConnectionProfile _profile;
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILogger<RdpSession> _logger;
    private readonly IRdpBackendLocator _backendLocator;
    private readonly IProcessLauncher _processLauncher;
    private Process? _rdpProcess;
    private ConnectionState _state = ConnectionState.Disconnected;

    public Guid SessionId { get; } = Guid.NewGuid();
    public ProtocolType Protocol => ProtocolType.Rdp;
    public string DisplayName => $"RDP — {_profile.Username}@{_profile.Host}:{_profile.Port}";

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

    public RdpSession(
        RdpConnectionProfile profile,
        ICredentialStore credentialStore,
        ISessionLogRedactor redactor,
        ILogger<RdpSession> logger)
        : this(profile, credentialStore, redactor, logger,
               new RdpBackendLocator(), new DefaultProcessLauncher()) { }

    // Internal constructor for test injection.
    internal RdpSession(
        RdpConnectionProfile profile,
        ICredentialStore credentialStore,
        ISessionLogRedactor redactor,
        ILogger<RdpSession> logger,
        IRdpBackendLocator backendLocator,
        IProcessLauncher processLauncher)
    {
        _profile = profile;
        _credentialStore = credentialStore;
        _redactor = redactor;
        _logger = logger;
        _backendLocator = backendLocator;
        _processLauncher = processLauncher;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var backend = _backendLocator.FindBackend();
        if (backend is null)
        {
            State = ConnectionState.Failed;
            throw new InvalidOperationException(
                "No RDP backend found. OmniRemote normally ships FreeRDP in the 'rdp' folder " +
                "next to the executable; re-install the packaged build, or install FreeRDP " +
                "yourself (xfreerdp, e.g. apt install freerdp3-x11 / brew install freerdp).");
        }

        State = ConnectionState.Connecting;
        _logger.LogInformation(
            "Launching RDP backend for session {SessionId}: {Host}:{Port}",
            SessionId, _profile.Host, _profile.Port);

        try
        {
            var password = await _credentialStore.LoadAsync($"rdp:{_profile.Id}", cancellationToken);

            var psi = new ProcessStartInfo(backend)
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            if (IsMstsc(backend))
                BuildMstscArguments(psi, password);
            else
                BuildArguments(psi, password);

            _rdpProcess = _processLauncher.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null for RDP backend.");

            _rdpProcess.EnableRaisingEvents = true;
            _rdpProcess.Exited += OnProcessExited;

            // Wait briefly. A very quick exit almost always means the backend reported an error
            // (wrong path, auth failure, missing server). Treat it as a failed connection.
            bool exited = await _rdpProcess.WaitForExitAsync(cancellationToken)
                                           .WaitAsync(QuickExitThreshold, cancellationToken)
                                           .ContinueWith(t => t.IsCompletedSuccessfully, cancellationToken);

            if (exited)
            {
                int exitCode = _rdpProcess.ExitCode;
                State = ConnectionState.Failed;
                throw new InvalidOperationException(
                    $"RDP backend exited immediately (code {exitCode}). " +
                    "Check host address, credentials, and that the remote host accepts RDP connections.");
            }

            State = ConnectionState.Connected;
            _logger.LogInformation("RDP session {SessionId} is running", SessionId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException { Message: var m } || !m.Contains("exited immediately"))
        {
            // Don't double-wrap our own "exited immediately" exception.
            State = ConnectionState.Failed;
            _logger.LogError(
                "RDP session {SessionId} failed to launch: {Message}",
                SessionId, _redactor.Redact(ex.Message));
            throw;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        CleanupMstscArtifacts();
        if (_state == ConnectionState.Connected)
        {
            State = ConnectionState.Disconnected;
            _logger.LogInformation("RDP session {SessionId} closed", SessionId);
        }
    }

    private void BuildArguments(ProcessStartInfo psi, string? password)
    {
        // All arguments are added via ArgumentList — never via string concatenation.
        // This prevents command-line injection regardless of what the user typed.
        psi.ArgumentList.Add($"/v:{_profile.Host}:{_profile.Port}");
        psi.ArgumentList.Add($"/u:{_profile.Username}");

        if (!string.IsNullOrWhiteSpace(_profile.Domain))
            psi.ArgumentList.Add($"/d:{_profile.Domain}");

        psi.ArgumentList.Add($"/w:{_profile.DesktopWidth}");
        psi.ArgumentList.Add($"/h:{_profile.DesktopHeight}");

        if (_profile.BypassCertificateValidation)
        {
            _logger.LogWarning(
                "RDP certificate validation bypassed for {Host} (per-profile setting)",
                _profile.Host);
            psi.ArgumentList.Add("/cert:ignore");
        }

        if (!string.IsNullOrWhiteSpace(password))
            psi.ArgumentList.Add($"/p:{password}");
        // Note: /p: is visible in the OS process list. This is a known FreeRDP limitation.
    }

    private static bool IsMstsc(string backend) =>
        string.Equals(Path.GetFileNameWithoutExtension(backend), "mstsc", StringComparison.OrdinalIgnoreCase);

    private string? _rdpFilePath;
    private string? _cmdkeyTarget;

    /// <summary>
    /// mstsc has no FreeRDP-style switches: it takes a .rdp file, and saved credentials come
    /// from Windows Credential Manager (TERMSRV/host), which we populate with cmdkey for the
    /// lifetime of the session.
    /// </summary>
    private void BuildMstscArguments(ProcessStartInfo psi, string? password)
    {
        RejectControlCharacters(_profile.Host, "host");
        RejectControlCharacters(_profile.Username, "username");
        RejectControlCharacters(_profile.Domain, "domain");

        var user = string.IsNullOrWhiteSpace(_profile.Domain)
            ? _profile.Username
            : _profile.Domain + "\\" + _profile.Username;
        var hasPassword = !string.IsNullOrEmpty(password);

        var lines = new List<string>
        {
            $"full address:s:{_profile.Host}:{_profile.Port}",
            $"username:s:{user}",
            $"desktopwidth:i:{_profile.DesktopWidth}",
            $"desktopheight:i:{_profile.DesktopHeight}",
            "screen mode id:i:1",
            $"authentication level:i:{(_profile.BypassCertificateValidation ? 0 : 2)}",
            $"prompt for credentials:i:{(hasPassword ? 0 : 1)}",
        };
        if (_profile.BypassCertificateValidation)
            _logger.LogWarning("RDP certificate validation bypassed for {Host} (per-profile setting)", _profile.Host);

        _rdpFilePath = Path.Combine(Path.GetTempPath(), $"omniremote_{SessionId:N}.rdp");
        File.WriteAllLines(_rdpFilePath, lines);
        psi.ArgumentList.Add(_rdpFilePath);

        if (hasPassword)
        {
            _cmdkeyTarget = _profile.Host;
            RunCmdkey("/generic:TERMSRV/" + _cmdkeyTarget, "/user:" + user, "/pass:" + password);
        }
    }

    private static void RejectControlCharacters(string? value, string field)
    {
        if (value is not null && value.Any(char.IsControl))
            throw new InvalidOperationException($"Invalid characters in RDP {field}.");
    }

    private static void RunCmdkey(params string[] args)
    {
        var psi = new ProcessStartInfo("cmdkey.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p?.WaitForExit(5000);
    }

    private void CleanupMstscArtifacts()
    {
        try
        {
            if (_cmdkeyTarget is not null)
            {
                RunCmdkey("/delete:TERMSRV/" + _cmdkeyTarget);
                _cmdkeyTarget = null;
            }
            if (_rdpFilePath is not null)
            {
                File.Delete(_rdpFilePath);
                _rdpFilePath = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("RDP cleanup failed: {Message}", _redactor.Redact(ex.Message));
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_rdpProcess is { HasExited: false })
        {
            _rdpProcess.Kill();
            _logger.LogInformation("RDP session {SessionId} killed", SessionId);
        }
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_rdpProcess is { HasExited: false })
            _rdpProcess.Kill();
        _rdpProcess?.Dispose();
        _rdpProcess = null;
        CleanupMstscArtifacts();
        await Task.CompletedTask;
    }
}
