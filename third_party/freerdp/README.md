# Bundled FreeRDP

FreeRDP only publishes source releases, so prebuilt binaries are not committed here.
To ship RDP with no user-side install, build FreeRDP 3.x (SDL client) for each platform and
place the result in a folder named after the .NET runtime identifier:

```
third_party/freerdp/win-x64/     sdl-freerdp.exe (or wfreerdp.exe) + its DLLs
third_party/freerdp/linux-x64/   sdl-freerdp (or xfreerdp) + its shared libraries
third_party/freerdp/osx-arm64/   sdl-freerdp + dylibs
third_party/freerdp/osx-x64/     sdl-freerdp + dylibs
```

`scripts/package.sh` copies each folder into `<package>/rdp/`, and OmniRemote looks there first,
before anything on PATH. Tip: `vcpkg install freerdp` builds the client and gathers its DLLs on
Windows. Pin one FreeRDP version so a system update can never change RDP behaviour.

Without a bundled build: Windows falls back to the built-in `mstsc.exe`; Linux/macOS need a
system `xfreerdp`.
