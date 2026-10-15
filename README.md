# OmniRemote

**English** | [中文](#中文)

---

## English

OmniRemote is an open-source, cross-platform remote access client for Windows and Linux.
It brings SSH terminal sessions, SFTP file transfers, Telnet, and RDP remote desktop into a single unified desktop application built on .NET 10 and Avalonia UI.

### Features

| Protocol | Description | Default Port |
|----------|-------------|-------------|
| SSH | Encrypted terminal with host-key verification and known-hosts management | 22 |
| SFTP | Secure file transfer — browse, upload, download with progress tracking | 22 |
| Telnet | Legacy terminal access with a persistent unencrypted-traffic warning | 23 |
| RDP | Remote Desktop via mstsc (Windows) or xfreerdp (Linux) | 3389 |

- **Tabbed workspace** — manage multiple sessions side by side
- **OS-backed credential storage** — passwords and private-key passphrases are stored in Windows Credential Manager; never in plain text
- **Per-profile security controls** — TLS certificate bypass and SSH host-key policy are opt-in per connection, never global
- **Session log redaction** — passwords and tokens are automatically stripped from all log output

### Platform support

| Platform | SSH/SFTP/Telnet | RDP |
|----------|----------------|-----|
| Windows 10 / 11 / Server 2019+ | Yes | Built-in (`mstsc.exe`) |
| Linux (X11 or Wayland) | Yes | Requires `freerdp3` package |

### Prerequisites

- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or SDK to build from source)
- **Linux RDP only:** `sudo apt install freerdp3-x11` (Debian/Ubuntu) or equivalent

### Build from source

```bash
# Clone the repository
git clone <repository-url>
cd OmniRemote

# Restore and build
dotnet build OmniRemote.slnx

# Run all tests
dotnet test OmniRemote.slnx

# Run the application (debug)
dotnet run --project src/OmniRemote.App/OmniRemote.App.csproj
```

### Publish (self-contained single file)

```bash
# Windows x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj /p:PublishProfile=win-x64

# Linux x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj /p:PublishProfile=linux-x64
```

Output is placed in `publish/win-x64/` or `publish/linux-x64/` at the solution root.

### How to use

1. **Launch OmniRemote.**
2. Click **New Profile** in the left panel and fill in the connection details (host, port, username, protocol).
3. Your password or passphrase is saved to the OS credential store — it is never written to disk in plain text.
4. Click the **connect (▶)** button next to a saved profile to open a session tab.
5. For SSH/SFTP connections the host key is verified on first connect; you will be prompted to accept or reject it.
6. Manage multiple sessions using the tab bar at the top. A **⚠** badge appears on any tab where a security bypass is active.

### Security notes

- Certificate validation and SSH host-key checking are **on by default**.
- To enable a bypass (e.g., self-signed certificate for RDP), open the profile editor and confirm the warning dialog. Bypass is stored per-profile only.
- Telnet sessions always display a red **[UNENCRYPTED]** banner — use SSH instead whenever possible.

### Project structure

```
src/
  OmniRemote.App/                    Avalonia UI, ViewModels, DI bootstrap
  OmniRemote.Core/                   Domain models, validation
  OmniRemote.Infrastructure/         SQLite profiles, credential store, log redaction
  OmniRemote.Protocols.Abstractions/ Shared interfaces (IRemoteSession, etc.)
  OmniRemote.Protocols.Ssh/          SSH adapter (SSH.NET)
  OmniRemote.Protocols.Sftp/         SFTP adapter (SSH.NET)
  OmniRemote.Protocols.Telnet/       Telnet adapter
  OmniRemote.Protocols.Rdp/          RDP adapter (mstsc / xfreerdp)
tests/                               xUnit test projects (224 tests)
```

---

## 中文

OmniRemote 是一款開源的跨平台遠端存取客戶端，支援 Windows 與 Linux。
它將 SSH 終端機連線、SFTP 檔案傳輸、Telnet 以及 RDP 遠端桌面整合到一個統一的桌面應用程式中，基於 .NET 10 與 Avalonia UI 開發。

### 功能特色

| 通訊協定 | 說明 | 預設埠號 |
|----------|------|---------|
| SSH | 加密終端機，支援主機金鑰驗證與 known_hosts 管理 | 22 |
| SFTP | 安全檔案傳輸，支援瀏覽目錄、上傳、下載及進度顯示 | 22 |
| Telnet | 傳統終端機存取，連線期間持續顯示未加密警告 | 23 |
| RDP | 遠端桌面，Windows 使用 mstsc，Linux 使用 xfreerdp | 3389 |

- **分頁式工作區** — 同時管理多個遠端連線
- **作業系統層級憑證儲存** — 密碼與私鑰密語儲存於 Windows 憑證管理員，絕不以明文寫入磁碟
- **每個連線設定獨立的安全控制** — TLS 憑證略過與 SSH 主機金鑰策略為每個連線設定檔獨立控制，不影響全域設定
- **連線日誌自動遮蔽** — 所有日誌輸出中的密碼與 Token 自動過濾

### 平台支援

| 平台 | SSH / SFTP / Telnet | RDP |
|------|---------------------|-----|
| Windows 10 / 11 / Server 2019+ | 支援 | 內建（`mstsc.exe`） |
| Linux（X11 或 Wayland） | 支援 | 需安裝 `freerdp3` 套件 |

### 環境需求

- [.NET 10 執行環境](https://dotnet.microsoft.com/download/dotnet/10.0)（或 SDK 用於從原始碼建置）
- **Linux RDP 功能：** `sudo apt install freerdp3-x11`（Debian/Ubuntu）或對應套件管理指令

### 從原始碼建置

```bash
# 複製儲存庫
git clone <repository-url>
cd OmniRemote

# 還原相依套件並建置
dotnet build OmniRemote.slnx

# 執行所有測試
dotnet test OmniRemote.slnx

# 以偵錯模式執行應用程式
dotnet run --project src/OmniRemote.App/OmniRemote.App.csproj
```

### 發布（單一可執行檔，自帶執行環境）

```bash
# Windows x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj /p:PublishProfile=win-x64

# Linux x64
dotnet publish src/OmniRemote.App/OmniRemote.App.csproj /p:PublishProfile=linux-x64
```

輸出檔案位於解決方案根目錄下的 `publish/win-x64/` 或 `publish/linux-x64/`。

### 使用方式

1. **啟動 OmniRemote。**
2. 點選左側面板的 **New Profile**，填入主機、埠號、使用者名稱及通訊協定。
3. 密碼或私鑰密語會儲存至作業系統憑證管理員，不會以明文寫入磁碟。
4. 點選已儲存連線設定旁的 **▶（連線）** 按鈕，開啟連線分頁。
5. 首次連線 SSH/SFTP 時會驗證主機金鑰，系統會提示您接受或拒絕。
6. 使用頂部分頁列管理多個連線。若某連線啟用了安全略過，分頁上會顯示 **⚠** 警示圖示。

### 安全性說明

- 憑證驗證與 SSH 主機金鑰驗證**預設為開啟**。
- 若需啟用略過（例如 RDP 使用自簽憑證），請在連線設定編輯器中確認警告對話框。略過設定僅針對該連線設定檔生效。
- Telnet 連線期間會持續顯示紅色 **[UNENCRYPTED]** 警示橫幅，建議在可能的情況下改用 SSH。

### 專案結構

```
src/
  OmniRemote.App/                    Avalonia UI、ViewModel、DI 初始化
  OmniRemote.Core/                   領域模型、資料驗證
  OmniRemote.Infrastructure/         SQLite 連線設定、憑證儲存、日誌遮蔽
  OmniRemote.Protocols.Abstractions/ 共用介面（IRemoteSession 等）
  OmniRemote.Protocols.Ssh/          SSH 適配器（SSH.NET）
  OmniRemote.Protocols.Sftp/         SFTP 適配器（SSH.NET）
  OmniRemote.Protocols.Telnet/       Telnet 適配器
  OmniRemote.Protocols.Rdp/          RDP 適配器（mstsc / xfreerdp）
tests/                               xUnit 測試專案（共 224 個測試）
```
