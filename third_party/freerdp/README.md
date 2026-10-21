# Bundled FreeRDP

**English** | [中文](#中文)

---

FreeRDP only publishes source releases, so prebuilt binaries are not committed here.
To ship RDP with no user-side install, build FreeRDP 3.x (SDL client) for each platform and
place the result in a folder named after the .NET runtime identifier:

```
third_party/freerdp/win-x64/     sdl-freerdp.exe (or wfreerdp.exe) + its DLLs
third_party/freerdp/linux-x64/   sdl-freerdp (or xfreerdp) + its shared libraries
third_party/freerdp/osx-arm64/   sdl-freerdp + dylibs
third_party/freerdp/osx-x64/     sdl-freerdp + dylibs
```

`scripts/package.sh` and `scripts/package.ps1` copy each folder into `<package>/rdp/`, and
OmniRemote looks there first, before anything on PATH.

Tip: `vcpkg install freerdp` builds the client and gathers its DLLs on Windows.
Pin one FreeRDP version so a system update can never change RDP behaviour.

Without a bundled build: Windows falls back to the built-in `mstsc.exe`; Linux/macOS need a
system `xfreerdp`.

---

## 中文

FreeRDP 只發布原始碼，因此預先編譯的執行檔不會提交至此。
若要讓使用者在不安裝任何套件的情況下使用 RDP，請針對各平台自行編譯 FreeRDP 3.x（SDL 客戶端），
並將結果放入以 .NET 執行環境識別碼命名的資料夾中：

```
third_party/freerdp/win-x64/     sdl-freerdp.exe（或 wfreerdp.exe）+ 相依 DLL
third_party/freerdp/linux-x64/   sdl-freerdp（或 xfreerdp）+ 相依共享函式庫
third_party/freerdp/osx-arm64/   sdl-freerdp + dylib 函式庫
third_party/freerdp/osx-x64/     sdl-freerdp + dylib 函式庫
```

`scripts/package.sh` 與 `scripts/package.ps1` 會將各資料夾複製至 `<安裝包>/rdp/`，
OmniRemote 啟動時會優先在此查找 RDP 後端，再搜尋 PATH。

提示：在 Windows 上可使用 `vcpkg install freerdp` 自動編譯並收集所有 DLL。
建議固定使用特定版本的 FreeRDP，避免系統更新意外改變 RDP 行為。

若未內附 FreeRDP：Windows 會退回使用內建的 `mstsc.exe`；Linux / macOS 則需要系統安裝 `xfreerdp`。
