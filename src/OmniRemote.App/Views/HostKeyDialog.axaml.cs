using Avalonia.Controls;
using OmniRemote.App.Services;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.Views;

public partial class HostKeyDialog : Window
{
    public HostKeyDialog(HostKeyVerificationRequest request)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        Title            = loc.HostKeyDialogTitle;
        TitleText.Text   = loc.HostKeyDialogTitle;
        MessageText.Text = loc.HostKeyDialogMessage(request.Host, request.Port, request.KeyType);
        FingerprintText.Text = request.Fingerprint;
        TrustButton.Content  = loc.HostKeyDialogTrustButton;
        RejectButton.Content = loc.HostKeyDialogRejectButton;

        TrustButton.Click  += (_, _) => Close(true);
        RejectButton.Click += (_, _) => Close(false);
    }
}
