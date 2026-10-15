# Protocol Support

| Protocol | Project | Backend | Default Port | Status |
|----------|---------|---------|-------------|--------|
| SSH | OmniRemote.Protocols.Ssh | SSH.NET 2026.0.0 | 22 | Phase 3 target |
| SFTP | OmniRemote.Protocols.Sftp | SSH.NET 2026.0.0 | 22 | Phase 4 target |
| Telnet | OmniRemote.Protocols.Telnet | System.Net.Sockets | 23 | Phase 5 target |
| RDP | OmniRemote.Protocols.Rdp | mstsc (Win) / xfreerdp (Linux) | 3389 | Phase 6 target |

## Platform notes

**Windows:** mstsc.exe is the default RDP backend, located via `%SystemRoot%\System32\mstsc.exe`.

**Linux:** xfreerdp or xfreerdp3 must be installed separately (`apt install freerdp2-x11`).
If neither is found `RdpBackendLocator` returns null and the connection shows an unsupported state.

**macOS:** Not a primary target; RDP backend not tested.

## Adding a new protocol

1. Create `OmniRemote.Protocols.<Name>` project under `src/`.
2. Add a profile model in `OmniRemote.Core/Models/`.
3. Implement `IRemoteProtocolAdapter` and `IRemoteSession`.
4. Register the adapter in `App.axaml.cs` `ConfigureServices`.
5. Add a protocol-specific connection form in the UI (Phase 7+).
