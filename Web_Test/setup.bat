@echo off
title Launcher WebRTC TFG
color 0A

echo ===============================
echo   INICIANDO ENTORNO TFG
echo ===============================
echo.

REM ----------- COMPROBAR NODE ----------
echo [1/5] Comprobando Node.js...
where npm >nul 2>&1

IF ERRORLEVEL 1 (
    echo ERROR: Node.js no está instalado.
    echo Descargalo de: https://nodejs.org/
    pause
    exit /b
) ELSE (
    echo OK: Node.js detectado
)

echo.

REM ----------- COMPROBAR NPM ----------
echo [2/5] Comprobando npm...

where npm >nul 2>&1
IF ERRORLEVEL 1 (
    echo ERROR: npm no está disponible.
    pause
    exit /b
) ELSE (
    echo OK: npm detectado
)

echo.

REM ----------- INICIALIZAR PROYECTO ----------
echo [3/5] Preparando proyecto...

IF NOT EXIST package.json (
    echo Creando package.json...
    npm init -y
)

echo.

REM ----------- INSTALAR DEPENDENCIAS ----------
echo [4/5] Instalando dependencias necesarias...

IF NOT EXIST node_modules (
    echo Instalando ws...
    npm install ws
) ELSE (
    echo Dependencias ya instaladas
)

echo.

REM ----------- LANZAR SERVIDOR ----------
echo [5/5] Lanzando servidor WebSocket...

start cmd /k "echo ===== SERVER LOG ===== && node server.js"

echo Esperando 2 segundos para asegurar arranque...
timeout /t 2 >nul

REM ----------- SERVIDOR HTTP ----------
echo.
echo Lanzando servidor web para index.html...

npx http-server -p 3000 >nul 2>&1

IF ERRORLEVEL 1 (
    echo Instalando http-server...
    npm install -g http-server
)

start cmd /k "echo ===== HTTP SERVER ===== && npx http-server -p 3000"

echo.

REM ----------- ABRIR NAVEGADOR ----------
echo Abriendo navegador...
start http://localhost:3000

echo.
echo ===============================
echo   TODO LISTO
echo ===============================
echo.
echo Servidor WS: ws://localhost:8080
echo Web:         http://localhost:3000
echo.
pause