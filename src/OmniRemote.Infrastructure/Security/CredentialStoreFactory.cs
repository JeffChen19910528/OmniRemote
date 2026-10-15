using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Security;

public static class CredentialStoreFactory
{
    /// <summary>
    /// Windows: Credential Manager. Linux: Secret Service when secret-tool exists (falling back
    /// to the encrypted file if the keyring is unreachable). macOS / other: encrypted file.
    /// </summary>
    public static ICredentialStore Create(string appDataDirectory)
    {
        if (OperatingSystem.IsWindows())
            return new WindowsCredentialStore();

        var file = new FileCredentialStore(appDataDirectory);
        if (OperatingSystem.IsLinux() && SecretToolCredentialStore.IsAvailable())
            return new FallbackCredentialStore(new SecretToolCredentialStore(), file);
        return file;
    }
}

/// <summary>Tries the primary store; if it throws or has nothing, uses the secondary.</summary>
public sealed class FallbackCredentialStore(ICredentialStore primary, ICredentialStore secondary) : ICredentialStore
{
    public async Task SaveAsync(string key, string secret, CancellationToken ct = default)
    {
        try { await primary.SaveAsync(key, secret, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await secondary.SaveAsync(key, secret, ct);
        }
    }

    public async Task<string?> LoadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await primary.LoadAsync(key, ct);
            if (value is not null) return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        return await secondary.LoadAsync(key, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try { await primary.DeleteAsync(key, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        await secondary.DeleteAsync(key, ct);
    }
}
