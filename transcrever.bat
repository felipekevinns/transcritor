@echo off
chcp 65001 >nul
REM ================================================================
REM  Transcrever com WhisperX pela linha de comando (sem falantes)
REM  Uso: arraste um ou mais videos/audios para cima deste arquivo.
REM  Saida: pasta "transcricoes" ao lado do primeiro arquivo.
REM ================================================================
if "%~1"=="" (
  echo Arraste um ou mais videos para cima deste arquivo .bat
  pause
  exit /b
)
set "ROOT=%~dp0"
set HF_HUB_DISABLE_SYMLINKS_WARNING=1
set "PY=%ROOT%runtime\python\python.exe"
if not exist "%PY%" set "PY=%ROOT%venv\Scripts\python.exe"
if exist "%ROOT%runtime\ffmpeg" set "PATH=%ROOT%runtime\ffmpeg;%PATH%"
if exist "%ROOT%runtime\python" set "HF_HOME=%ROOT%models\huggingface"

set "VOCAB="
if exist "%ROOT%vocabulario.txt" set /p VOCAB=<"%ROOT%vocabulario.txt"

"%PY%" -m whisperx %* --model large-v3 --language pt --compute_type float16 --batch_size 8 --initial_prompt "%VOCAB%" --output_format all --output_dir "%~dp1transcricoes"

echo.
echo Pronto! Arquivos em: "%~dp1transcricoes"
pause
