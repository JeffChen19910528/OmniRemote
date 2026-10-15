# Testing

## Running tests

```bash
# All tests
dotnet test OmniRemote.sln

# Single test project
dotnet test tests/OmniRemote.Core.Tests/

# Single test by name
dotnet test tests/OmniRemote.Core.Tests/ --filter "FullyQualifiedName~MyTest"

# With detailed output
dotnet test OmniRemote.sln --logger "console;verbosity=detailed"
```

## Test categories

| Category | Project | Requirements |
|----------|---------|-------------|
| Unit | OmniRemote.Core.Tests, OmniRemote.Protocols.Tests | None |
| Integration | OmniRemote.Infrastructure.Tests | SQLite on disk |
| SSH/SFTP integration | OmniRemote.Protocols.Tests | Local SSH server (e.g. OpenSSH on loopback) |
| Platform acceptance | OmniRemote.App.Tests | OS credential store |

Tests that cannot run due to missing dependencies, OS, or service must be marked **BLOCKED** in output — not skipped silently.

## Security test requirements

- Verify `BypassCertificateValidation = false` is the default and that `true` affects only the targeted profile.
- Verify SSH host-key policy rejects changed keys.
- Verify secrets are never returned by `IConnectionProfileStore.GetAllAsync`.
- Verify `SessionLogRedactor` strips password and token patterns.
- Verify `RdpSession.BuildArguments` uses `ArgumentList`, not string concatenation.
