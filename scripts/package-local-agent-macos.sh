#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNTIME="${1:-osx-arm64}"
PUBLISH_DIR="$ROOT_DIR/artifacts/publish/Cratebase.LocalAgent/$RUNTIME"
PACKAGE_DIR="$ROOT_DIR/artifacts/package/local-agent-macos"
APP_DIR="$PACKAGE_DIR/Cratebase Local Agent.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
OUTPUT_DIR="$ROOT_DIR/src/Cratebase.Api/local-agent"
DMG_PATH="$OUTPUT_DIR/Cratebase.LocalAgent.dmg"

rm -rf "$PUBLISH_DIR" "$PACKAGE_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR" "$OUTPUT_DIR"

dotnet publish "$ROOT_DIR/src/Cratebase.LocalAgent/Cratebase.LocalAgent.csproj" \
  --configuration Release \
  --runtime "$RUNTIME" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:PublishSingleFile=false

cp -R "$PUBLISH_DIR/"* "$MACOS_DIR/"
chmod +x "$MACOS_DIR/Cratebase.LocalAgent"

cat > "$CONTENTS_DIR/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>Cratebase Local Agent</string>
  <key>CFBundleExecutable</key>
  <string>Cratebase.LocalAgent</string>
  <key>CFBundleIdentifier</key>
  <string>local.cratebase.agent</string>
  <key>CFBundleName</key>
  <string>Cratebase Local Agent</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.0</string>
  <key>CFBundleVersion</key>
  <string>0.1.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
</dict>
</plist>
PLIST

rm -f "$DMG_PATH"
hdiutil create \
  -volname "Cratebase Local Agent" \
  -srcfolder "$APP_DIR" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "$DMG_PATH"
