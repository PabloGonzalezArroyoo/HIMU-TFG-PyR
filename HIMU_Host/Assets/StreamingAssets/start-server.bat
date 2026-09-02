@echo off
setlocal enabledelayedexpansion
title Streaming server

:: 0. Script relaunches itself minimized
if /I not "%~1"=="MIN" (
    start "" /min "%~f0" MIN %*
    exit /b
)
shift

cd /d "%~dp0"

:: 1. .bat has arguments (ports needed for execution). Example: start-server.bat 8080 3000
:: (They receive default values)
set "WS_PORT=8080"
set "HTTP_PORT=3000"

if not "%~1"=="" set "WS_PORT=%~1"
if not "%~2"=="" set "HTTP_PORT=%~2"

echo WebSocket/Node port is: !WS_PORT!
echo HTML port: !HTTP_PORT!
echo.

:: 2. Checking if Node and necessary modules are installed to run server.js
if not exist "node\node.exe" (
    echo Error: node\node.exe not found
    pause
    exit /b 1
)

if not exist "server.js" (
    echo Error: server.js not found
    pause
    exit /b 1
)

if not exist "node_modules\serve\build\main.js" (
    echo Error: node_modules\serve\build\main.js not found
    pause
    exit /b 1
)

if not exist "public\" (
    echo Error: public folder not found
    pause
    exit /b 1
)

title Streaming server

:: 3. Launch server.js (which contains web deployment)
echo Initiating server.js...
echo Initiating server.js...
set "PORT=!WS_PORT!"
start "Server WS" /min "node\node.exe" server.js

echo Initiating server HTML...
start "Server HTML" /min "node\node.exe" "node_modules\serve\build\main.js" -l !HTTP_PORT! public

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