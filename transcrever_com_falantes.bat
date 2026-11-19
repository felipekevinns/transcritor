@echo off
chcp 65001 >nul
REM ================================================================
REM  Transcrever com WhisperX + identificacao de falantes
REM  Requer o token do Hugging Face salvo em hf_token.txt (nesta pasta)
REM  Uso: arraste um ou mais videos/audios para cima deste arquivo.
REM ================================================================
if "%~1"=="" (
  echo Arraste um ou mais videos para cima deste arquivo .bat
  pause
  exit /b
)
set "ROOT=%~dp0"
if not exist "%ROOT%hf_token.txt" (
  echo Falta o token do Hugging Face em "%ROOT%hf_token.txt"
  pause
  exit /b
)
set HF_HUB_DISABLE_SYMLINKS_WARNING=1
set "PY=%ROOT%runtime\python\python.exe"
if not exist "%PY%" set "PY=%ROOT%venv\Scripts\python.exe"
if exist "%ROOT%runtime\ffmpeg" set "PATH=%ROOT%runtime\ffmpeg;%PATH%"
if exist "%ROOT%runtime\python" set "HF_HOME=%ROOT%models\huggingface"

set /p HFTOKEN=<"%ROOT%hf_token.txt"
set "VOCAB="
if exist "%ROOT%vocabulario.txt" set /p VOCAB=<"%ROOT%vocabulario.txt"

"%PY%" -m whisperx %* --model large-v3 --language pt --compute_type float16 --batch_size 8 --initial_prompt "%VOCAB%" --diarize --hf_token %HFTOKEN% --output_format all --output_dir "%~dp1transcricoes"

echo.
echo Pronto! Arquivos em: "%~dp1transcricoes"
pause
