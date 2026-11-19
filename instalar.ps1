<#
.SYNOPSIS
    Instalador do Transcritor (WhisperX com interface para Windows).

.DESCRIPTION
    Instala Python 3.11, ffmpeg, um ambiente Python isolado com WhisperX,
    PyTorch com CUDA (quando ha placa NVIDIA) e a interface grafica.
    Pode ser executado de novo a qualquer momento: etapas ja concluidas sao puladas.

.PARAMETER CpuOnly
    Nao instala o PyTorch com CUDA (para computadores sem placa NVIDIA).

.PARAMETER NoShortcut
    Nao cria atalhos na Area de Trabalho.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\instalar.ps1
#>
param(
    [switch]$CpuOnly,
    [switch]$NoShortcut
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Venv = Join-Path $Root 'venv'
$Py = Join-Path $Venv 'Scripts\python.exe'
$PyW = Join-Path $Venv 'Scripts\pythonw.exe'

$Versions = @{
    whisperx        = '3.8.6'
    torch           = '2.8.0'
    torchaudio      = '2.8.0'
    torchvision     = '0.23.0'
    pywebview       = '6.2.1'
    pythondocx      = '1.2.0'
    pipsystemcerts  = '5.3'
    hfxet           = '1.8.0'
    proxytools      = '0.1.0'
}
$TorchIndex = 'https://download.pytorch.org/whl/cu128'

$script:StepNo = 0
function Write-Step($text) {
    $script:StepNo++
    Write-Host ''
    Write-Host ("[{0}/8] {1}" -f $script:StepNo, $text) -ForegroundColor White
}
function Write-Ok($text)   { Write-Host "      $text" -ForegroundColor DarkGreen }
function Write-Info($text) { Write-Host "      $text" -ForegroundColor Gray }
function Fail($text) {
    Write-Host ''
    Write-Host "ERRO: $text" -ForegroundColor Red
    Write-Host 'A instalação foi interrompida. Corrija o problema e execute o instalador novamente.' -ForegroundColor Red
    exit 1
}
function Refresh-Path {
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'User') + ';' + [Environment]::GetEnvironmentVariable('Path', 'Machine')
}
function Invoke-Pip {
    param([string[]]$PipArgs, [string]$What)
    & $Py -m pip @PipArgs --disable-pip-version-check
    if ($LASTEXITCODE -ne 0) { Fail "falha ao instalar $What." }
}
function Test-Winget {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Fail 'o winget não foi encontrado. Instale o "Instalador de Aplicativo" pela Microsoft Store.'
    }
}

Write-Host ''
Write-Host 'Transcritor - instalação' -ForegroundColor White
Write-Host "Pasta: $Root" -ForegroundColor Gray

# ------------------------------------------------------------------ 1. Python
Write-Step 'Python 3.11'
$SysPy = $null
$candidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python311\python.exe'),
    (Join-Path $env:ProgramFiles 'Python311\python.exe')
)
foreach ($c in $candidates) { if (Test-Path $c) { $SysPy = $c; break } }
if (-not $SysPy -and (Get-Command py -ErrorAction SilentlyContinue)) {
    $p = (& py -3.11 -c 'import sys; print(sys.executable)' 2>$null)
    if ($LASTEXITCODE -eq 0 -and $p) { $SysPy = $p.Trim() }
}
if (-not $SysPy) {
    Test-Winget
    Write-Info 'Instalando Python 3.11...'
    winget install --id Python.Python.3.11 -e --scope user --silent --accept-package-agreements --accept-source-agreements
    Refresh-Path
    foreach ($c in $candidates) { if (Test-Path $c) { $SysPy = $c; break } }
    if (-not $SysPy) { Fail 'não foi possível instalar o Python 3.11.' }
}
Write-Ok (& $SysPy --version)

# ------------------------------------------------------------------ 2. ffmpeg
Write-Step 'ffmpeg'
Refresh-Path
$ff = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ff) {
    $hit = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages') -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $hit) {
        Test-Winget
        Write-Info 'Instalando ffmpeg...'
        winget install --id Gyan.FFmpeg -e --silent --accept-package-agreements --accept-source-agreements
        Refresh-Path
        $ff = Get-Command ffmpeg -ErrorAction SilentlyContinue
        if (-not $ff) {
            $hit = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages') -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $hit) { Fail 'não foi possível instalar o ffmpeg.' }
        }
    }
}
Write-Ok 'ffmpeg disponível'

# ------------------------------------------------------------------ 3. Ambiente
Write-Step 'Ambiente Python isolado'
if (-not (Test-Path $Py)) {
    & $SysPy -m venv $Venv
    if ($LASTEXITCODE -ne 0) { Fail 'não foi possível criar o ambiente virtual.' }
    Write-Ok "Criado em $Venv"
} else {
    Write-Ok 'Já existe'
}
& $Py -m pip install --upgrade pip --quiet --disable-pip-version-check

# ------------------------------------------------------------------ 4. Certificados
Write-Step 'Certificados do Windows'
# Faz o Python confiar nos certificados do Windows (necessario com antivirus
# que inspecionam HTTPS, como o Kaspersky). A verificacao continua ativa.
Invoke-Pip @('install', "pip-system-certs==$($Versions.pipsystemcerts)", '--quiet') 'pip-system-certs'
Write-Ok 'Configurado'

# ------------------------------------------------------------------ 5. WhisperX
Write-Step 'WhisperX (pode levar alguns minutos)'
Invoke-Pip @('install', "whisperx==$($Versions.whisperx)", "hf_xet==$($Versions.hfxet)", "python-docx==$($Versions.pythondocx)") 'WhisperX'
Write-Ok "WhisperX $($Versions.whisperx)"

# ------------------------------------------------------------------ 6. PyTorch com CUDA
Write-Step 'PyTorch para a placa de vídeo'
$hasNvidia = [bool](Get-Command nvidia-smi -ErrorAction SilentlyContinue)
if ($hasNvidia) {
    $gpu = (& nvidia-smi --query-gpu=name --format=csv,noheader 2>$null | Select-Object -First 1)
    if (-not $gpu) { $hasNvidia = $false }
}
if ($CpuOnly -or -not $hasNvidia) {
    Write-Info 'Sem placa NVIDIA (ou -CpuOnly). O processador será usado.'
} else {
    Write-Info "Placa encontrada: $gpu"
    $cudaOk = (& $Py -c 'import torch; print(torch.cuda.is_available())' 2>$null)
    if ($cudaOk -match 'True') {
        Write-Ok 'PyTorch com CUDA já instalado'
    } else {
        Write-Info 'Baixando o PyTorch com CUDA (cerca de 3 GB)...'
        Invoke-Pip @('install', '--force-reinstall', '--no-deps',
            "torch==$($Versions.torch)", "torchaudio==$($Versions.torchaudio)", "torchvision==$($Versions.torchvision)",
            '--index-url', $TorchIndex) 'PyTorch com CUDA'
        Write-Ok 'PyTorch com CUDA instalado'
    }
}

# ------------------------------------------------------------------ 7. Interface
Write-Step 'Interface gráfica'
Invoke-Pip @('install', 'setuptools', 'wheel', '--quiet') 'setuptools'
# proxy_tools so existe como codigo-fonte e falha no isolamento de build padrao
$env:SETUPTOOLS_USE_DISTUTILS = 'stdlib'
Invoke-Pip @('install', "proxy_tools==$($Versions.proxytools)", '--no-build-isolation', '--quiet') 'proxy_tools'
Remove-Item Env:\SETUPTOOLS_USE_DISTUTILS -ErrorAction SilentlyContinue
Invoke-Pip @('install', "pywebview==$($Versions.pywebview)", '--quiet') 'pywebview'

$wv = @(
    (Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\EdgeWebView\Application\*\msedgewebview2.exe" -ErrorAction SilentlyContinue),
    (Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\EdgeCore\*\msedgewebview2.exe" -ErrorAction SilentlyContinue)
) | Where-Object { $_ }
if (-not $wv) {
    Test-Winget
    Write-Info 'Instalando o Microsoft Edge WebView2 Runtime...'
    winget install --id Microsoft.EdgeWebView2Runtime -e --silent --accept-package-agreements --accept-source-agreements
}
Write-Ok "pywebview $($Versions.pywebview)"

# ------------------------------------------------------------------ 8. Verificacao e atalhos
Write-Step 'Verificação final'
$check = & $Py -c "import torch, whisperx, webview, docx; print('cuda' if torch.cuda.is_available() else 'cpu', torch.__version__)" 2>$null
if ($LASTEXITCODE -ne 0) { Fail 'as bibliotecas não carregaram corretamente.' }
if ($check -match '^cuda') { Write-Ok "Pronto para a placa de vídeo ($check)" } else { Write-Ok "Pronto para o processador ($check)" }

if (-not $NoShortcut) {
    $ws = New-Object -ComObject WScript.Shell
    $targets = @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Transcritor.lnk'), (Join-Path $Root 'Transcritor.lnk'))
    foreach ($t in $targets) {
        $l = $ws.CreateShortcut($t)
        $l.TargetPath = $PyW
        $l.Arguments = '"' + (Join-Path $Root 'app\app.py') + '"'
        $l.WorkingDirectory = (Join-Path $Root 'app')
        $l.IconLocation = (Join-Path $Root 'app\transcritor.ico') + ',0'
        $l.Description = 'Transcritor - WhisperX'
        $l.Save()
    }
    Write-Ok 'Atalho "Transcritor" criado na Área de Trabalho'
}

Write-Host ''
Write-Host 'Instalação concluída.' -ForegroundColor White
Write-Host 'Abra o Transcritor pelo atalho na Área de Trabalho.' -ForegroundColor Gray
Write-Host 'Na primeira transcrição os modelos são baixados (cerca de 3 a 5 GB, uma única vez).' -ForegroundColor Gray
Write-Host 'Para identificar falantes, configure o token do Hugging Face dentro do aplicativo.' -ForegroundColor Gray
