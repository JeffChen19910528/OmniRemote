using OmniRemote.App.ViewModels;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.App.Tests;

public sealed class SessionTabViewModelTests
{
    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void State_reflects_session_initial_state()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);
        Assert.Equal(ConnectionState.Disconnected, vm.State);
    }

    [Fact]
    public void DisplayName_and_Protocol_come_from_session()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);
        Assert.Equal(session.DisplayName, vm.DisplayName);
        Assert.Equal(session.Protocol, vm.Protocol);
    }

    // ── State propagation ─────────────────────────────────────────────────

    [Fact]
    public void StateChanged_event_updates_vm_State()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);

        session.SimulateStateChange(ConnectionState.Connected);

        Assert.Equal(ConnectionState.Connected, vm.State);
    }

    [Fact]
    public void Failed_state_sets_ErrorMessage()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);

        session.SimulateStateChange(ConnectionState.Failed);

        Assert.Equal(ConnectionState.Failed, vm.State);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void Connected_state_does_not_set_ErrorMessage()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);

        session.SimulateStateChange(ConnectionState.Connected);

        Assert.Null(vm.ErrorMessage);
    }

    // ── Security warning ──────────────────────────────────────────────────

    [Fact]
    public void SecurityWarning_is_null_for_non_Telnet_session()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);
        Assert.Null(vm.SecurityWarning);
    }

    [Fact]
    public void SecurityWarning_is_set_for_session_with_warning()
    {
        var session = new FakeSession(ConnectionState.Disconnected, securityWarning: "DANGER: plaintext");
        var vm = new SessionTabViewModel(session);
        Assert.Equal("DANGER: plaintext", vm.SecurityWarning);
    }

    // ── Connect / Disconnect commands ─────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_calls_session_ConnectAsync()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);

        await vm.ConnectAsync();

        Assert.True(session.ConnectCalled);
    }

    [Fact]
    public async Task ConnectAsync_clears_previous_error_message()
    {
        var session = new FakeSession(ConnectionState.Disconnected);
        var vm = new SessionTabViewModel(session);
        session.SimulateStateChange(ConnectionState.Failed); // sets ErrorMessage

        session.NextConnectShouldSucceed = true;
        await vm.ConnectAsync();

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task ConnectAsync_captures_exception_as_ErrorMessage()
    {
        var session = new FakeSession(ConnectionState.Disconnected)
        {
            NextConnectException = new InvalidOperationException("host unreachable"),
        };
        var vm = new SessionTabViewModel(session);

        await vm.ConnectAsync();

        Assert.Equal("host unreachable", vm.ErrorMessage);
    }

    [Fact]
    public async Task DisconnectAsync_calls_session_DisconnectAsync()
    {
        var session = new FakeSession(ConnectionState.Connected);
        var vm = new SessionTabViewModel(session);

        await vm.DisconnectAsync();

        Assert.True(session.DisconnectCalled);
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_disconnects_and_disposes_session()
    {
        var session = new FakeSession(ConnectionState.Connected);
        var vm = new SessionTabViewModel(session);

        await vm.DisposeAsync();

        Assert.True(session.Disposed);
    }

    // ── Test double ───────────────────────────────────────────────────────

    private sealed class FakeSession : IRemoteSession
    {
        private ConnectionState _state;
        private readonly string? _securityWarning;

        public bool ConnectCalled { get; private set; }
        public bool DisconnectCalled { get; private set; }
        public bool Disposed { get; private set; }
        public bool NextConnectShouldSucceed { get; set; }
        public Exception? NextConnectException { get; set; }

        public FakeSession(ConnectionState initialState, string? securityWarning = null)
        {
            _state = initialState;
            _securityWarning = securityWarning;
        }

        public Guid SessionId { get; } = Guid.NewGuid();
        public ConnectionState State => _state;
        public ProtocolType Protocol => ProtocolType.Ssh;
        public string DisplayName => "SSH — user@host:22";
        public string? SecurityWarning => _securityWarning;

        public event EventHandler<ConnectionState>? StateChanged;

        public void SimulateStateChange(ConnectionState newState)
        {
            _state = newState;
            StateChanged?.Invoke(this, newState);
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalled = true;
            if (NextConnectException is { } ex) throw ex;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
