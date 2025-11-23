@echo off
setlocal

echo Building Terra Invicta Screen Reader Mod...

REM Clean previous build
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

REM Build the project
dotnet build TISpeechMod.csproj --configuration Release --verbosity minimal

REM Check if build was successful
if not exist "bin\Release\net472\TISpeechMod.dll" (
    echo Build failed! DLL not found.
    pause
    exit /b 1
)

REM Deploy to game directory
set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta"
set "GAME_MODS_DIR=%GAME_DIR%\Mods"
echo Deploying to: %GAME_MODS_DIR%

REM Create Mods directory if it doesn't exist
if not exist "%GAME_MODS_DIR%" mkdir "%GAME_MODS_DIR%"

REM Copy the mod DLL
copy "bin\Release\net472\TISpeechMod.dll" "%GAME_MODS_DIR%\"

REM Copy Tolk.dll and NVDA controller to game directory if they exist
if exist "Tolk.dll" copy "Tolk.dll" "%GAME_DIR%\"
if exist "nvdaControllerClient32.dll" copy "nvdaControllerClient32.dll" "%GAME_DIR%\"
if exist "nvdaControllerClient64.dll" copy "nvdaControllerClient64.dll" "%GAME_DIR%\"

echo.
echo Build and deployment complete!
echo Mod DLL copied to: %GAME_MODS_DIR%\TISpeechMod.dll
echo.
echo To use this mod:
echo 1. Make sure MelonLoader is installed in Terra Invicta
echo 2. Ensure Tolk.dll is in the game's main directory
echo 3. Run the game - the mod should load automatically
echo.
echo The mod will announce tooltips and UI elements as you hover over them.
echo.
pause