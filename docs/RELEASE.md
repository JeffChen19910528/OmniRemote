# RELEASE.md

## Build and test

```bash
# Debug build + all tests
dotnet build OmniRemote.slnx
dotnet test OmniRemote.slnx

# Release build
dotnet build OmniRemote.slnx -c Release
```

## Packaging (single-file self-contained)

```bash
# Windows x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj \
  /p:PublishProfile=win-x64

# Linux x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj \
  /p:PublishProfile=linux-x64
```

Output goes to `publish/win-x64/` and `publish/linux-x64/` at the solution root.

## Runtime prerequisites

| Platform | Requirement |
|----------|-------------|
| Windows  | Windows 10 / Server 2019 or later. `mstsc.exe` for RDP (built-in). |
| Linux    | X11 or Wayland display server. `freerdp3` for RDP (`apt install freerdp3-x11` or equivalent). |

## FreeRDP detection

OmniRemote checks for `xfreerdp` / `xfreerdp3` on `PATH` at runtime. If not found, RDP sessions
open with a `Failed` state and a message pointing to the missing backend. SSH, SFTP, and Telnet
work without any extra dependencies.

## Credential storage

- **Windows**: Windows Credential Manager (`wincred`)
- **Linux**: Secret Service API (`libsecret`) via the system keyring (GNOME Keyring, KWallet)

If the OS credential store is unavailable the application fails safely — it does not fall back
to plaintext storage.

## Security checklist before release

- [ ] Run `dotnet test` — all tests green, no BLOCKED status
- [ ] Verify `HardeningTests.Source_files_contain_no_hardcoded_secrets` passes
- [ ] Confirm `BypassCertificateValidation` defaults to `false` in all profiles
- [ ] Confirm `StrictHostKeyChecking` defaults to `true` in SSH / SFTP profiles
- [ ] Smoke-test SSH with an incorrect host key — connection must be refused
- [ ] Smoke-test Telnet — persistent `[UNENCRYPTED]` warning must be visible throughout session
- [ ] Smoke-test RDP bypass toggle — confirmation panel must appear before bypass activates
- [ ] Review any profiles saved during testing — delete test credentials from credential store
