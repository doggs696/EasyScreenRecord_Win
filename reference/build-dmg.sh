#!/bin/bash

# EasyScreenRecord - Build DMG Script

set -e

echo "🔨 Building Release..."
xcodebuild -scheme EasyScreenRecord -configuration Release build CONFIGURATION_BUILD_DIR=./build -quiet

echo "📦 Creating DMG..."
rm -f EasyScreenRecord.dmg rw.*.dmg 2>/dev/null

create-dmg \
  --volname "EasyScreenRecord" \
  --window-pos 200 120 \
  --window-size 600 400 \
  --icon-size 100 \
  --icon "EasyScreenRecord.app" 150 185 \
  --hide-extension "EasyScreenRecord.app" \
  --app-drop-link 450 185 \
  "EasyScreenRecord.dmg" \
  "build/EasyScreenRecord.app"

echo ""
echo "✅ Done! EasyScreenRecord.dmg created"
ls -lh EasyScreenRecord.dmg
