@echo off
REM Abre o Transcritor mostrando mensagens de erro, se houver (diagnostico).
set "PY=%~dp0runtime\python\python.exe"
if not exist "%PY%" set "PY=%~dp0venv\Scripts\python.exe"
cd /d "%~dp0app"
"%PY%" app.py
if errorlevel 1 pause
