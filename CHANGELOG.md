# Histórico de versões

## [1.1.0] — 2026-11-19

Instalação muito mais simples, em duas versões.

### Novo
- **Instalador (`Transcritor-Setup-1.1.0.exe`)**: assistente com a identidade visual do app, escolha da pasta de instalação, opção de baixar os modelos já na instalação, atalhos no menu Iniciar e na Área de Trabalho, e desinstalação pelas *Configurações do Windows*. Não pede permissão de administrador.
- **Versão Portable (`Transcritor-Portable-1.1.0.zip`)**: extraia e abra o `Transcritor.exe`. No primeiro uso, ele prepara tudo dentro da própria pasta. Nada é instalado no Windows, e a pasta pode ser movida.
- **`Transcritor.exe`**: inicializador nativo, com tema claro e escuro. Mostra o progresso real de cada download, confere a integridade (SHA-256) do Python e do ffmpeg baixados, retoma de onde parou se for interrompido e grava um registro detalhado em `runtime\preparo.log`.
- Instala automaticamente, quando faltam, o Microsoft Visual C++ e o WebView2 (instaladores oficiais, com a assinatura da Microsoft conferida).
- Os modelos ficam na pasta do programa (`models\`), não mais no perfil do usuário.

### Alterado
- O Python agora é embutido (`runtime\python`) e o ffmpeg fica em `runtime\ffmpeg`, sem depender do winget nem de instalações no sistema.
- Os scripts de linha de comando usam o Python embutido quando ele existe.

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
