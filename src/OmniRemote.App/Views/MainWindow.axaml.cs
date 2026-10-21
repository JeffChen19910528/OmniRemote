using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using OmniRemote.App.ViewModels;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.HostKeyConfirmAsync = ShowHostKeyDialogAsync;
        };
    }

    private Task<bool> ShowHostKeyDialogAsync(HostKeyVerificationRequest request, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var result = await new HostKeyDialog(request).ShowDialog<bool>(this);
                tcs.SetResult(result);
            }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}