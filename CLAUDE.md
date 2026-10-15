# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

OmniRemote is a cross-platform remote access client (SSH, SFTP, Telnet, RDP) built with C# on .NET 10 and Avalonia UI, targeting Windows and Linux.

## Technology stack

- **Runtime:** .NET 10, C#
- **UI:** Avalonia UI with MVVM
- **DI/Config/Logging:** Microsoft.Extensions.*
- **SSH/SFTP:** SSH.NET (verify licensing and maintenance before use)
- **RDP:** FreeRDP via a narrowly scoped adapter (do not build from scratch)
- **Persistence:** SQLite for connection metadata; Windows Credential Manager / Linux keyring for secrets
- **Testing:** xUnit

Before adding or updating any dependency, verify its current maintenance status, licensing, security advisories, and platform support. Do not invent package versions.

## Build and test commands

Once the solution exists:

```bash
# Build
dotnet build OmniRemote.slnx

# Run all tests
dotnet test OmniRemote.slnx

# Run a single test project
dotnet test tests/OmniRemote.Core.Tests/

# Run a single test by name
dotnet test tests/OmniRemote.Core.Tests/ --filter "FullyQualifiedName~MyTestMethod"

# Release build
dotnet build OmniRemote.slnx -c Release
```

## Solution layout

```
OmniRemote/
├── OmniRemote.sln
├── src/
│   ├── OmniRemote.App/                      # Avalonia views and view models
│   ├── OmniRemote.Core/                     # Domain models, contracts, policies
│   ├── OmniRemote.Infrastructure/           # Persistence, secret storage, logging
│   ├── OmniRemote.Protocols.Abstractions/   # IRemoteProtocolAdapter, IRemoteSession, etc.
│   ├── OmniRemote.Protocols.Ssh/
│   ├── OmniRemote.Protocols.Sftp/
│   ├── OmniRemote.Protocols.Telnet/
│   └── OmniRemote.Protocols.Rdp/
├── tests/
│   ├── OmniRemote.Core.Tests/
│   ├── OmniRemote.Infrastructure.Tests/
│   ├── OmniRemote.Protocols.Tests/
│   └── OmniRemote.App.Tests/
└── docs/
    ├── ARCHITECTURE.md
    ├── SECURITY.md
    ├── PROTOCOLS.md
    ├── TESTING.md
    └── RELEASE.md
```

## Core contracts (OmniRemote.Protocols.Abstractions)

Key interfaces to keep protocol implementations decoupled from UI:

- `IRemoteProtocolAdapter` — factory for sessions
- `IRemoteSession` — lifecycle: connect, disconnect, dispose
- `IConnectionProfileStore` — CRUD for saved profiles
- `ICredentialStore` — secret storage abstraction
- `IHostIdentityPolicy` — SSH known-hosts trust decisions
- `ICertificateValidationPolicy` — TLS certificate validation per profile
- `ISessionLogRedactor` — strips secrets from log output

## Implementation phases

Work one phase at a time. Do not start a later phase while the current phase has unresolved build or test failures.

0. Repository/environment assessment + `docs/ARCHITECTURE.md` + `docs/SECURITY.md`
1. Solution skeleton: Avalonia app, DI, logging, config, test projects
2. Connection profiles + OS-backed secret storage
3. SSH terminal (auth, host-key verification, known-hosts flow)
4. SFTP (directory ops, upload/download, progress, cancellation)
5. Telnet (with persistent unencrypted-traffic warning)
6. RDP adapter (FreeRDP, safe argument passing, missing-backend handling)
7. Unified workspace (tabs, session switching, search, status indicators)
8. Validation bypass controls (per-profile TLS bypass, SSH host-key policy separate)
9. Hardening and acceptance (static analysis, secret scan, packaging, Windows + Linux)

## Security invariants

These must never be violated:

- Certificate and host-key validation is **on by default**; bypass is never a global setting.
- SSH host-key trust is a separate workflow from TLS certificate bypass.
- Passwords, private keys, and passphrases go to OS credential storage only — never SQLite, JSON, or source code.
- If OS secret storage is unavailable, fail safely; do not silently fall back to plaintext.
- Use `ProcessStartInfo.ArgumentList` for RDP backend invocation; never concatenate user input into a shell command.
- Redact secrets from all logs, exception messages, and diagnostic bundles via `ISessionLogRedactor`.
- Telnet sessions always display a persistent warning that traffic is unencrypted.

## Testing rules

- Never report a test as passing unless it was executed and the result observed.
- Tests that cannot run due to missing OS, service, or privilege must be reported as **BLOCKED**, not PASS.
- Do not weaken production security policies to make a test pass.
- Use local test servers or controlled fixtures; do not connect to external systems in automated tests.
