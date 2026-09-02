@echo off
setlocal enabledelayedexpansion

set "TARGET_DIR=%~dp0Assets\StreamingAssets"

if not exist "!TARGET_DIR!\" (
    echo Error: !TARGET_DIR! not found
    pause
    exit /b 1
)

cd /d "!TARGET_DIR!"

echo Working in: !TARGET_DIR!
echo.

set "NODE_VERSION=20.11.0"
set "NODE_DIR=!TARGET_DIR!\node"
set "NPM_CMD=!NODE_DIR!\npm.cmd"
set "NODE_ZIP_URL=https://nodejs.org/dist/v!NODE_VERSION!/node-v!NODE_VERSION!-win-x64.zip"
set "ZIP_PATH=!TARGET_DIR!\node_download.zip"
set "EXTRACT_TMP=!TARGET_DIR!\node_extract_tmp"

if exist "!NPM_CMD!" (
    echo Node portable already present in !NODE_DIR!, skipping download.
    goto :install
)

echo Node portable not found. Downloading Node v!NODE_VERSION!...
curl -L -o "!ZIP_PATH!" "!NODE_ZIP_URL!"
if errorlevel 1 (
    echo Error: could not download Node zip
    pause
    exit /b 1
)

echo Extracting...
if exist "!EXTRACT_TMP!" rmdir /s /q "!EXTRACT_TMP!"
powershell -NoProfile -Command "Expand-Archive -Path '!ZIP_PATH!' -DestinationPath '!EXTRACT_TMP!' -Force"
if errorlevel 1 (
    echo Error: could not extract Node zip
    pause
    exit /b 1
)

if exist "!NODE_DIR!" rmdir /s /q "!NODE_DIR!"
for /d %%D in ("!EXTRACT_TMP!\node-v!NODE_VERSION!-*") do move "%%D" "!NODE_DIR!" >nul

rmdir /s /q "!EXTRACT_TMP!"
del "!ZIP_PATH!"

if not exist "!NPM_CMD!" (
    echo Error: extraction finished but !NPM_CMD! still not found
    pause
    exit /b 1
)

echo Node portable ready in !NODE_DIR!

:install

echo.
echo Generating package.json...
call "!NPM_CMD!" init -y

echo.
echo Installing dependencies: ws express serve
call "!NPM_CMD!" install ws express serve

echo.
echo Done. node_modules and package.json are ready in this folder.
pause
