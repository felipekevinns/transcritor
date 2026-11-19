# Histórico de versões

## [1.0.0] — 2026-11-19

Primeira versão pública.

### Aplicativo
- Interface de mesa para Windows com fila de arquivos, arrastar e soltar (arquivos e pastas) e progresso por etapa.
- Qualidade **Máxima precisão** (`large-v3`) ou **Rápida** (`large-v3-turbo`).
- Processamento na **placa de vídeo** (CUDA, `float16`) ou no **processador** (`int8`).
- Identificação de falantes com `pyannote/speaker-diarization-community-1`, com número automático, exato ou em intervalo.
- Vocabulário personalizado para nomes, siglas e termos técnicos.
- Sete formatos de saída: texto corrido, texto com horários e falantes, Word, SRT, VTT, JSON e TSV.
- Leitura da transcrição dentro do aplicativo, com cópia do texto.
- Redução automática do lote quando falta memória na placa de vídeo.
- Cancelamento imediato, com a memória da placa liberada.
- Tema claro e escuro, de acordo com o Windows.
- Correção automática para instalações do WebView2 sem o Microsoft Edge.

### Instalação
- Instalador `Instalar.bat` / `instalar.ps1`: instala Python 3.11, ffmpeg, WhisperX, PyTorch com CUDA e a interface, com versões testadas.
- Compatível com antivírus que inspecionam HTTPS (os certificados do Windows são usados).

### Linha de comando
- `transcrever.bat` e `transcrever_com_falantes.bat` para uso sem interface.
