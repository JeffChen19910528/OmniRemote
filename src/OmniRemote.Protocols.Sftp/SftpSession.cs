using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using Renci.SshNet;

namespace OmniRemote.Protocols.Sftp;

public sealed class SftpSession : ISftpSession
{
    private readonly SftpConnectionProfile _profile;
    private readonly IHostIdentityPolicy _hostIdentityPolicy;
    private readonly ICredentialStore _credentialStore;
    private readonly ISessionLogRedactor _redactor;
    private readonly ILogger<SftpSession> _logger;
    private SftpClient? _client;
    private ConnectionState _state = ConnectionState.Disconnected;

    public Guid SessionId { get; } = Guid.NewGuid();
    public ProtocolType Protocol => ProtocolType.Sftp;
    public string DisplayName => $"SFTP — {_profile.Username}@{_profile.Host}:{_profile.Port}";

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

    public SftpSession(
        SftpConnectionProfile profile,
        IHostIdentityPolicy hostIdentityPolicy,
        ICredentialStore credentialStore,
        ISessionLogRedactor redactor,
        ILogger<SftpSession> logger)
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
        _logger.LogInformation("Connecting to SFTP host {Host}:{Port}", _profile.Host, _profile.Port);

        try
        {
            var password = await _credentialStore.LoadAsync($"sftp:{_profile.Id}", cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No password stored for profile '{_profile.Name}'. Please re-enter credentials.");

            var connectionInfo = new ConnectionInfo(_profile.Host, _profile.Port, _profile.Username,
                new PasswordAuthenticationMethod(_profile.Username, password))
            {
                Timeout = _profile.ConnectTimeout,
            };

            _client = new SftpClient(connectionInfo);

            if (_profile.StrictHostKeyChecking)
            {
                // SSH.NET fires HostKeyReceived synchronously on the connection thread (inside Connect()).
                // Use .GetAwaiter().GetResult() to safely bridge sync event → async policy check.
                _client.HostKeyReceived += (_, args) =>
                    HandleHostKeyReceived(args, cancellationToken);
            }

            await Task.Run(() => _client.Connect(), cancellationToken);
            State = ConnectionState.Connected;
            _logger.LogInformation("SFTP session {SessionId} connected to {Host}", SessionId, _profile.Host);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Failed;
            _logger.LogError("SFTP connection failed for {Host}: {Message}",
                _profile.Host, _redactor.Redact(ex.Message));
            throw;
        }
    }

    private void HandleHostKeyReceived(Renci.SshNet.Common.HostKeyEventArgs args, CancellationToken ct)
    {
        var result = _hostIdentityPolicy
            .CheckHostKeyAsync(_profile.Host, _profile.Port, args.HostKeyName, args.HostKey, ct)
            .GetAwaiter().GetResult();

        if (result == HostKeyTrustResult.Changed)
        {
            _logger.LogWarning(
                "Host key CHANGED for {Host}:{Port} — connection rejected.",
                _profile.Host, _profile.Port);
            args.CanTrust = false;
            return;
        }

        args.CanTrust = result == HostKeyTrustResult.Trusted;
    }

    public async Task<IReadOnlyList<SftpEntry>> ListDirectoryAsync(
        string remotePath, CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(remotePath);
        var client = RequireClient();

        _logger.LogDebug("Listing SFTP directory {Path}", remotePath);

        var files = new List<Renci.SshNet.Sftp.ISftpFile>();
        await foreach (var f in client.ListDirectoryAsync(remotePath, cancellationToken))
            files.Add(f);

        return files
            .Where(f => f.Name is not "." and not "..")
            .Select(f => new SftpEntry(
                Name: f.Name,
                FullPath: f.FullName,
                IsDirectory: f.IsDirectory,
                IsSymbolicLink: f.IsSymbolicLink,
                SizeBytes: f.Length,
                LastModified: new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero),
                Permissions: BuildPermissionsString(f)))
            .OrderBy(e => e.IsDirectory ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task UploadAsync(
        string localPath,
        string remotePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(remotePath);
        var client = RequireClient();

        if (!File.Exists(localPath))
            throw new FileNotFoundException("Local file not found.", localPath);

        var fileName = Path.GetFileName(localPath);
        var fileInfo = new FileInfo(localPath);
        var totalBytes = fileInfo.Length;

        _logger.LogInformation("Uploading {FileName} ({Bytes} bytes) to {RemotePath}",
            fileName, totalBytes, remotePath);

        IProgress<Renci.SshNet.UploadFileProgressReport>? sshProgress = null;
        if (progress is not null)
        {
            sshProgress = new Progress<Renci.SshNet.UploadFileProgressReport>(r =>
                progress.Report(new TransferProgress(fileName, (long)r.TotalBytesUploaded, totalBytes)));
        }

        await using var stream = File.OpenRead(localPath);
        await client.UploadFileAsync(stream, remotePath, sshProgress, cancellationToken);

        _logger.LogInformation("Upload complete: {FileName} → {RemotePath}", fileName, remotePath);
    }

    public async Task DownloadAsync(
        string remotePath,
        string localPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(remotePath);
        var client = RequireClient();

        var fileName = Path.GetFileName(remotePath);

        _logger.LogInformation("Downloading {RemotePath} to {LocalPath}", remotePath, localPath);

        long? totalBytes = null;
        try
        {
            var attrs = await client.GetAsync(remotePath, cancellationToken);
            totalBytes = attrs.Length;
        }
        catch
        {
            // Size unavailable — progress will report 0 total
        }

        IProgress<Renci.SshNet.DownloadFileProgressReport>? sshProgress = null;
        if (progress is not null)
        {
            sshProgress = new Progress<Renci.SshNet.DownloadFileProgressReport>(r =>
                progress.Report(new TransferProgress(fileName, (long)r.TotalBytesDownloaded, totalBytes ?? 0)));
        }

        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(localPath);
        await client.DownloadFileAsync(remotePath, stream, sshProgress, cancellationToken);

        _logger.LogInformation("Download complete: {RemotePath} → {LocalPath}", remotePath, localPath);
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(oldPath);
        ValidateRemotePath(newPath);
        var client = RequireClient();

        _logger.LogInformation("Renaming {OldPath} → {NewPath}", oldPath, newPath);
        await client.RenameFileAsync(oldPath, newPath, cancellationToken);
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(remotePath);
        var client = RequireClient();

        _logger.LogInformation("Creating directory {Path}", remotePath);
        await client.CreateDirectoryAsync(remotePath, cancellationToken);
    }

    public async Task DeleteAsync(string remotePath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        ValidateRemotePath(remotePath);
        var client = RequireClient();

        _logger.LogInformation("Deleting {Kind} {Path}", isDirectory ? "directory" : "file", remotePath);

        if (isDirectory)
            await client.DeleteDirectoryAsync(remotePath, cancellationToken);
        else
            await client.DeleteFileAsync(remotePath, cancellationToken);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _client?.Disconnect();
        State = ConnectionState.Disconnected;
        _logger.LogInformation("SFTP session {SessionId} disconnected", SessionId);
        return Task.CompletedTask;
    }

    private SftpClient RequireClient() => _client?.IsConnected == true
        ? _client
        : throw new InvalidOperationException("SFTP session is not connected.");

    private static void ValidateRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Remote path must not be empty.", nameof(path));
        if (path.Contains('\0'))
            throw new ArgumentException("Remote path must not contain null bytes.", nameof(path));
        if (!path.StartsWith('/'))
            throw new ArgumentException("Remote path must be absolute (start with '/').", nameof(path));

        // Guard against path traversal
        var normalized = string.Join("/", path.Split('/').Where(s => s != ".."));
        if (normalized != path)
            throw new ArgumentException("Remote path must not contain '..' segments.", nameof(path));
    }

    private static string BuildPermissionsString(Renci.SshNet.Sftp.ISftpFile f)
    {
        var type = f.IsDirectory ? 'd' : f.IsSymbolicLink ? 'l' : '-';
        return $"{type}" +
               $"{(f.OwnerCanRead ? 'r' : '-')}{(f.OwnerCanWrite ? 'w' : '-')}{(f.OwnerCanExecute ? 'x' : '-')}" +
               $"{(f.GroupCanRead ? 'r' : '-')}{(f.GroupCanWrite ? 'w' : '-')}{(f.GroupCanExecute ? 'x' : '-')}" +
               $"{(f.OthersCanRead ? 'r' : '-')}{(f.OthersCanWrite ? 'w' : '-')}{(f.OthersCanExecute ? 'x' : '-')}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            if (_client.IsConnected) _client.Disconnect();
            _client.Dispose();
            _client = null;
        }
        await Task.CompletedTask;
    }
}
