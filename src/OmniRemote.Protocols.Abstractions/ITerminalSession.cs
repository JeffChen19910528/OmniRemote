namespace OmniRemote.Protocols.Abstractions;

/// <summary>
/// A remote session that provides interactive terminal I/O (SSH shell, Telnet).
/// </summary>
public interface ITerminalSession : IRemoteSession
{
    /// <summary>Write bytes here to send input to the remote terminal.</summary>
    Stream TerminalInput { get; }

    /// <summary>Read bytes from here to receive output from the remote terminal.</summary>
    Stream TerminalOutput { get; }

    /// <summary>
    /// Notifies the remote that the local terminal has been resized.
    /// Safe to call at any time while connected; no-op when disconnected.
    /// </summary>
    void SetTerminalSize(uint columns, uint rows);

    /// <summary>
    /// Optional callback invoked when an SSH host key cannot be automatically trusted
    /// (Unknown or Changed result from IHostIdentityPolicy).
    ///
    /// Return true → accept and persist the key.
    /// Return false → abort the connection.
    ///
    /// If null: Unknown keys are rejected (secure default).
    /// Changed keys are ALWAYS rejected regardless of this callback.
    /// </summary>
    Func<HostKeyVerificationRequest, CancellationToken, Task<bool>>? HostKeyVerificationCallback { get; set; }
}
