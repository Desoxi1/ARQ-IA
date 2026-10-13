@echo off
setlocal
title Instalar ARQ-IA
pushd "%~dp0"
if errorlevel 1 (
    echo No se pudo abrir la carpeta de ARQ-IA.
    pause
    exit /b 1
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
set "arqInstallExit=%ERRORLEVEL%"
if not "%arqInstallExit%"=="0" (
    echo.
    echo La instalacion necesita atencion. Lee el mensaje anterior.
    pause
    popd
    exit /b %arqInstallExit%
)

echo.
echo Instalacion terminada. Comprobando componentes...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0doctor.ps1"
set "arqDoctorExit=%ERRORLEVEL%"
if not "%arqDoctorExit%"=="0" (
    echo El diagnostico encontro una instalacion incompleta. Lee el detalle anterior.
    pause
    popd
    exit /b %arqDoctorExit%
)
echo.
echo Abre Revit 2027 y Dynamo en una sesion nueva para usar ARQ-IA.
echo Si Codex solicita confiar en esta carpeta, acepta para habilitar los MCP locales.
pause
popd
exit /b 0
