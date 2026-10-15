using OmniRemote.App.Services;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OmniRemote.App.Tests;

public sealed class AdapterRegistryTests
{
    [Fact]
    public void CanHandle_true_when_adapter_is_registered()
    {
        var registry = new AdapterRegistry([new FakeAdapter(ProtocolType.Ssh)]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        Assert.True(registry.CanHandle(profile));
    }

    [Fact]
    public void CanHandle_false_when_no_adapter_registered()
    {
        var registry = new AdapterRegistry([]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        Assert.False(registry.CanHandle(profile));
    }

    [Fact]
    public void CanHandle_false_when_adapter_handles_different_protocol()
    {
        var registry = new AdapterRegistry([new FakeAdapter(ProtocolType.Rdp)]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        Assert.False(registry.CanHandle(profile));
    }

    [Fact]
    public async Task CreateSessionAsync_delegates_to_matching_adapter()
    {
        var adapter = new FakeAdapter(ProtocolType.Ssh);
        var registry = new AdapterRegistry([adapter]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        var session = await registry.CreateSessionAsync(profile);

        Assert.NotNull(session);
        Assert.True(adapter.CreateSessionCalled);
    }

    [Fact]
    public async Task CreateSessionAsync_throws_when_no_adapter_matches()
    {
        var registry = new AdapterRegistry([new FakeAdapter(ProtocolType.Rdp)]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.CreateSessionAsync(profile));
    }

    [Fact]
    public async Task CreateSessionAsync_propagates_adapter_exception()
    {
        var adapter = new FakeAdapter(ProtocolType.Ssh, throwOnCreate: true);
        var registry = new AdapterRegistry([adapter]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.CreateSessionAsync(profile));
    }

    [Fact]
    public async Task CreateSessionAsync_selects_first_matching_adapter()
    {
        var first  = new FakeAdapter(ProtocolType.Ssh);
        var second = new FakeAdapter(ProtocolType.Ssh);
        var registry = new AdapterRegistry([first, second]);
        var profile = new SshConnectionProfile { Name = "x", Host = "x", Username = "u" };

        await registry.CreateSessionAsync(profile);

        Assert.True(first.CreateSessionCalled);
        Assert.False(second.CreateSessionCalled);
    }

    // ── Test double ───────────────────────────────────────────────────────

    private sealed class FakeAdapter : IRemoteProtocolAdapter
    {
        private readonly ProtocolType _protocol;
        private readonly bool _throwOnCreate;

        public bool CreateSessionCalled { get; private set; }
        public ProtocolType SupportedProtocol => _protocol;

        public FakeAdapter(ProtocolType protocol, bool throwOnCreate = false)
        {
            _protocol = protocol;
            _throwOnCreate = throwOnCreate;
        }

        public bool CanHandle(ConnectionProfile profile) =>
            profile.Protocol == _protocol;

        public Task<IRemoteSession> CreateSessionAsync(
            ConnectionProfile profile, CancellationToken ct = default)
        {
            CreateSessionCalled = true;
            if (_throwOnCreate)
                throw new InvalidOperationException("Simulated adapter failure");
            return Task.FromResult<IRemoteSession>(new NullSession(profile.Protocol));
        }
    }

    private sealed class NullSession : IRemoteSession
    {
        public NullSession(ProtocolType protocol) => Protocol = protocol;
        public Guid SessionId { get; } = Guid.NewGuid();
        public ConnectionState State => ConnectionState.Disconnected;
        public ProtocolType Protocol { get; }
        public string DisplayName => "null";
#pragma warning disable CS0067
        public event EventHandler<ConnectionState>? StateChanged;
#pragma warning restore CS0067
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
