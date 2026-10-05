@echo off
setlocal
title Migrar version anterior de ARQ-IA
pushd "%~dp0"
if errorlevel 1 exit /b 1

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$script = Join-Path (Get-Location).Path 'dynamo\extension\uninstall_legacy_programfiles.ps1'; $args = '-NoProfile -ExecutionPolicy Bypass -File ' + [char]34 + $script + [char]34; $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $args -Verb RunAs -Wait -PassThru; exit $process.ExitCode"
set "arqMigrationExit=%ERRORLEVEL%"
echo.
if not "%arqMigrationExit%"=="0" (
    echo La migracion no termino. Revisa el aviso anterior.
) else (
    echo Version anterior retirada. ARQ-IA se cargara al iniciar Revit y Dynamo de nuevo.
)
pause
popd
exit /b %arqMigrationExit%
