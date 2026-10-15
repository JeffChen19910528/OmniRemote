# Architecture

## Overview

OmniRemote is a cross-platform remote access client built on .NET 10 and Avalonia UI.
It brings SSH terminal, SFTP file transfer, Telnet, and RDP remote desktop into one desktop application.

## Layer diagram

```
┌─────────────────────────────────────────────┐
│  OmniRemote.App  (Avalonia MVVM UI)          │
│  ViewModels ←→ Views (AXAML)                 │
└──────┬───────────────────────────────────────┘
       │ DI (IServiceProvider)
       ▼
┌─────────────────────────────────────────────┐
│  OmniRemote.Protocols.Abstractions           │
│  IRemoteProtocolAdapter  IRemoteSession      │
│  IConnectionProfileStore ICredentialStore    │
│  IHostIdentityPolicy ICertificateValidation  │
│  ISessionLogRedactor                         │
└──────┬──────┬──────┬──────┬─────────────────┘
       │      │      │      │
   Ssh │  Sftp│Telnet│  Rdp │
       ▼      ▼      ▼      ▼
┌──────────────────────────────────────────────┐
│  Protocol adapters (each in own project)      │
│  SSH.NET  SSH.NET  TcpClient  FreeRDP/mstsc   │
└──────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────┐
│  OmniRemote.Infrastructure                   │
│  SqliteConnectionProfileStore                │
│  WindowsCredentialStore  (DPAPI)             │
│  FileBasedHostIdentityPolicy                 │
│  DefaultCertificateValidationPolicy          │
│  SessionLogRedactor                          │
└──────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────┐
│  OmniRemote.Core                             │
│  Domain models: ConnectionProfile subclasses │
│  Enums: ProtocolType, ConnectionState        │
└─────────────────────────────────────────────┘
```

## Key design decisions

**Protocol isolation:** Each protocol lives in its own project with no cross-protocol dependencies.
New protocols are added by implementing `IRemoteProtocolAdapter` + `IRemoteSession` and registering them in DI.

**Secret storage:** Passwords and passphrases never touch SQLite or JSON.
`ICredentialStore` maps to Windows Credential Manager (DPAPI) on Windows.
The key scheme is `OmniRemote:{protocol}:{profileId}`.

**Host key policy:** SSH uses `IHostIdentityPolicy` backed by a flat known_hosts file (SHA-256 fingerprints).
This is entirely separate from `ICertificateValidationPolicy` (used for TLS protocols like RDP).

**Certificate bypass:** `BypassCertificateValidation` is a per-profile `bool`, default `false`.
`DefaultCertificateValidationPolicy.Validate()` is the single place that reads this flag.
It never affects other profiles or system-wide TLS trust.

**RDP backend:** `RdpSession` launches mstsc (Windows) or xfreerdp (Linux) via `ProcessStartInfo.ArgumentList`.
`RdpBackendLocator` checks for the binary before attempting a connection; if absent it fails with a clear message.

**Log redaction:** All log writes pass through `ISessionLogRedactor` which strips `password=...` and `token=...` patterns before output.

## Data flow for a connection

1. User selects a saved `ConnectionProfile` from the connection manager.
2. App resolves `IRemoteProtocolAdapter` for the profile's `ProtocolType`.
3. Adapter calls `ICredentialStore.LoadAsync` to retrieve the secret.
4. Session is created and `ConnectAsync` is called.
5. For SSH/SFTP: `IHostIdentityPolicy.CheckHostKeyAsync` runs on key receipt; if `Unknown` the UI must prompt the user before proceeding.
6. `StateChanged` events flow back to the ViewModel which updates the UI.

## Storage locations (Windows)

| Data | Location |
|------|---------|
| Connection profiles (no secrets) | `%APPDATA%\OmniRemote\profiles.db` |
| Known SSH host keys | `%APPDATA%\OmniRemote\known_hosts` |
| Secrets (passwords, passphrases) | Windows Credential Manager |
