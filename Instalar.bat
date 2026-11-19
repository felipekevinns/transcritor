@echo off
REM Instala o Transcritor. Pode ser executado de novo para reparar a instalacao.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0instalar.ps1" %*
echo.
pause
