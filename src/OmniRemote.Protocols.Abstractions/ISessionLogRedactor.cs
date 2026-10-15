namespace OmniRemote.Protocols.Abstractions;

public interface ISessionLogRedactor
{
    /// <summary>
    /// Strips passwords, keys, and tokens from log messages before they are written.
    /// </summary>
    string Redact(string message);
}
