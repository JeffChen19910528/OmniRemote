using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.ViewModels;

public sealed partial class SessionTabViewModel : ViewModelBase
{
    private readonly IRemoteSession _session;

    [ObservableProperty]
    public partial ConnectionState State { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public string DisplayName => _session.DisplayName;
    public ProtocolType Protocol => _session.Protocol;
    public Guid SessionId => _session.SessionId;

    /// <summary>
    /// Non-null when the session has a persistent security warning (e.g. Telnet).
    /// The UI must keep this visible for the lifetime of the session.
    /// </summary>
    public string? SecurityWarning { get; }

    /// <summary>
    /// True when any security validation has been weakened for this session's profile.
    /// Drives the persistent ⚠ indicator in the tab header.
    /// </summary>
    public bool IsBypassActive { get; }

    public SessionTabViewModel(IRemoteSession session, ConnectionProfile? profile = null)
    {
        _session = session;
        State = session.State;
        SecurityWarning = session.SecurityWarning;
        IsBypassActive = profile?.IsBypassActive ?? false;

        _session.StateChanged += (_, newState) =>
        {
            State = newState;
            if (newState == ConnectionState.Failed)
                ErrorMessage ??= "Connection failed.";
        };
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        ErrorMessage = null;
        try
        {
            await _session.ConnectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        try
        {
            await _session.DisconnectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _session.DisconnectAsync(); } catch { }
        await _session.DisposeAsync();
    }
}
