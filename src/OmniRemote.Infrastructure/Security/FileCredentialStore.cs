using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Security;

/// <summary>
/// Portable fallback secret store (macOS, and Linux without a keyring): AES-256-GCM encrypted
/// file plus a random key file, both owner-only (0600) on Unix. This protects against casual
/// disclosure (backups, screen shares, stray copies of credentials.dat) but not against
/// malware running as the same user — an OS keychain is stronger and is preferred where available.
/// </summary>
public sealed class FileCredentialStore : ICredentialStore
{
    private const int KeySize = 32, NonceSize = 12, TagSize = 16;

    private readonly string _dataPath;
    private readonly string _keyPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileCredentialStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _dataPath = Path.Combine(directory, "credentials.dat");
        _keyPath = Path.Combine(directory, "credentials.key");
    }

    public async Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var all = ReadAll();
            all[key] = Encrypt(secret);
            WriteAll(all);
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return ReadAll().TryGetValue(key, out var blob) ? Decrypt(blob) : null;
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var all = ReadAll();
            if (all.Remove(key)) WriteAll(all);
        }
        finally { _gate.Release(); }
    }

    private Dictionary<string, string> ReadAll()
    {
        if (!File.Exists(_dataPath)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_dataPath)) ?? new();
        }
        catch (JsonException) { return new(); }
    }

    private void WriteAll(Dictionary<string, string> all)
    {
        var tmp = _dataPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(all));
        RestrictToOwner(tmp);
        File.Move(tmp, _dataPath, overwrite: true);
    }

    private byte[] GetKey()
    {
        if (File.Exists(_keyPath))
        {
            var existing = File.ReadAllBytes(_keyPath);
            if (existing.Length == KeySize) return existing;
        }
        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(_keyPath, key);
        RestrictToOwner(_keyPath);
        return key;
    }

    private string Encrypt(string secret)
    {
        var plain = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(GetKey(), TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        Array.Clear(plain);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    private string? Decrypt(string blob)
    {
        try
        {
            var raw = Convert.FromBase64String(blob);
            var nonce = raw[..NonceSize];
            var tag = raw[NonceSize..(NonceSize + TagSize)];
            var cipher = raw[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(GetKey(), TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            var result = Encoding.UTF8.GetString(plain);
            Array.Clear(plain);
            return result;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null; // key lost or file corrupted: treat as "no saved password"
        }
    }

    private static void RestrictToOwner(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
