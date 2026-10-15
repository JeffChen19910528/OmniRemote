using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniRemote.App.Services;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConnectionProfileStore _profileStore;
    private readonly IAdapterRegistry _adapterRegistry;

    // ── Profile list ──────────────────────────────────────────────────────

    public ObservableCollection<ConnectionProfile> Profiles { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public System.Collections.Generic.IEnumerable<ConnectionProfile> FilteredProfiles =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Profiles
            : Profiles.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Host.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    // ── Sessions ──────────────────────────────────────────────────────────

    public ObservableCollection<SessionTabViewModel> Sessions { get; } = new();

    [ObservableProperty]
    public partial SessionTabViewModel? ActiveSession { get; set; }

    // ── Profile editor ────────────────────────────────────────────────────

    [ObservableProperty]
    public partial ProfileEditorViewModel? ActiveEditor { get; set; }

    // ── Quick Connect ─────────────────────────────────────────────────────

    public QuickConnectViewModel QuickConnect { get; } = new();

    // ── Status ────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "OmniRemote ready.";

    // ── Constructor ───────────────────────────────────────────────────────

    public MainWindowViewModel(
        IConnectionProfileStore profileStore,
        IAdapterRegistry adapterRegistry)
    {
        _profileStore = profileStore;
        _adapterRegistry = adapterRegistry;
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadProfilesAsync()
    {
        try
        {
            var profiles = await _profileStore.GetAllAsync();
            Profiles.Clear();
            foreach (var p in profiles)
                Profiles.Add(p);
            StatusMessage = $"{Profiles.Count} profile(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load profiles: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ConnectProfileAsync(ConnectionProfile profile) =>
        await OpenSessionAsync(profile);

    [RelayCommand]
    public async Task QuickConnectAsync()
    {
        var profile = QuickConnect.BuildProfile();
        if (profile is null) return;
        await OpenSessionAsync(profile);
    }

    [RelayCommand]
    public async Task CloseSessionAsync(SessionTabViewModel tab)
    {
        Sessions.Remove(tab);
        if (ActiveSession == tab)
            ActiveSession = Sessions.FirstOrDefault();
        await tab.DisposeAsync();
        StatusMessage = "Session closed.";
    }

    [RelayCommand]
    public void NewProfile()
    {
        var editor = new ProfileEditorViewModel();
        editor.ProfileSaved += OnEditorProfileSaved;
        editor.Cancelled += OnEditorCancelled;
        ActiveEditor = editor;
    }

    [RelayCommand]
    public void EditProfile(ConnectionProfile profile)
    {
        var editor = new ProfileEditorViewModel(profile);
        editor.ProfileSaved += OnEditorProfileSaved;
        editor.Cancelled += OnEditorCancelled;
        ActiveEditor = editor;
    }

    private async void OnEditorProfileSaved(object? sender, ConnectionProfile profile)
    {
        try
        {
            await _profileStore.SaveAsync(profile);
            await LoadProfilesAsync();
            ActiveEditor = null;
            StatusMessage = $"Profile '{profile.Name}' saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save profile: {ex.Message}";
        }
    }

    private void OnEditorCancelled(object? sender, EventArgs e) => ActiveEditor = null;

    partial void OnSearchTextChanged(string value) =>
        OnPropertyChanged(nameof(FilteredProfiles));

    // ── Session lifecycle ─────────────────────────────────────────────────

    public async Task OpenSessionAsync(ConnectionProfile profile)
    {
        if (!_adapterRegistry.CanHandle(profile))
        {
            StatusMessage = $"No adapter found for protocol '{profile.Protocol}'.";
            return;
        }

        IRemoteSession session;
        try
        {
            session = await _adapterRegistry.CreateSessionAsync(profile);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to create session: {ex.Message}";
            return;
        }

        var tab = new SessionTabViewModel(session, profile);
        Sessions.Add(tab);
        ActiveSession = tab;
        StatusMessage = $"Opening {session.DisplayName}…";

        await tab.ConnectAsync();

        StatusMessage = tab.State == ConnectionState.Connected
            ? $"Connected: {session.DisplayName}"
            : $"Failed: {tab.ErrorMessage ?? "unknown error"}";
    }
}
