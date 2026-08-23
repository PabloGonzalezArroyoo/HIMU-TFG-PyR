@echo off
setlocal enabledelayedexpansion
title Streaming server

:: ============================================================
:: -1. Script relaunches itself minimized
:: ============================================================
if /I not "%~1"=="MIN" (
    start "" /min "%~f0" MIN %*
    exit /b
)
shift

:: ============================================================
:: 0. .bat has arguments (ports needed for execution). Example: start-server.bat 8080 3000
:: (They receive default values)
set "WS_PORT=8080"
set "HTTP_PORT=3000"

if not "%~1"=="" set "WS_PORT=%~1"
if not "%~2"=="" set "HTTP_PORT=%~2"

echo WebSocket/Node port is: !WS_PORT!
echo HTML port: !HTTP_PORT!
echo.

:: 1. Checking if Node and necessary modules are installed to run server.js
echo Checking if Node.js is installed...
node -v >nul 2>&1
if !errorlevel! neq 0 (
    echo Node.js was not found. Ready to install: downloading installer...
    curl -o node_installer.msi https://nodejs.org/dist/v20.11.0/node-v20.11.0-x64.msi
    if !errorlevel! neq 0 (
        echo Could not download Node.js
        pause
        exit /b 1
    )
    echo Installing Node.js...
    msiexec /i node_installer.msi /quiet /norestart
    del node_installer.msi

    :: Reload PATH to find Node
    set "PATH=%PATH%;C:\Program Files\nodejs"
    node -v >nul 2>&1
    if !errorlevel! neq 0 (
        echo Error: restart script manually
        pause
        exit /b 1
    )
)
echo Node.js found
echo Checking Node modules...

if not exist node_modules\ws (
    echo Installing ws...
    call npm install ws
)

if not exist node_modules\express (
    echo Installing express...
    call npm install express
)

where serve >nul 2>&1
if !errorlevel! neq 0 (
    echo Installing serve...
    call npm install -g serve
)

echo Node modules ready
title Streaming server

:: 3. Launch both server (each in its own minimized window)
echo Initiating server.js...
set "PORT=!WS_PORT!"
start "Server WS" /min node server.js

echo Initiating server HTML...
start "Server HTML" /min npx serve . -l !HTTP_PORT!

echo.
echo ---------------------------------------------
echo  Both servers running
echo  WebSocket : ws://localhost:!WS_PORT!
echo  HTML      : http://localhost:!HTTP_PORT!
echo  (DO NOT CLOSE THIS WINDOW)
echo ---------------------------------------------
echo.

:: This window remains open waiting for Unity to close it
:loop
timeout /t 3600 /nobreak >nul
goto loop