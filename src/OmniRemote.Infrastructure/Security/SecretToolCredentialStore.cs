using System.Diagnostics;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Security;

/// <summary>
/// Linux Secret Service (GNOME Keyring / KWallet) via the <c>secret-tool</c> CLI (libsecret-tools).
/// The secret is passed on stdin, never on the command line.
/// </summary>
public sealed class SecretToolCredentialStore : ICredentialStore
{
    private const string Service = "omniremote";

    public static bool IsAvailable()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(':', StringSplitOptions.RemoveEmptyEntries)
                   .Any(d => File.Exists(Path.Combine(d, "secret-tool")));
    }

    public async Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        var (code, _) = await RunAsync(["store", "--label=OmniRemote", "service", Service, "key", key], secret, cancellationToken);
        if (code != 0) throw new InvalidOperationException($"secret-tool store failed (exit {code}).");
    }

    public async Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        var (code, output) = await RunAsync(["lookup", "service", Service, "key", key], null, cancellationToken);
        return code == 0 && output.Length > 0 ? output : null;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => await RunAsync(["clear", "service", Service, "key", key], null, cancellationToken);

    private static async Task<(int Code, string Output)> RunAsync(
        string[] args, string? stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("secret-tool")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start secret-tool.");
        if (stdin is not null) await p.StandardInput.WriteAsync(stdin);
        p.StandardInput.Close();
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, output);
    }
}
