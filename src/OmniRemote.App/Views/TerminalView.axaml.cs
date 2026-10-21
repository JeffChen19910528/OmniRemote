using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmniRemote.App.ViewModels;

namespace OmniRemote.App.Views;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        Focusable = true;

        AddHandler(KeyDownEvent,          OnTerminalKeyDown,   RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent,        OnTerminalTextInput, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnPointerWheel,   RoutingStrategies.Tunnel);
        PointerPressed += (_, _) => Focus();

        DecrBtn.Click  += (_, _) => { Vm?.DecreaseFontSize(); Focus(); };
        IncrBtn.Click  += (_, _) => { Vm?.IncreaseFontSize(); Focus(); };
        ResetBtn.Click += (_, _) => { Vm?.ResetFontSize();    Focus(); };
    }

    private TerminalViewModel? Vm => DataContext as TerminalViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TerminalViewModel vm)
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TerminalViewModel.DisplayText))
                    Dispatcher.UIThread.Post(ScrollToBottom, DispatcherPriority.Background);
            };
    }

    private void ScrollToBottom()
    {
        double maxY = Math.Max(0, Scroll.Extent.Height - Scroll.Viewport.Height);
        Scroll.Offset = new Vector(Scroll.Offset.X, maxY);
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        var vm = Vm;
        if (vm is null) return;
        if (e.Delta.Y > 0) vm.IncreaseFontSize();
        else               vm.DecreaseFontSize();
        e.Handled = true;
    }

    private void OnTerminalKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        // Font size shortcuts (must not be forwarded to SSH)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.OemPlus:
                case Key.Add:
                    vm.IncreaseFontSize(); e.Handled = true; return;
                case Key.OemMinus:
                case Key.Subtract:
                    vm.DecreaseFontSize(); e.Handled = true; return;
                case Key.D0:
                case Key.NumPad0:
                    vm.ResetFontSize();    e.Handled = true; return;
            }
        }

        var send = e.Key switch
        {
            Key.Enter    => "\r",
            Key.Back     => "\x7f",
            Key.Tab      => "\t",
            Key.Escape   => "\x1b",
            Key.Up       => "\x1b[A",
            Key.Down     => "\x1b[B",
            Key.Right    => "\x1b[C",
            Key.Left     => "\x1b[D",
            Key.Home     => "\x1b[H",
            Key.End      => "\x1b[F",
            Key.Delete   => "\x1b[3~",
            Key.PageUp   => "\x1b[5~",
            Key.PageDown => "\x1b[6~",
            Key.F1       => "\x1bOP",
            Key.F2       => "\x1bOQ",
            Key.F3       => "\x1bOR",
            Key.F4       => "\x1bOS",
            _ => GetCtrlSequence(e)
        };

        if (send is null) return;
        vm.SendText(send);
        e.Handled = true;
    }

    private static string? GetCtrlSequence(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return null;
        return e.Key switch
        {
            Key.C => "\x03",
            Key.D => "\x04",
            Key.L => "\x0c",
            Key.Z => "\x1a",
            Key.A => "\x01",
            Key.E => "\x05",
            Key.K => "\x0b",
            Key.U => "\x15",
            Key.R => "\x12",
            Key.W => "\x17",
            _ => null
        };
    }

    private void OnTerminalTextInput(object? sender, TextInputEventArgs e)
    {
        var vm = Vm;
        if (vm is null || string.IsNullOrEmpty(e.Text)) return;
        vm.SendText(e.Text);
        e.Handled = true;
    }
}
