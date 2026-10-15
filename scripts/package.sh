#!/usr/bin/env bash
# Build self-contained OmniRemote packages for Windows, Linux and macOS.
# Runs on Linux, macOS, or Windows (Git Bash). .NET publish cross-compiles, so one
# machine can produce every target.
#
#   scripts/package.sh                    # all targets
#   scripts/package.sh win-x64 linux-x64  # selected targets
#   VERSION=1.2.0 scripts/package.sh
#
# Output: dist/OmniRemote-<version>-<rid>.{zip|tar.gz}
#
# Bundled RDP backend (optional but recommended): put a FreeRDP build for each platform in
#   third_party/freerdp/<rid>/   (e.g. sdl-freerdp.exe + its DLLs, or sdl-freerdp + libs)
# and it is copied to <package>/rdp/, where the app picks it up before anything on PATH.
# See third_party/freerdp/README.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/OmniRemote.App/OmniRemote.App.csproj"
VERSION="${VERSION:-1.0.0}"
DIST="$ROOT/dist"
STAGE="$ROOT/obj/package"

ALL_TARGETS=(win-x64 linux-x64 osx-x64 osx-arm64)
TARGETS=("$@")
[ ${#TARGETS[@]} -eq 0 ] && TARGETS=("${ALL_TARGETS[@]}")

make_zip() { # <dir-to-zip-contents-of> <out.zip>
  local src="$1" out="$2"
  rm -f "$out"
  if command -v zip >/dev/null 2>&1; then
    (cd "$src" && zip -qr "$out" .)
  else
    tar -a -cf "$out" -C "$src" .   # bsdtar (Windows 10+, macOS) writes zip from the extension
  fi
}

mac_bundle() { # <publish-dir> <app-dir>
  local pub="$1" app="$2"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp -R "$pub"/. "$app/Contents/MacOS/"
  cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>OmniRemote</string>
  <key>CFBundleDisplayName</key><string>OmniRemote</string>
  <key>CFBundleIdentifier</key><string>com.omniremote.app</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>OmniRemote.App</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
}

rm -rf "$STAGE"
mkdir -p "$DIST"

for rid in "${TARGETS[@]}"; do
  echo "==> $rid"
  pub="$STAGE/$rid/publish"
  dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None -p:Version="$VERSION" -o "$pub" --nologo -v quiet

  find "$pub" -name '*.pdb' -delete   # debug symbols only bloat the download

  # Bundle FreeRDP if the maintainer staged one for this platform.
  if [ -d "$ROOT/third_party/freerdp/$rid" ]; then
    mkdir -p "$pub/rdp"
    cp -R "$ROOT/third_party/freerdp/$rid"/. "$pub/rdp/"
    echo "    bundled FreeRDP from third_party/freerdp/$rid"
  elif [[ "$rid" != win-* ]]; then
    echo "    NOTE: no bundled FreeRDP for $rid; RDP needs a system xfreerdp"
  else
    echo "    NOTE: no bundled FreeRDP; Windows falls back to built-in mstsc.exe"
  fi

  base="OmniRemote-$VERSION-$rid"
  case "$rid" in
    win-*)
      make_zip "$pub" "$DIST/$base.zip" ;;
    linux-*)
      chmod +x "$pub/OmniRemote.App" 2>/dev/null || true
      tar -czf "$DIST/$base.tar.gz" -C "$pub" . ;;
    osx-*)
      app="$STAGE/$rid/OmniRemote.app"
      mac_bundle "$pub" "$app"
      chmod +x "$app/Contents/MacOS/OmniRemote.App" 2>/dev/null || true
      tar -czf "$DIST/$base.tar.gz" -C "$STAGE/$rid" OmniRemote.app ;;
  esac
done

echo; echo "Packages in $DIST:"; ls -lh "$DIST"
