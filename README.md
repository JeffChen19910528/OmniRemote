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

### Package (self-contained, no .NET install needed)

```bash
scripts/package.sh                     # win-x64, linux-x64, osx-x64, osx-arm64
scripts/package.sh win-x64             # selected targets only
VERSION=1.2.0 scripts/package.sh
```

Works on Linux, macOS, and Windows (Git Bash); output goes to `dist/`. To bundle an RDP client so
users install nothing, see [third_party/freerdp/README.md](third_party/freerdp/README.md).
RDP backend order: bundled FreeRDP (`rdp/` next to the app) → FreeRDP on PATH → Windows `mstsc.exe`.

### Saved passwords

Tick **Remember password** in the profile editor or Quick Connect. Stored in Windows Credential
Manager; on Linux via `secret-tool` (libsecret-tools) with an encrypted-file fallback; on macOS in
an AES-GCM encrypted file under the app data folder.

### What to set up before connecting

OmniRemote is only the client; the machine you connect **to** must accept the connection.

| Target | Protocol | Setup on the target |
|--------|----------|---------------------|
| Windows | RDP (3389) | Settings → System → Remote Desktop → On. Home editions cannot host RDP (Pro/Enterprise/Server only). The login account must be an administrator or in *Remote Desktop Users*, and have a password. |
| Linux | SSH / SFTP (22) | Install and start the SSH server: `sudo apt install openssh-server && sudo systemctl enable --now ssh` |
| Linux | RDP (3389) | Needs an RDP server such as `xrdp`: `sudo apt install xrdp && sudo systemctl enable --now xrdp` |
| macOS | SSH / SFTP (22) | System Settings → General → Sharing → **Remote Login** → On. |
| macOS | Desktop | Not supported: macOS Screen Sharing uses VNC, which OmniRemote does not implement. |
| Any | Telnet (23) | Only for legacy devices; traffic is unencrypted. |

Also check:

- **Firewall / router:** allow the port (22, 3389, 23) on the target. Connecting over the internet needs a
  port forward or, better, a VPN — avoid exposing 3389 directly.
- **Self-signed RDP certificate:** if connection fails on certificate trust, enable *Bypass TLS certificate
  validation* for that profile only (see Security notes).
- **SSH first connect:** accept the host key when prompted; a later "key changed" warning means the server
  was reinstalled or something is intercepting the connection.
- **Client side, RDP:** Windows needs nothing (built-in `mstsc.exe`); Linux needs `xfreerdp`
  (`sudo apt install freerdp3-x11`); macOS needs `brew install freerdp` — unless the package already
  bundles FreeRDP in its `rdp/` folder.
- **Client side, saved passwords on Linux:** `sudo apt install libsecret-tools` to use the system keyring;
  otherwise an encrypted file is used.
- **macOS app:** first launch of the unsigned `.app` requires right-click → Open.

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

### 打包（自帶執行環境，使用者不需安裝 .NET）

```bash
scripts/package.sh                     # win-x64、linux-x64、osx-x64、osx-arm64
scripts/package.sh win-x64             # 只打包指定平台
VERSION=1.2.0 scripts/package.sh
```

可在 Linux、macOS、Windows（Git Bash）執行，輸出在 `dist/`。若要內附 RDP 用戶端、讓使用者免安裝，
請見 [third_party/freerdp/README.md](third_party/freerdp/README.md)。
RDP 後端順序：程式旁 `rdp/` 內附的 FreeRDP → PATH 上的 FreeRDP → Windows 內建 `mstsc.exe`。

### 記住密碼

在連線設定編輯器或 Quick Connect 勾選 **Remember password**。Windows 存於憑證管理員；
Linux 透過 `secret-tool`（libsecret-tools），不可用時退回加密檔；macOS 存於應用程式資料夾內的 AES-GCM 加密檔。

### 連線前需要的設定

OmniRemote 只是用戶端，**被連線的那台機器**必須先允許連線。

| 目標主機 | 協定 | 目標主機需要的設定 |
|----------|------|--------------------|
| Windows | RDP（3389） | 設定 → 系統 → 遠端桌面 → 開啟。家用版無法被 RDP 連入（需專業版／企業版／Server）。登入帳號須為管理員或在「Remote Desktop Users」群組，且帳號要有密碼。 |
| Linux | SSH / SFTP（22） | 安裝並啟動 SSH 伺服器：`sudo apt install openssh-server && sudo systemctl enable --now ssh` |
| Linux | RDP（3389） | 需要 RDP 伺服器，例如 `xrdp`：`sudo apt install xrdp && sudo systemctl enable --now xrdp` |
| macOS | SSH / SFTP（22） | 系統設定 → 一般 → 共享 → 開啟 **遠端登入**。 |
| macOS | 遠端桌面 | 不支援：macOS 螢幕共享使用 VNC，OmniRemote 未實作 VNC。 |
| 任意 | Telnet（23） | 僅供舊設備使用，流量未加密。 |

其他注意事項：

- **防火牆／路由器：** 目標主機需放行對應埠號（22、3389、23）。從外網連線需設定埠轉發，建議改用 VPN，
  避免直接對外開放 3389。
- **RDP 自簽憑證：** 若因憑證不受信任而連線失敗，僅對該連線設定啟用「略過 TLS 憑證驗證」（見安全性說明）。
- **SSH 首次連線：** 提示時確認主機金鑰；之後若出現「金鑰已變更」警告，代表伺服器重灌或連線被攔截。
- **用戶端（RDP）：** Windows 不需額外安裝（內建 `mstsc.exe`）；Linux 需 `xfreerdp`
  （`sudo apt install freerdp3-x11`）；macOS 需 `brew install freerdp`。若安裝包的 `rdp/` 資料夾已內附 FreeRDP 則不需要。
- **用戶端（Linux 記住密碼）：** `sudo apt install libsecret-tools` 可使用系統金鑰圈，否則改用加密檔。
- **macOS 應用程式：** 未簽章的 `.app` 第一次開啟需右鍵 → 開啟。

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
