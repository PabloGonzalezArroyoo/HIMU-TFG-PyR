@echo off
title Launcher WebRTC HIMU
color 0B

echo ================================
echo  INITIALIZING HIMU'S ENVIROMENT 
echo ================================
echo.

REM ----------- CHECK NODE AND NPM ----------
echo [1/4] Checking Node.js y npm...
where node >nul 2>&1

IF ERRORLEVEL 1 (
    echo ERROR: Node.js is not installed.
    echo Download it from: https://nodejs.org/
    pause
    exit /b
)
echo OK: Node.js y npm detected

echo.

REM ----------- INITIALIZING PROJECT ----------
echo [2/4] Seting up project...

IF NOT EXIST package.json (
    echo Creating package.json...
    npm init -y
) ELSE (
    echo OK: package.json already exists
)

echo.

REM ----------- INSTALL DEPENDENCIES ----------
echo [3/4] Installing dependencies needed...

IF NOT EXIST node_modules (
    echo Installing ws...
    npm install ws
) ELSE (
    echo OK: Dependencies already installed
)

echo.

REM ----------- LAUNCHING SERVER ----------
echo [4/4] Launching WebSocket server...

start "WS Server" cmd /k "echo ===== SERVER LOG ===== && node src/server/server.js"
timeout /t 2 >nul
echo OK: WebSocket launched - Its console will show logs

start "HTTP Server" cmd /k "echo ===== HTTP SERVER ===== && npx http-server src/web -p 3000 -c-1 -o"
timeout /t 2 >nul
echo OK: HTTP Sever launched - Browser should open itself

echo.
color 0A
echo ===============================
echo   ALL READY
echo ===============================
echo.
echo WS server : ws://localhost:8080
echo Web       : http://localhost:3000
echo *NOTE:* Each process opened a console. Close them to terminate the server and socket.
echo.
pause