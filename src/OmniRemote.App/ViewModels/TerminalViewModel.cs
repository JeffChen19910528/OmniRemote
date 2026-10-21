using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.ViewModels;

public sealed partial class TerminalViewModel : ViewModelBase, IDisposable
{
    private static readonly Regex AnsiEscape = new(
        @"\x1b\[[0-9;?]*[A-Za-z]|\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)|\x1b[^[\]]|\x07|\x0e|\x0f",
        RegexOptions.Compiled);

    private readonly ITerminalSession _session;
    private readonly CancellationTokenSource _cts = new();
    private readonly StringBuilder _buffer = new(65536);

    [ObservableProperty]
    public partial string DisplayText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double FontSize { get; set; } = 13.0;

    public void IncreaseFontSize() => FontSize = Math.Min(36, FontSize + 1);
    public void DecreaseFontSize() => FontSize = Math.Max(8,  FontSize - 1);
    public void ResetFontSize()    => FontSize = 13.0;

    public TerminalViewModel(ITerminalSession session)
    {
        _session = session;
        _ = ReadLoopAsync(_cts.Token);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var raw = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int n = await _session.TerminalOutput.ReadAsync(raw, ct);
                if (n == 0) break;
                AppendOutput(Encoding.UTF8.GetString(raw, 0, n));
            }
        }
        catch (OperationCanceledException) { }
        catch { /* session closed */ }
    }

    private void AppendOutput(string raw)
    {
        var text = AnsiEscape.Replace(raw, string.Empty);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        lock (_buffer)
            _buffer.Append(text);

        var snapshot = _buffer.ToString();
        Dispatcher.UIThread.Post(() => DisplayText = snapshot);
    }

    public void SendText(string text)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _session.TerminalInput.Write(bytes);
            _session.TerminalInput.Flush();
        }
        catch { /* session may be closed */ }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
