namespace OmniRemote.Protocols.Abstractions;

public interface ICredentialStore
{
    /// <summary>
    /// Stores a secret in OS-protected storage. Never logs or persists plaintext.
    /// </summary>
    Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
