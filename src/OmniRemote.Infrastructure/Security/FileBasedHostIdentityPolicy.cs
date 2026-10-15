using Microsoft.Extensions.Logging;
using OmniRemote.Protocols.Abstractions;
using System.Collections.Concurrent;
using System.Text;

namespace OmniRemote.Infrastructure.Security;

/// <summary>
/// Simple file-backed known-hosts store. Validates against recorded fingerprints;
/// refuses silently-changed keys.
/// </summary>
public sealed class FileBasedHostIdentityPolicy : IHostIdentityPolicy
{
    private readonly string _knownHostsPath;
    private readonly ILogger<FileBasedHostIdentityPolicy> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public FileBasedHostIdentityPolicy(string knownHostsPath, ILogger<FileBasedHostIdentityPolicy> logger)
    {
        _knownHostsPath = knownHostsPath;
        _logger = logger;
        Load();
    }

    public Task<HostKeyTrustResult> CheckHostKeyAsync(
        string host, int port, string keyType, byte[] keyData, CancellationToken cancellationToken = default)
    {
        var lookupKey = MakeKey(host, port, keyType);
        var fingerprint = Fingerprint(keyData);

        if (_cache.TryGetValue(lookupKey, out var stored))
        {
            var result = stored == fingerprint ? HostKeyTrustResult.Trusted : HostKeyTrustResult.Changed;
            if (result == HostKeyTrustResult.Changed)
                _logger.LogWarning("Host key changed for {Host}:{Port} ({KeyType}). Expected {Expected}, got {Got}",
                    host, port, keyType, stored[..Math.Min(16, stored.Length)], fingerprint[..Math.Min(16, fingerprint.Length)]);
            return Task.FromResult(result);
        }

        return Task.FromResult(HostKeyTrustResult.Unknown);
    }

    public async Task TrustHostKeyAsync(
        string host, int port, string keyType, byte[] keyData, CancellationToken cancellationToken = default)
    {
        var lookupKey = MakeKey(host, port, keyType);
        var fingerprint = Fingerprint(keyData);
        _cache[lookupKey] = fingerprint;
        await PersistAsync(cancellationToken);
        _logger.LogInformation("Trusted host key for {Host}:{Port} ({KeyType})", host, port, keyType);
    }

    private void Load()
    {
        if (!File.Exists(_knownHostsPath)) return;
        foreach (var line in File.ReadAllLines(_knownHostsPath))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2) _cache[parts[0]] = parts[1];
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_knownHostsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var lines = _cache.Select(kv => $"{kv.Key}\t{kv.Value}");
        await File.WriteAllLinesAsync(_knownHostsPath, lines, cancellationToken);
    }

    private static string MakeKey(string host, int port, string keyType) => $"{host}:{port}:{keyType}";

    private static string Fingerprint(byte[] keyData) =>
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(keyData));
}
