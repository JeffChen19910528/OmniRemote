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
    private readonly ICredentialStore? _credentialStore;

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
        IAdapterRegistry adapterRegistry,
        ICredentialStore? credentialStore = null)
    {
        _credentialStore = credentialStore;
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

        var key = CredentialKeys.For(profile);
        var remember = QuickConnect.Remember;
        if (key is not null && _credentialStore is not null && !string.IsNullOrEmpty(QuickConnect.Password))
        {
            try { await _credentialStore.SaveAsync(key, QuickConnect.Password); }
            catch (Exception ex) { StatusMessage = $"Could not save password: {ex.Message}"; }
        }
        if (remember)
        {
            try
            {
                await _profileStore.SaveAsync(profile);
                await LoadProfilesAsync();
            }
            catch (Exception ex) { StatusMessage = $"Failed to save profile: {ex.Message}"; }
        }

        await OpenSessionAsync(profile);

        QuickConnect.Password = null;
        if (!remember && key is not null && _credentialStore is not null)
        {
            // One-off connection: do not leave its password behind.
            try { await _credentialStore.DeleteAsync(key); } catch { /* best effort */ }
        }
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
        var key = CredentialKeys.For(profile);
        if (key is not null && _credentialStore is not null)
            editor.HasSavedPassword = _credentialStore.LoadAsync(key).GetAwaiter().GetResult() is not null;
        editor.ProfileSaved += OnEditorProfileSaved;
        editor.Cancelled += OnEditorCancelled;
        ActiveEditor = editor;
    }

    private async void OnEditorProfileSaved(object? sender, ConnectionProfile profile)
    {
        try
        {
            await _profileStore.SaveAsync(profile);
            await SyncPasswordAsync(sender as ProfileEditorViewModel, profile);
            await LoadProfilesAsync();
            ActiveEditor = null;
            StatusMessage = $"Profile '{profile.Name}' saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save profile: {ex.Message}";
        }
    }

    private async Task SyncPasswordAsync(ProfileEditorViewModel? editor, ConnectionProfile profile)
    {
        var key = CredentialKeys.For(profile);
        if (editor is null || key is null || _credentialStore is null) return;

        if (!editor.RememberPassword)
            await _credentialStore.DeleteAsync(key);
        else if (!string.IsNullOrEmpty(editor.Password))
            await _credentialStore.SaveAsync(key, editor.Password);
        // Remember + empty field on an existing profile: keep the stored password unchanged.
        editor.Password = null;
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
