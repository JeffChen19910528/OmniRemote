namespace OmniRemote.Protocols.Abstractions;

/// <summary>
/// An SFTP session that exposes file-system operations over an SSH transport.
/// Destructive operations (Delete, Rename) must be confirmed by the caller before invoking.
/// </summary>
public interface ISftpSession : IRemoteSession
{
    /// <summary>Lists the contents of a remote directory.</summary>
    Task<IReadOnlyList<SftpEntry>> ListDirectoryAsync(
        string remotePath,
        CancellationToken cancellationToken = default);

    /// <summary>Uploads a local file to the remote path.</summary>
    Task UploadAsync(
        string localPath,
        string remotePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads a remote file to a local path.</summary>
    Task DownloadAsync(
        string remotePath,
        string localPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Renames or moves a remote path. Caller must confirm before invoking.</summary>
    Task RenameAsync(
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a remote directory.</summary>
    Task CreateDirectoryAsync(
        string remotePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a remote file or empty directory. Caller must confirm before invoking.
    /// </summary>
    Task DeleteAsync(
        string remotePath,
        bool isDirectory,
        CancellationToken cancellationToken = default);
}
