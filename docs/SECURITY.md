# Security

## Threat model

OmniRemote manages credentials and live connections to remote systems.
Primary threats: credential exposure in storage or logs, host impersonation, and command injection.

## Secret handling

- Passwords, private key passphrases, and tokens are stored exclusively in Windows Credential Manager (DPAPI) via `WindowsCredentialStore`.
- SQLite (`profiles.db`) stores connection metadata only — never secrets.
- Exported profiles omit secrets entirely.
- If OS secret storage is unavailable the app throws `PlatformNotSupportedException` rather than silently falling back to plaintext.

## SSH host identity

- `FileBasedHostIdentityPolicy` records SHA-256 fingerprints in `known_hosts` (tab-delimited, per host:port:keyType).
- A changed fingerprint returns `HostKeyTrustResult.Changed`; the connection is rejected and the user is warned.
- An unknown key returns `HostKeyTrustResult.Unknown`; the UI must ask the user before `TrustHostKeyAsync` is called.
- `StrictHostKeyChecking` defaults to `true` on all SSH and SFTP profiles.

## TLS certificate validation

- `DefaultCertificateValidationPolicy` is the single source of truth for TLS trust decisions.
- `BypassCertificateValidation` is per-profile, defaults to `false`.
- When bypass is active, a warning is logged at `Warning` level and the UI must display a persistent indicator.
- Bypass never affects other profiles or global .NET TLS settings.
- SSH host-key trust is a completely separate flow; `BypassCertificateValidation` has no effect on SSH.

## Telnet

- Telnet sessions log `TelnetSession.UnencryptedWarning` at startup.
- The session `DisplayName` includes `[UNENCRYPTED]` so the UI can always show the warning.
- Telnet must never be presented as a secure transport.

## RDP process invocation

- `RdpSession.BuildArguments` uses `ProcessStartInfo.ArgumentList` exclusively.
- User-controlled strings (host, username, domain) are passed as individual items — never concatenated into a shell string.
- If the backend binary is missing, `RdpBackendLocator.FindBackend()` returns null and the connection fails with a clear error.

## Log redaction

- `SessionLogRedactor` strips `password=...` and `token=...` patterns from all log messages.
- Exception messages are redacted before logging.
- Private key material is never logged.

## Reporting

Report security issues to the project maintainers privately before public disclosure.
