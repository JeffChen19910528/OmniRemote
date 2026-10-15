using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Security;

/// <summary>
/// Windows Credential Manager (DPAPI) backed secret store.
/// Secrets are never written to SQLite, logs, or exported profiles.
/// </summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string TargetPrefix = "OmniRemote:";

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        NativeCredential.Write(TargetPrefix + key, secret);
        return Task.CompletedTask;
    }

    public Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = NativeCredential.Read(TargetPrefix + key);
        return Task.FromResult(value);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        NativeCredential.Delete(TargetPrefix + key);
        return Task.CompletedTask;
    }
}
