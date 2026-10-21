# Build self-contained OmniRemote packages.
# Runs natively on Windows PowerShell 5.1+ or PowerShell 7+.
# .NET publish cross-compiles, so one machine can produce every target.
#
#   scripts\package.ps1                           # all targets
#   scripts\package.ps1 win-x64                   # Windows only
#   scripts\package.ps1 win-x64 linux-x64         # selected targets
#   $env:VERSION="1.2.0"; scripts\package.ps1
#
# Output: dist\OmniRemote-<version>-<rid>.{zip|tar.gz}
#
# Bundled RDP backend (optional): put a FreeRDP build for each platform in
#   third_party\freerdp\<rid>\  (sdl-freerdp.exe + DLLs, or sdl-freerdp + libs)
# and it is copied to <package>\rdp\, where the app picks it up before PATH.

param(
    [Parameter(ValueFromRemainingArguments)]
    [string[]] $Targets
)

$ErrorActionPreference = 'Stop'

$Root    = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $Root 'src\OmniRemote.App\OmniRemote.App.csproj'
$Version = if ($env:VERSION) { $env:VERSION } else { '1.0.0' }
$Dist    = Join-Path $Root 'dist'
$Stage   = Join-Path $Root 'obj\package'

$AllTargets = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')
if (-not $Targets -or $Targets.Count -eq 0) { $Targets = $AllTargets }

if (-not (Test-Path $Dist))  { New-Item -ItemType Directory -Path $Dist  | Out-Null }
if (Test-Path $Stage)        { Remove-Item -Recurse -Force $Stage }

foreach ($rid in $Targets) {
    Write-Host "==> $rid" -ForegroundColor Cyan
    $pub = Join-Path $Stage "$rid\publish"

    dotnet publish $Project -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:Version=$Version -o $pub --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }

    # Remove debug symbols
    Get-ChildItem -Path $pub -Filter '*.pdb' -Recurse | Remove-Item -Force

    # Bundle FreeRDP if staged
    $rdpSrc = Join-Path $Root "third_party\freerdp\$rid"
    if (Test-Path $rdpSrc) {
        $rdpDst = Join-Path $pub 'rdp'
        New-Item -ItemType Directory -Path $rdpDst -Force | Out-Null
        Copy-Item -Recurse -Force "$rdpSrc\*" $rdpDst
        Write-Host "    bundled FreeRDP from third_party\freerdp\$rid" -ForegroundColor Green
    } elseif ($rid -like 'win-*') {
        Write-Host "    NOTE: no bundled FreeRDP; Windows falls back to built-in mstsc.exe" -ForegroundColor Yellow
    } else {
        Write-Host "    NOTE: no bundled FreeRDP for $rid; RDP needs a system xfreerdp" -ForegroundColor Yellow
    }

    $base = "OmniRemote-$Version-$rid"

    switch -Wildcard ($rid) {
        'win-*' {
            # Rename the single-file exe so the zip entry is recognisable
            $exeSrc = Join-Path $pub 'OmniRemote.App.exe'
            $exeDst = Join-Path $pub 'OmniRemote.exe'
            if ((Test-Path $exeSrc) -and -not (Test-Path $exeDst)) {
                Rename-Item $exeSrc $exeDst
            }

            $zip = Join-Path $Dist "$base.zip"
            if (Test-Path $zip) { Remove-Item $zip }
            Compress-Archive -Path "$pub\*" -DestinationPath $zip
            $sz = (Get-Item $zip).Length / 1MB
            Write-Host ("    -> dist\$base.zip  ({0:F1} MB)" -f $sz) -ForegroundColor Green
        }
        'linux-*' {
            $tar = Join-Path $Dist "$base.tar.gz"
            # Use dotnet's cross-platform tar when available, else fall through to WSL/git-bash
            if (Get-Command tar -ErrorAction SilentlyContinue) {
                tar -czf $tar -C $pub .
            } else {
                throw "tar not found — install Windows Subsystem for Linux or Git for Windows to produce $tar"
            }
            $sz = (Get-Item $tar).Length / 1MB
            Write-Host ("    -> dist\$base.tar.gz  ({0:F1} MB)" -f $sz) -ForegroundColor Green
        }
        'osx-*' {
            $appDir  = Join-Path $Stage "$rid\OmniRemote.app"
            $macOS   = Join-Path $appDir 'Contents\MacOS'
            $res     = Join-Path $appDir 'Contents\Resources'
            New-Item -ItemType Directory -Path $macOS, $res -Force | Out-Null
            Copy-Item -Recurse -Force "$pub\*" "$macOS\"

            @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>OmniRemote</string>
  <key>CFBundleDisplayName</key><string>OmniRemote</string>
  <key>CFBundleIdentifier</key><string>com.omniremote.app</string>
  <key>CFBundleVersion</key><string>$Version</string>
  <key>CFBundleShortVersionString</key><string>$Version</string>
  <key>CFBundleExecutable</key><string>OmniRemote.App</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
"@ | Set-Content -Encoding UTF8 (Join-Path $appDir 'Contents\Info.plist')

            $tar = Join-Path $Dist "$base.tar.gz"
            if (Get-Command tar -ErrorAction SilentlyContinue) {
                tar -czf $tar -C (Join-Path $Stage $rid) OmniRemote.app
            } else {
                throw "tar not found — install Git for Windows to produce $tar"
            }
            $sz = (Get-Item $tar).Length / 1MB
            Write-Host ("    -> dist\$base.tar.gz  ({0:F1} MB)" -f $sz) -ForegroundColor Green
        }
    }
}

Write-Host "`nPackages in dist\:" -ForegroundColor Cyan
Get-ChildItem $Dist | Select-Object Name, @{N='Size';E={'{0:F1} MB' -f ($_.Length/1MB)}} | Format-Table -AutoSize
