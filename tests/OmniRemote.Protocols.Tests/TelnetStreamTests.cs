using Microsoft.Extensions.Logging.Abstractions;
using OmniRemote.Protocols.Telnet;
using System.Net;
using System.Net.Sockets;

namespace OmniRemote.Protocols.Tests;

/// <summary>
/// Unit tests for TelnetStream IAC negotiation using a loopback TCP socket pair.
/// No external Telnet server required.
/// </summary>
public sealed class TelnetStreamTests : IDisposable
{
    // ── Socket pair helpers ───────────────────────────────────────────────

    private readonly TcpListener _listener;
    private readonly TcpClient _client;
    private readonly TcpClient _server;
    private readonly NetworkStream _serverStream;
    private readonly TelnetStream _sut;

    public TelnetStreamTests()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _client = new TcpClient();
        _client.Connect(IPAddress.Loopback, port);
        _server = _listener.AcceptTcpClient();

        _serverStream = _server.GetStream();
        _sut = new TelnetStream(_client.GetStream(), NullLogger.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _server.Dispose();
        _client.Dispose();
        _listener.Stop();
    }

    // ── Clean data pass-through ───────────────────────────────────────────

    [Fact]
    public async Task Clean_bytes_pass_through_unchanged()
    {
        var data = "Hello, world!\r\n"u8.ToArray();
        await _serverStream.WriteAsync(data);
        await _serverStream.FlushAsync();

        var buf = new byte[64];
        int n = await _sut.ReadAsync(buf.AsMemory(0, buf.Length));

        Assert.Equal(data.Length, n);
        Assert.Equal(data, buf[..n]);
    }

    // ── IAC IAC → literal 0xFF ────────────────────────────────────────────

    [Fact]
    public async Task IAC_IAC_produces_literal_0xFF_byte()
    {
        // Send IAC IAC (escaped 0xFF data)
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFF });
        await _serverStream.FlushAsync();

        var buf = new byte[4];
        int n = await _sut.ReadAsync(buf.AsMemory(0, buf.Length));

        Assert.Equal(1, n);
        Assert.Equal(0xFF, buf[0]);
    }

    // ── ECHO negotiation ──────────────────────────────────────────────────

    [Fact]
    public async Task DO_ECHO_is_refused_with_WONT_ECHO()
    {
        // Server sends: IAC DO ECHO
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFD, 1 });
        await _serverStream.FlushAsync();

        // Consume (no clean data produced)
        await DrainCleanDataAsync();

        // Read the response sent back to server
        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFC, 1 }, response); // IAC WONT ECHO
    }

    [Fact]
    public async Task WILL_ECHO_is_accepted_with_DO_ECHO()
    {
        // Server sends: IAC WILL ECHO
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFB, 1 });
        await _serverStream.FlushAsync();

        await DrainCleanDataAsync();

        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFD, 1 }, response); // IAC DO ECHO
    }

    // ── SGA negotiation ───────────────────────────────────────────────────

    [Fact]
    public async Task DO_SGA_is_accepted_with_WILL_SGA()
    {
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFD, 3 });
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();

        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFB, 3 }, response); // IAC WILL SGA
    }

    [Fact]
    public async Task WILL_SGA_is_accepted_with_DO_SGA()
    {
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFB, 3 });
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();

        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFD, 3 }, response); // IAC DO SGA
    }

    // ── NAWS negotiation ──────────────────────────────────────────────────

    [Fact]
    public async Task DO_NAWS_triggers_WILL_NAWS_and_window_size_subneg()
    {
        Assert.False(_sut.NawsEnabled);

        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFD, 31 }); // IAC DO NAWS
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();

        // Should receive: IAC WILL NAWS (3 bytes) + IAC SB NAWS cols rows IAC SE (9 bytes) = 12 bytes
        var response = await ReadFromServerAsync(12);
        Assert.Equal(new byte[] { 0xFF, 0xFB, 31 }, response[..3]); // IAC WILL NAWS
        Assert.Equal(new byte[] { 0xFF, 0xFA, 31 }, response[3..6]); // IAC SB NAWS
        Assert.Equal(new byte[] { 0xFF, 0xF0 }, response[10..]); // IAC SE

        Assert.True(_sut.NawsEnabled);
    }

    [Fact]
    public async Task SetWindowSize_sends_NAWS_subneg_after_negotiation()
    {
        // First negotiate NAWS
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFD, 31 });
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();
        await ReadFromServerAsync(12); // consume the WILL NAWS + initial size

        // Now resize
        _sut.SendWindowSize(132, 48);

        var response = await ReadFromServerAsync(9);
        Assert.Equal(new byte[] { 0xFF, 0xFA, 31 }, response[..3]); // IAC SB NAWS
        Assert.Equal((byte)(132 >> 8), response[3]);
        Assert.Equal((byte)(132 & 0xFF), response[4]);
        Assert.Equal((byte)(48 >> 8), response[5]);
        Assert.Equal((byte)(48 & 0xFF), response[6]);
        Assert.Equal(new byte[] { 0xFF, 0xF0 }, response[7..]); // IAC SE
    }

    [Fact]
    public void SendWindowSize_does_nothing_before_NAWS_negotiated()
    {
        Assert.False(_sut.NawsEnabled);
        _sut.SendWindowSize(80, 24); // should not throw; no bytes sent
    }

    // ── Unknown option refusal ────────────────────────────────────────────

    [Fact]
    public async Task DO_unknown_option_is_refused_with_WONT()
    {
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFD, 99 }); // IAC DO (unknown)
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();

        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFC, 99 }, response); // IAC WONT (unknown)
    }

    [Fact]
    public async Task WILL_unknown_option_is_refused_with_DONT()
    {
        await _serverStream.WriteAsync(new byte[] { 0xFF, 0xFB, 99 }); // IAC WILL (unknown)
        await _serverStream.FlushAsync();
        await DrainCleanDataAsync();

        var response = await ReadFromServerAsync(3);
        Assert.Equal(new byte[] { 0xFF, 0xFE, 99 }, response); // IAC DONT (unknown)
    }

    // ── IAC sequences interspersed with data ──────────────────────────────

    [Fact]
    public async Task Clean_data_survives_embedded_IAC_sequences()
    {
        // data "AB" + IAC WILL SGA + "CD"
        var payload = new byte[] { (byte)'A', (byte)'B', 0xFF, 0xFB, 3, (byte)'C', (byte)'D' };
        await _serverStream.WriteAsync(payload);
        await _serverStream.FlushAsync();

        var buf = new byte[16];
        int n = await ReadCleanDataAsync(buf, expectedMinBytes: 4);

        Assert.Equal(4, n);
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D' }, buf[..4]);
    }

    // ── Write path: IAC escaping ──────────────────────────────────────────

    [Fact]
    public async Task Write_escapes_0xFF_as_IAC_IAC()
    {
        await _sut.WriteAsync(new byte[] { 0x41, 0xFF, 0x42 }); // A 0xFF B

        var buf = new byte[8];
        int n = await _serverStream.ReadAsync(buf.AsMemory(0, buf.Length));

        Assert.Equal(4, n);
        Assert.Equal(new byte[] { 0x41, 0xFF, 0xFF, 0x42 }, buf[..4]); // A IAC IAC B
    }

    [Fact]
    public async Task Write_passes_through_data_without_0xFF_unchanged()
    {
        var data = "Hello"u8.ToArray();
        await _sut.WriteAsync(data);

        var buf = new byte[16];
        int n = await _serverStream.ReadAsync(buf.AsMemory(0, buf.Length));
        Assert.Equal(data, buf[..n]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task DrainCleanDataAsync()
    {
        var buf = new byte[256];
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
#pragma warning disable CA2022 // intentional fire-and-forget partial read; we discard the result
        try { await _sut.ReadAsync(buf.AsMemory(), cts.Token); } catch { }
#pragma warning restore CA2022
    }

    private async Task<int> ReadCleanDataAsync(byte[] buf, int expectedMinBytes)
    {
        int total = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (total < expectedMinBytes)
            total += await _sut.ReadAsync(buf.AsMemory(total, buf.Length - total), cts.Token);
        return total;
    }

    private async Task<byte[]> ReadFromServerAsync(int count)
    {
        var buf = new byte[count];
        int total = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (total < count)
            total += await _serverStream.ReadAsync(buf.AsMemory(total, count - total), cts.Token);
        return buf;
    }
}
