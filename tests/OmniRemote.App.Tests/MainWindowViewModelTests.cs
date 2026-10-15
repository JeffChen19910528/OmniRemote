using OmniRemote.App.Services;
using OmniRemote.App.ViewModels;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmniRemote.App.Tests;

public sealed class MainWindowViewModelTests
{
    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Sessions_is_empty_on_creation()
    {
        var vm = MakeVm();
        Assert.Empty(vm.Sessions);
    }

    [Fact]
    public void Profiles_is_empty_on_creation()
    {
        var vm = MakeVm();
        Assert.Empty(vm.Profiles);
    }

    // ── Profile loading ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadProfilesAsync_populates_Profiles()
    {
        var store = new FakeProfileStore(new SshConnectionProfile
        {
            Name = "Dev Server", Host = "dev.example.com", Username = "admin",
        });
        var vm = MakeVm(profileStore: store);

        await vm.LoadProfilesAsync();

        Assert.Single(vm.Profiles);
        Assert.Equal("Dev Server", vm.Profiles[0].Name);
    }

    [Fact]
    public async Task LoadProfilesAsync_replaces_existing_profiles()
    {
        var store = new FakeProfileStore(
            new SshConnectionProfile { Name = "A", Host = "a.example.com", Username = "u" },
            new SshConnectionProfile { Name = "B", Host = "b.example.com", Username = "u" });
        var vm = MakeVm(profileStore: store);

        await vm.LoadProfilesAsync();
        Assert.Equal(2, vm.Profiles.Count);

        store.Profiles.Clear();
        store.Profiles.Add(new SshConnectionProfile { Name = "C", Host = "c.example.com", Username = "u" });

        await vm.LoadProfilesAsync();
        Assert.Single(vm.Profiles);
        Assert.Equal("C", vm.Profiles[0].Name);
    }

    // ── Search / filter ───────────────────────────────────────────────────

    [Fact]
    public async Task FilteredProfiles_returns_all_when_search_empty()
    {
        var store = new FakeProfileStore(
            new SshConnectionProfile { Name = "Alpha", Host = "alpha.host", Username = "u" },
            new SshConnectionProfile { Name = "Beta",  Host = "beta.host",  Username = "u" });
        var vm = MakeVm(profileStore: store);
        await vm.LoadProfilesAsync();

        vm.SearchText = "";
        Assert.Equal(2, vm.FilteredProfiles.Count());
    }

    [Fact]
    public async Task FilteredProfiles_filters_by_name()
    {
        var store = new FakeProfileStore(
            new SshConnectionProfile { Name = "Alpha", Host = "alpha.host", Username = "u" },
            new SshConnectionProfile { Name = "Beta",  Host = "beta.host",  Username = "u" });
        var vm = MakeVm(profileStore: store);
        await vm.LoadProfilesAsync();

        vm.SearchText = "alp";
        var filtered = vm.FilteredProfiles.ToList();
        Assert.Single(filtered);
        Assert.Equal("Alpha", filtered[0].Name);
    }

    [Fact]
    public async Task FilteredProfiles_filters_by_host()
    {
        var store = new FakeProfileStore(
            new SshConnectionProfile { Name = "Alpha", Host = "dev.example.com",  Username = "u" },
            new SshConnectionProfile { Name = "Beta",  Host = "prod.example.com", Username = "u" });
        var vm = MakeVm(profileStore: store);
        await vm.LoadProfilesAsync();

        vm.SearchText = "dev.";
        var filtered = vm.FilteredProfiles.ToList();
        Assert.Single(filtered);
        Assert.Equal("Alpha", filtered[0].Name);
    }

    [Fact]
    public async Task FilteredProfiles_is_case_insensitive()
    {
        var store = new FakeProfileStore(
            new SshConnectionProfile { Name = "MyServer", Host = "myserver.local", Username = "u" });
        var vm = MakeVm(profileStore: store);
        await vm.LoadProfilesAsync();

        vm.SearchText = "MYSERVER";
        Assert.Single(vm.FilteredProfiles);
    }

    // ── Open session ──────────────────────────────────────────────────────

    [Fact]
    public async Task OpenSessionAsync_adds_tab_to_Sessions()
    {
        var vm = MakeVm(registry: new FakeAdapterRegistry(ProtocolType.Ssh));

        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "Test", Host = "h", Username = "u" });

        Assert.Single(vm.Sessions);
    }

    [Fact]
    public async Task OpenSessionAsync_sets_ActiveSession()
    {
        var vm = MakeVm(registry: new FakeAdapterRegistry(ProtocolType.Ssh));

        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "Test", Host = "h", Username = "u" });

        Assert.NotNull(vm.ActiveSession);
        Assert.Same(vm.Sessions[0], vm.ActiveSession);
    }

    [Fact]
    public async Task OpenSessionAsync_no_adapter_does_not_add_tab()
    {
        var vm = MakeVm(registry: new FakeAdapterRegistry()); // no protocols handled

        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "Test", Host = "h", Username = "u" });

        Assert.Empty(vm.Sessions);
    }

    // ── Session isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task One_session_failure_does_not_affect_other_sessions()
    {
        var registry = new FakeAdapterRegistry(
            (ProtocolType.Ssh,    connectShouldFail: true),
            (ProtocolType.Telnet, connectShouldFail: false));
        var vm = MakeVm(registry: registry);

        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "Bad", Host = "bad", Username = "u" });
        await vm.OpenSessionAsync(new TelnetConnectionProfile { Name = "Good", Host = "good" });

        Assert.Equal(2, vm.Sessions.Count);
        Assert.Equal(ConnectionState.Failed,    vm.Sessions[0].State);
        Assert.Equal(ConnectionState.Connected, vm.Sessions[1].State);
    }

    // ── Close session ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseSessionAsync_removes_tab_from_Sessions()
    {
        var vm = MakeVm(registry: new FakeAdapterRegistry(ProtocolType.Ssh));
        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "Test", Host = "h", Username = "u" });

        var tab = vm.Sessions[0];
        await vm.CloseSessionAsync(tab);

        Assert.Empty(vm.Sessions);
    }

    [Fact]
    public async Task CloseSessionAsync_sets_ActiveSession_to_remaining_tab()
    {
        var vm = MakeVm(registry: new FakeAdapterRegistry(ProtocolType.Ssh));
        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "A", Host = "a", Username = "u" });
        await vm.OpenSessionAsync(new SshConnectionProfile { Name = "B", Host = "b", Username = "u" });

        var first = vm.Sessions[0];
        await vm.CloseSessionAsync(first);

        Assert.Single(vm.Sessions);
        Assert.Same(vm.Sessions[0], vm.ActiveSession);
    }

    // ── Quick Connect ─────────────────────────────────────────────────────

    [Fact]
    public void QuickConnect_BuildProfile_returns_null_for_empty_host()
    {
        var vm = MakeVm();
        vm.QuickConnect.Host = "";

        var profile = vm.QuickConnect.BuildProfile();

        Assert.Null(profile);
        Assert.NotNull(vm.QuickConnect.ValidationError);
    }

    [Fact]
    public void QuickConnect_BuildProfile_returns_profile_for_valid_input()
    {
        var vm = MakeVm();
        vm.QuickConnect.Host = "myhost.local";
        vm.QuickConnect.Port = 22;
        vm.QuickConnect.Protocol = ProtocolType.Ssh;
        vm.QuickConnect.Username = "admin";

        var profile = vm.QuickConnect.BuildProfile();

        Assert.IsType<SshConnectionProfile>(profile);
        Assert.Equal("myhost.local", profile!.Host);
    }

    [Fact]
    public void QuickConnect_Protocol_change_updates_default_port()
    {
        var vm = MakeVm();
        vm.QuickConnect.Protocol = ProtocolType.Rdp;
        Assert.Equal(3389, vm.QuickConnect.Port);

        vm.QuickConnect.Protocol = ProtocolType.Telnet;
        Assert.Equal(23, vm.QuickConnect.Port);
    }

    // ── Profile editor integration ────────────────────────────────────────

    [Fact]
    public void NewProfile_opens_editor()
    {
        var vm = MakeVm();
        vm.NewProfile();
        Assert.NotNull(vm.ActiveEditor);
    }

    [Fact]
    public void EditProfile_opens_editor_pre_populated()
    {
        var vm = MakeVm();
        var profile = new SshConnectionProfile { Name = "Edit Me", Host = "h", Username = "u" };

        vm.EditProfile(profile);

        Assert.NotNull(vm.ActiveEditor);
        Assert.Equal("Edit Me", vm.ActiveEditor!.Name);
    }

    [Fact]
    public void Editor_cancel_clears_ActiveEditor()
    {
        var vm = MakeVm();
        vm.NewProfile();

        vm.ActiveEditor!.Cancel();

        Assert.Null(vm.ActiveEditor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MainWindowViewModel MakeVm(
        FakeProfileStore? profileStore = null,
        FakeAdapterRegistry? registry = null) =>
        new(
            profileStore ?? new FakeProfileStore(),
            registry ?? new FakeAdapterRegistry());

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class FakeProfileStore : IConnectionProfileStore
    {
        public List<ConnectionProfile> Profiles { get; } = new();

        public FakeProfileStore(params ConnectionProfile[] initial) =>
            Profiles.AddRange(initial);

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ConnectionProfile>>(Profiles);
        public Task<ConnectionProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));
        public Task SaveAsync(ConnectionProfile profile, CancellationToken ct = default)
        { Profiles.Add(profile); return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        { Profiles.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    private sealed class FakeAdapterRegistry : IAdapterRegistry
    {
        private readonly Dictionary<ProtocolType, bool> _entries;

        public FakeAdapterRegistry() =>
            _entries = new Dictionary<ProtocolType, bool>();

        public FakeAdapterRegistry(ProtocolType protocol, bool connectShouldFail = false) =>
            _entries = new Dictionary<ProtocolType, bool> { [protocol] = connectShouldFail };

        public FakeAdapterRegistry(params (ProtocolType protocol, bool connectShouldFail)[] entries)
        {
            _entries = new Dictionary<ProtocolType, bool>();
            foreach (var (p, f) in entries)
                _entries[p] = f;
        }

        public bool CanHandle(ConnectionProfile profile) =>
            _entries.ContainsKey(profile.Protocol);

        public Task<IRemoteSession> CreateSessionAsync(
            ConnectionProfile profile, CancellationToken ct = default) =>
            Task.FromResult<IRemoteSession>(new FakeSession(profile, _entries[profile.Protocol]));
    }

    private sealed class FakeSession : IRemoteSession
    {
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly bool _connectShouldFail;

        public FakeSession(ConnectionProfile profile, bool connectShouldFail = false)
        {
            Protocol = profile.Protocol;
            DisplayName = $"{profile.Protocol} — {profile.Host}";
            _connectShouldFail = connectShouldFail;
        }

        public Guid SessionId { get; } = Guid.NewGuid();
        public ConnectionState State => _state;
        public ProtocolType Protocol { get; }
        public string DisplayName { get; }

        public event EventHandler<ConnectionState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            if (_connectShouldFail)
            {
                _state = ConnectionState.Failed;
                StateChanged?.Invoke(this, _state);
                throw new InvalidOperationException("Simulated connect failure");
            }
            _state = ConnectionState.Connected;
            StateChanged?.Invoke(this, _state);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            _state = ConnectionState.Disconnected;
            StateChanged?.Invoke(this, _state);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
