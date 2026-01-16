#!/bin/bash

# EasyScreenRecord - Build and Run Script

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

# Kill existing process
pkill -x EasyScreenRecord 2>/dev/null

cd "$PROJECT_DIR"

# Parse arguments
CLEAN=false
BUILD=true
for arg in "$@"; do
    case $arg in
        --clean|-c)
            CLEAN=true
            ;;
        --no-build|-n)
            BUILD=false
            ;;
        --help|-h)
            echo "Usage: ./run.sh [options]"
            echo "Options:"
            echo "  -c, --clean     Clean before build"
            echo "  -n, --no-build  Skip build, just run"
            echo "  -h, --help      Show this help"
            exit 0
            ;;
    esac
done

# Clean if requested
if $CLEAN; then
    echo "🧹 Cleaning..."
    xcodebuild -scheme EasyScreenRecord clean -quiet
fi

# Build
if $BUILD; then
    echo "🔨 Building..."
    xcodebuild -scheme EasyScreenRecord -configuration Debug build -quiet
    if [ $? -ne 0 ]; then
        echo "❌ Build failed"
        exit 1
    fi
    echo "✅ Build succeeded"
fi

# Find and run the built app
APP_PATH=$(find ~/Library/Developer/Xcode/DerivedData -name "EasyScreenRecord.app" -path "*/Debug/*" -type d 2>/dev/null | head -1)
if [ -z "$APP_PATH" ]; then
    echo "❌ Could not find built app"
    exit 1
fi

echo "🚀 Starting app..."
open "$APP_PATH"
