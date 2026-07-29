@echo off
setlocal enabledelayedexpansion
title Servidor de Streaming

:: ============================================================
:: -1. Minimizar automaticamente esta misma ventana
::    El script se relanza a si mismo con la ventana minimizada
::    y la instancia original se cierra.
:: ============================================================
if /I not "%~1"=="MIN" (
    start "" /min "%~f0" MIN %*
    exit /b
)
shift

:: ============================================================
:: 0. Configurar puertos (con valores por defecto)
::    Uso: start-server.bat [puerto_ws] [puerto_html]
::    Ejemplo: start-server.bat 8080 3000
:: ============================================================
set "WS_PORT=8080"
set "HTTP_PORT=3000"

if not "%~1"=="" set "WS_PORT=%~1"
if not "%~2"=="" set "HTTP_PORT=%~2"

echo Puerto WebSocket: !WS_PORT!
echo Puerto HTML: !HTTP_PORT!
echo.

:: ============================================================
:: 1. Comprobar Node.js
:: ============================================================
echo Comprobando Node.js...
node -v >nul 2>&1
if !errorlevel! neq 0 (
    echo Node.js no se encontro en tu dispositivo. Descargando instalador...
    curl -o node_installer.msi https://nodejs.org/dist/v20.11.0/node-v20.11.0-x64.msi
    if !errorlevel! neq 0 (
        echo No se pudo descargar Node.js. Revisa tu conexion.
        pause
        exit /b 1
    )
    echo Instalando Node.js...
    msiexec /i node_installer.msi /quiet /norestart
    del node_installer.msi

    :: Recargar PATH para que node sea reconocido
    set "PATH=%PATH%;C:\Program Files\nodejs"
    node -v >nul 2>&1
    if !errorlevel! neq 0 (
        echo Error: reinicia el bat manualmente.
        pause
        exit /b 1
    )
)
echo Node.js encontrado.

:: ============================================================
:: 2. Comprobar e instalar modulos necesarios
:: ============================================================
echo Comprobando modulos de Node...

if not exist node_modules\ws (
    echo Instalando ws...
    call npm install ws
)

if not exist node_modules\express (
    echo Instalando express...
    call npm install express
)

:: serve se instala globalmente
where serve >nul 2>&1
if !errorlevel! neq 0 (
    echo Instalando serve...
    call npm install -g serve
)

echo Modulos listos.

title Servidor de Streaming

:: ============================================================
:: 3. Lanzar ambos servidores, cada uno en su propia ventana
::    minimizada (/min).
:: ============================================================
echo Iniciando server.js...
set "PORT=!WS_PORT!"
start "Server WS" /min node server.js

echo Iniciando servidor HTML...
start "Server HTML" /min npx serve . -l !HTTP_PORT!

:: ============================================================
:: 4. Informar al usuario
:: ============================================================
echo.
echo =============================================
echo  Servidores iniciados correctamente
echo  WebSocket : ws://localhost:!WS_PORT!
echo  HTML      : http://localhost:!HTTP_PORT!
echo  (Esta ventana esta minimizada, no la cierres)
echo =============================================
echo.

:: Esta ventana se queda abierta esperando (minimizada). Cerrarla
:: NO cierra las ventanas "Server WS" / "Server HTML" (quedan
:: independientes), por eso Unity las cierra por puerto desde C#,
:: subiendo por el arbol de procesos hasta el cmd.exe que las abrio.
:loop
timeout /t 3600 /nobreak >nul
goto loop