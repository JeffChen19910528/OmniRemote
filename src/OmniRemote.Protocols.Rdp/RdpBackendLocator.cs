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

internal sealed class RdpBackendLocator : IRdpBackendLocator
{
    public string? FindBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var mstsc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mstsc.exe");
            if (File.Exists(mstsc)) return mstsc;
        }

        foreach (var candidate in new[] { "xfreerdp", "xfreerdp3", "freerdp" })
        {
            if (IsOnPath(candidate)) return candidate;
        }

        return null;
    }

    internal static bool IsOnPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim(), executable);
            if (File.Exists(full)) return true;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(full + ".exe")) return true;
        }
        return false;
    }
}
