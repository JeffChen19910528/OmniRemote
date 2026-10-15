using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace OmniRemote.Protocols.Telnet;

/// <summary>
/// Wraps a raw NetworkStream and handles Telnet IAC option negotiation inline.
/// Clean application data passes through; IAC sequences are consumed and responded to.
/// </summary>
internal sealed class TelnetStream : Stream
{
    // RFC 854 command bytes
    internal const byte IAC  = 0xFF;
    internal const byte WILL = 0xFB;
    internal const byte WONT = 0xFC;
    internal const byte DO   = 0xFD;
    internal const byte DONT = 0xFE;
    internal const byte SB   = 0xFA;
    internal const byte SE   = 0xF0;
    internal const byte GA   = 0xF9;

    // Telnet option codes
    internal const byte OPT_ECHO      = 1;
    internal const byte OPT_SGA       = 3;
    internal const byte OPT_TERM_TYPE = 24;
    internal const byte OPT_NAWS      = 31;

    private enum ParseState { Data, GotIAC, GotCmd, GotSB, GotSBData, GotSBIAC }

    private readonly NetworkStream _inner;
    private readonly ILogger _logger;
    private ParseState _state = ParseState.Data;
    private byte _cmd;
    private readonly List<byte> _sbData = new();
    private readonly Queue<byte> _overflow = new();
    private bool _nawsEnabled;
    private uint _cols = 80;
    private uint _rows = 24;

    public TelnetStream(NetworkStream inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public override bool CanRead  => true;
    public override bool CanWrite => true;
    public override bool CanSeek  => false;
    public override long Length   => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);

    // ── Read path ─────────────────────────────────────────────────────────

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        // Drain any overflow from previous read first
        int drained = DrainOverflow(buffer.Span);
        if (drained > 0) return drained;

        // Read from network until we have at least one clean byte
        var raw = new byte[4096];
        int cleanCount = 0;

        while (cleanCount == 0)
        {
            int n = await _inner.ReadAsync(raw.AsMemory(0, raw.Length), ct);
            if (n == 0) return 0;

            var responses = new List<byte>();
            var clean = new List<byte>(n);

            for (int i = 0; i < n; i++)
                ProcessByte(raw[i], responses, clean);

            if (responses.Count > 0)
                await _inner.WriteAsync(responses.ToArray(), ct);

            cleanCount = Math.Min(clean.Count, buffer.Length);
            for (int i = 0; i < cleanCount; i++)
                buffer.Span[i] = clean[i];

            // Buffer overflow
            for (int i = cleanCount; i < clean.Count; i++)
                _overflow.Enqueue(clean[i]);
        }

        return cleanCount;
    }

    private int DrainOverflow(Span<byte> target)
    {
        int n = 0;
        while (n < target.Length && _overflow.Count > 0)
            target[n++] = _overflow.Dequeue();
        return n;
    }

    private void ProcessByte(byte b, List<byte> responses, List<byte> clean)
    {
        switch (_state)
        {
            case ParseState.Data:
                if (b == IAC) { _state = ParseState.GotIAC; break; }
                if (b != 0x00) clean.Add(b); // strip NUL padding
                break;

            case ParseState.GotIAC:
                if (b == IAC) { clean.Add(0xFF); _state = ParseState.Data; break; }
                if (b == SB)  { _sbData.Clear(); _state = ParseState.GotSB; break; }
                if (b is WILL or WONT or DO or DONT) { _cmd = b; _state = ParseState.GotCmd; break; }
                _state = ParseState.Data; // NOP, GA, DM, BRK, etc. — ignore
                break;

            case ParseState.GotCmd:
                Negotiate(_cmd, b, responses);
                _state = ParseState.Data;
                break;

            case ParseState.GotSB:
                _sbData.Add(b);
                _state = ParseState.GotSBData;
                break;

            case ParseState.GotSBData:
                if (b == IAC) { _state = ParseState.GotSBIAC; break; }
                _sbData.Add(b);
                break;

            case ParseState.GotSBIAC:
                if (b == SE)  { HandleSubneg(_sbData); _state = ParseState.Data; break; }
                if (b == IAC) { _sbData.Add(0xFF); _state = ParseState.GotSBData; break; }
                _sbData.Add(IAC); _sbData.Add(b); _state = ParseState.GotSBData;
                break;
        }
    }

    private void Negotiate(byte cmd, byte option, List<byte> responses)
    {
        switch (cmd)
        {
            case DO:
                switch (option)
                {
                    case OPT_ECHO:
                        // Server wants us to echo — refuse; let the server echo
                        responses.AddRange([IAC, WONT, OPT_ECHO]);
                        break;
                    case OPT_SGA:
                        responses.AddRange([IAC, WILL, OPT_SGA]);
                        break;
                    case OPT_NAWS:
                        _nawsEnabled = true;
                        responses.AddRange([IAC, WILL, OPT_NAWS]);
                        AppendNaws(responses, _cols, _rows);
                        break;
                    default:
                        responses.AddRange([IAC, WONT, option]);
                        break;
                }
                break;

            case WILL:
                switch (option)
                {
                    case OPT_ECHO: responses.AddRange([IAC, DO,   OPT_ECHO]); break;
                    case OPT_SGA:  responses.AddRange([IAC, DO,   OPT_SGA]);  break;
                    default:       responses.AddRange([IAC, DONT, option]);   break;
                }
                break;

            case DONT:
                responses.AddRange([IAC, WONT, option]);
                if (option == OPT_NAWS) _nawsEnabled = false;
                break;

            case WONT:
                responses.AddRange([IAC, DONT, option]);
                break;
        }
    }

    private void HandleSubneg(List<byte> data)
    {
        if (data.Count > 0)
            _logger.LogDebug("Telnet subnegotiation for option {Option}", data[0]);
    }

    private static void AppendNaws(List<byte> buf, uint cols, uint rows)
    {
        buf.AddRange([IAC, SB, OPT_NAWS,
            (byte)(cols >> 8), (byte)(cols & 0xFF),
            (byte)(rows >> 8), (byte)(rows & 0xFF),
            IAC, SE]);
    }

    /// <summary>Sends a NAWS subnegotiation if the server has negotiated NAWS.</summary>
    public void SendWindowSize(uint cols, uint rows)
    {
        _cols = cols;
        _rows = rows;
        if (!_nawsEnabled) return;

        var buf = new List<byte>(9);
        AppendNaws(buf, cols, rows);
        _inner.Write(buf.ToArray());
    }

    // ── Write path ────────────────────────────────────────────────────────

    public override void Write(byte[] buffer, int offset, int count)
        => WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        // Escape any 0xFF bytes as IAC IAC
        bool hasIAC = false;
        foreach (byte b in buffer.Span)
            if (b == 0xFF) { hasIAC = true; break; }

        if (!hasIAC) { await _inner.WriteAsync(buffer, ct); return; }

        var escaped = new List<byte>(buffer.Length + 4);
        foreach (byte b in buffer.Span) { escaped.Add(b); if (b == 0xFF) escaped.Add(0xFF); }
        await _inner.WriteAsync(escaped.ToArray(), ct);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    // Expose for tests
    internal bool NawsEnabled => _nawsEnabled;
}
