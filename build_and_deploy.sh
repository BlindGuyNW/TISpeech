#!/bin/bash

# Terra Invicta Screen Reader Mod Build and Deploy Script
# This script builds the mod and copies it to the game's Mods directory

set -e

echo "Building Terra Invicta Screen Reader Mod..."

# Clean previous build
if [ -d "bin" ]; then
    rm -rf bin
fi

if [ -d "obj" ]; then
    rm -rf obj
fi

# Build the project
dotnet build TISpeechMod.csproj --configuration Release --verbosity minimal

# Check if build was successful
if [ ! -f "bin/Release/net472/TISpeechMod.dll" ]; then
    echo "Build failed! DLL not found."
    exit 1
fi

# Deploy to game directory
GAME_DIR="/mnt/c/Program Files (x86)/Steam/steamapps/common/Terra Invicta"
GAME_MODS_DIR="$GAME_DIR/Mods"
echo "Deploying to: $GAME_MODS_DIR"

# Create Mods directory if it doesn't exist
mkdir -p "$GAME_MODS_DIR"

# Copy the mod DLL
cp "bin/Release/net472/TISpeechMod.dll" "$GAME_MODS_DIR/"

# Copy Tolk.dll and NVDA controller to game directory if they exist
if [ -f "Tolk.dll" ]; then
    cp "Tolk.dll" "$GAME_DIR/"
fi
if [ -f "nvdaControllerClient32.dll" ]; then
    cp "nvdaControllerClient32.dll" "$GAME_DIR/"
fi
if [ -f "nvdaControllerClient64.dll" ]; then
    cp "nvdaControllerClient64.dll" "$GAME_DIR/"
fi

echo "Build and deployment complete!"
echo "Mod DLL copied to: $GAME_MODS_DIR/TISpeechMod.dll"
echo ""
echo "To use this mod:"
echo "1. Make sure MelonLoader is installed in Terra Invicta"
echo "2. Ensure Tolk.dll is in the game's main directory"
echo "3. Run the game - the mod should load automatically"
echo ""
echo "The mod will announce tooltips and UI elements as you hover over them."