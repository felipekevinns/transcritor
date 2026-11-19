@echo off
REM Abre o Transcritor. Se der algum erro, esta janela mostra a mensagem.
cd /d "%~dp0app"
"%~dp0venv\Scripts\python.exe" app.py
if errorlevel 1 pause
