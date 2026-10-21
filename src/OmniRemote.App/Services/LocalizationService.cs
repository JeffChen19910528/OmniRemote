using CommunityToolkit.Mvvm.ComponentModel;

namespace OmniRemote.App.Services;

public enum AppLanguage { English, TraditionalChinese }

public sealed partial class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    [ObservableProperty]
    public partial AppLanguage Language { get; set; } = AppLanguage.English;

    partial void OnLanguageChanged(AppLanguage value) =>
        OnPropertyChanged(string.Empty); // refreshes every bound string at once

    private bool IsChinese => Language == AppLanguage.TraditionalChinese;

    // ── Title bar ─────────────────────────────────────────────────────────

    public string LanguageToggleLabel => IsChinese ? "English" : "中文";

    // ── Left panel ────────────────────────────────────────────────────────

    public string SearchPlaceholder        => IsChinese ? "搜尋連線…"                         : "Search connections…";
    public string QuickConnectHeader       => IsChinese ? "快速連線"                           : "Quick Connect";
    public string HostPlaceholder          => IsChinese ? "主機"                               : "Host";
    public string UsernamePlaceholder      => IsChinese ? "使用者名稱"                         : "Username";
    public string PasswordPlaceholder      => IsChinese ? "密碼"                               : "Password";
    public string RememberProfilePassword  => IsChinese ? "記住（設定檔 + 密碼）"               : "Remember (profile + password)";
    public string ConnectButton            => IsChinese ? "連線"                               : "Connect";
    public string NewProfileButton         => IsChinese ? "+ 新增設定檔"                       : "+ New Profile";
    public string ReloadProfiles           => IsChinese ? "重新載入設定檔"                     : "Reload Profiles";

    public string BypassWarningTooltip     => IsChinese ? "此設定檔已降低安全性驗證"             : "Security validation is weakened for this profile";
    public string SessionBypassTooltip     => IsChinese ? "此連線已降低安全性驗證"               : "Security validation is weakened for this session";

    // ── Workspace / empty state ───────────────────────────────────────────

    public string NoActiveSessions         => IsChinese ? "沒有進行中的連線"                   : "No active sessions";
    public string NoActiveSessionsHint     => IsChinese ? "使用快速連線或選擇已儲存的設定檔以開始連線。" : "Use Quick Connect or select a saved profile to start a session.";
    public string SessionBypassBanner      => IsChinese
        ? "⚠  此連線已降低安全性驗證，主機金鑰或憑證驗證已停用。"
        : "⚠  Security validation is weakened for this session. Host-key or certificate checking is disabled.";

    // ── Profile editor ────────────────────────────────────────────────────

    public string ConnectionProfileTitle   => IsChinese ? "連線設定檔"                         : "Connection Profile";
    public string ProtocolLabel            => IsChinese ? "通訊協定"                           : "PROTOCOL";
    public string NameLabel                => IsChinese ? "名稱"                               : "NAME";
    public string HostLabel                => IsChinese ? "主機"                               : "HOST";
    public string PortLabel                => IsChinese ? "埠號"                               : "PORT";
    public string UsernameLabel            => IsChinese ? "使用者名稱"                         : "USERNAME";
    public string PasswordLabel            => IsChinese ? "密碼"                               : "PASSWORD";
    public string DescriptionLabel         => IsChinese ? "描述（選填）"                       : "DESCRIPTION (OPTIONAL)";
    public string SecurityLabel            => IsChinese ? "安全性"                             : "SECURITY";

    public string NameEditorPlaceholder    => IsChinese ? "例如：正式伺服器"                   : "e.g. Production Web Server";
    public string HostEditorPlaceholder    => IsChinese ? "主機名稱或 IP"                      : "hostname or IP";
    public string UsernameEditorPlaceholder => IsChinese ? "使用者名稱"                        : "username";
    public string PasswordEditorPlaceholder => IsChinese ? "留空以保留已儲存的密碼"             : "leave empty to keep the saved password";
    public string DescriptionPlaceholder   => IsChinese ? "選填備註"                           : "optional note";

    public string RememberPassword         => IsChinese ? "記住密碼（儲存於作業系統金鑰圈）"    : "Remember password (stored in the OS keychain)";
    public string PasswordAlreadySaved     => IsChinese ? "此設定檔已有儲存的密碼。"            : "A password is already saved for this profile.";

    public string BypassTlsCert            => IsChinese ? "略過 TLS 憑證驗證"                  : "Bypass TLS certificate validation";
    public string TlsDisabledWarning       => IsChinese ? "⚠  此設定檔已停用 TLS 憑證驗證。"   : "⚠  TLS certificate validation is DISABLED for this profile.";
    public string WarningHeader            => IsChinese ? "⚠  警告"                            : "⚠  WARNING";
    public string TlsWarningDetail         => IsChinese
        ? "停用 TLS 憑證驗證將使此連線暴露於中間人攻擊風險。伺服器身分將不會被驗證。請僅在隔離的可信任網路上使用。"
        : "Disabling TLS certificate validation exposes this connection to man-in-the-middle attacks. The server's identity will NOT be verified. Only use this on isolated, trusted networks.";
    public string ConfirmDisableTls        => IsChinese ? "我了解風險 — 停用驗證"               : "I understand — disable validation";

    public string DisableStrictHostKey     => IsChinese ? "停用嚴格主機金鑰驗證"               : "Disable strict host-key checking";
    public string HostKeyDisabledWarning   => IsChinese ? "⚠  此設定檔已停用嚴格主機金鑰驗證。" : "⚠  Strict host-key checking is DISABLED for this profile.";
    public string HostKeyWarningDetail     => IsChinese
        ? "停用嚴格主機金鑰驗證將使此連線暴露於中間人攻擊風險。伺服器身分將不會與 known_hosts 進行驗證。請僅在無法取得正確主機金鑰時使用。"
        : "Disabling strict host-key checking exposes this connection to man-in-the-middle attacks. The server's identity will NOT be verified against known hosts. Only use this when you cannot obtain the correct host key.";
    public string ConfirmDisableHostKey    => IsChinese ? "我了解風險 — 停用驗證"               : "I understand — disable checking";

    // ── Host key dialog ───────────────────────────────────────────────────

    public string HostKeyDialogTitle      => IsChinese ? "未知主機金鑰"                          : "Unknown Host Key";
    public string HostKeyDialogTrustButton  => IsChinese ? "信任"                               : "Trust";
    public string HostKeyDialogRejectButton => IsChinese ? "拒絕"                               : "Reject";

    public string HostKeyDialogMessage(string host, int port, string keyType) => IsChinese
        ? $"無法自動驗證 {host}:{port} 的主機金鑰（{keyType}）。\n指紋如下。您要信任並儲存此金鑰嗎？"
        : $"The host key for {host}:{port} ({keyType}) could not be automatically verified.\nFingerprint is shown below. Do you want to trust and save this key?";

    public string CancelButton             => IsChinese ? "取消"                               : "Cancel";
    public string SaveProfileButton        => IsChinese ? "儲存設定檔"                         : "Save Profile";

    // ── RDP display settings ──────────────────────────────────────────────

    public string RdpDisplayLabel          => IsChinese ? "顯示設定"                           : "DISPLAY";
    public string RdpFullscreenLabel       => IsChinese ? "全螢幕"                             : "Fullscreen";
    public string RdpResolutionLabel       => IsChinese ? "解析度"                             : "RESOLUTION";
    public string RdpWidthLabel            => IsChinese ? "寬度"                               : "Width";
    public string RdpHeightLabel           => IsChinese ? "高度"                               : "Height";

    // ── Validation errors ─────────────────────────────────────────────────

    public string ErrorNameRequired        => IsChinese ? "名稱為必填項目。"                   : "Name is required.";
    public string ErrorHostRequired        => IsChinese ? "主機為必填項目。"                   : "Host is required.";
    public string ErrorUsernameRequired    => IsChinese ? "此通訊協定需要使用者名稱。"           : "Username is required for this protocol.";
    public string ErrorPortRange           => IsChinese ? "埠號必須介於 1 到 65535 之間。"      : "Port must be between 1 and 65535.";

    // ── Status bar messages ───────────────────────────────────────────────

    public string StatusReady              => IsChinese ? "OmniRemote 已就緒。"                : "OmniRemote ready.";
    public string StatusSessionClosed      => IsChinese ? "連線已關閉。"                       : "Session closed.";

    public string StatusProfilesLoaded(int count) => IsChinese
        ? $"已載入 {count} 個設定檔。"
        : $"{count} profile(s) loaded.";

    public string StatusProfileSaved(string name) => IsChinese
        ? $"設定檔 '{name}' 已儲存。"
        : $"Profile '{name}' saved.";

    public string StatusOpening(string name) => IsChinese
        ? $"正在開啟 {name}…"
        : $"Opening {name}…";

    public string StatusConnected(string name) => IsChinese
        ? $"已連線：{name}"
        : $"Connected: {name}";

    public string StatusConnectFailed(string? err) => IsChinese
        ? $"連線失敗：{err ?? "未知錯誤"}"
        : $"Failed: {err ?? "unknown error"}";

    public string StatusNoAdapter(string proto) => IsChinese
        ? $"找不到通訊協定 '{proto}' 的適配器。"
        : $"No adapter found for protocol '{proto}'.";

    public string StatusLoadProfilesFailed(string msg) => IsChinese
        ? $"載入設定檔失敗：{msg}"
        : $"Failed to load profiles: {msg}";

    public string StatusSaveProfileFailed(string msg) => IsChinese
        ? $"儲存設定檔失敗：{msg}"
        : $"Failed to save profile: {msg}";

    public string StatusSavePasswordFailed(string msg) => IsChinese
        ? $"無法儲存密碼：{msg}"
        : $"Could not save password: {msg}";

    public string StatusCreateSessionFailed(string msg) => IsChinese
        ? $"建立連線失敗：{msg}"
        : $"Failed to create session: {msg}";
}
