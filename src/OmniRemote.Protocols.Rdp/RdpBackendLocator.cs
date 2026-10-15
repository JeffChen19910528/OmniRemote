using System.Runtime.InteropServices;

namespace OmniRemote.Protocols.Rdp;

internal interface IRdpBackendLocator
{
    string? FindBackend();
}

internal interface IProcessLauncher
{
    System.Diagnostics.Process? Start(System.Diagnostics.ProcessStartInfo psi);
}

internal sealed class DefaultProcessLauncher : IProcessLauncher
{
    public System.Diagnostics.Process? Start(System.Diagnostics.ProcessStartInfo psi)
        => System.Diagnostics.Process.Start(psi);
}

/// <summary>
/// Resolution order: (1) FreeRDP bundled with the app (<c>rdp/</c> next to the executable),
/// (2) FreeRDP on PATH, (3) mstsc.exe on Windows (ships with the OS).
/// Bundling means end users install nothing, and a system update of FreeRDP
/// cannot break OmniRemote.
/// </summary>
internal sealed class RdpBackendLocator : IRdpBackendLocator
{
    private static readonly string[] FreeRdpNames =
        ["sdl-freerdp", "sdl-freerdp3", "wfreerdp", "xfreerdp3", "xfreerdp", "freerdp"];

    private readonly string _baseDirectory;

    public RdpBackendLocator() : this(AppContext.BaseDirectory) { }

    internal RdpBackendLocator(string baseDirectory) => _baseDirectory = baseDirectory;

    public string? FindBackend()
    {
        var bundled = FindBundled(_baseDirectory);
        if (bundled is not null) return bundled;

        foreach (var candidate in FreeRdpNames)
        {
            var found = ResolveOnPath(candidate);
            if (found is not null) return found;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var mstsc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe");
            if (File.Exists(mstsc)) return mstsc;
        }

        return null;
    }

    internal static string? FindBundled(string baseDirectory)
    {
        var dir = Path.Combine(baseDirectory, "rdp");
        if (!Directory.Exists(dir)) return null;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        foreach (var name in FreeRdpNames)
        {
            var full = Path.Combine(dir, isWindows ? name + ".exe" : name);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    internal static bool IsOnPath(string executable) => ResolveOnPath(executable) is not null;

    /// <summary>Full path of the executable, or null. On macOS also checks Homebrew's directories,
    /// because apps launched from Finder get a minimal PATH that excludes them.</summary>
    internal static string? ResolveOnPath(string executable)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var dirs = pathVar.Split(isWindows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (OperatingSystem.IsMacOS())
            dirs.AddRange(["/opt/homebrew/bin", "/usr/local/bin"]);

        foreach (var dir in dirs)
        {
            var full = Path.Combine(dir.Trim(), executable);
            if (File.Exists(full)) return full;
            if (isWindows && File.Exists(full + ".exe")) return full + ".exe";
        }
        return null;
    }
}
