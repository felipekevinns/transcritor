# Gera os dois pacotes de distribuição em dist\:
#   Transcritor-Portable-<versão>.zip   (extrair e abrir o Transcritor.exe)
#   Transcritor-Setup-<versão>.exe      (assistente de instalação)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Versao = (Get-Content (Join-Path $Root 'app\dependencias.json') -Raw | ConvertFrom-Json).versao
$Dist = Join-Path $Root 'dist'
$Pkg = Join-Path $Dist 'Transcritor'

Write-Host "Transcritor $Versao"
if (Test-Path $Pkg) { Remove-Item $Pkg -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $Pkg 'app\ui') | Out-Null

# 1. Inicializador
& (Join-Path $Root 'launcher\compilar.ps1') -Saida (Join-Path $Pkg 'Transcritor.exe')

# 2. Aplicativo
foreach ($f in 'app.py', 'worker.py', 'transcritor.ico', 'dependencias.json') {
    Copy-Item (Join-Path $Root "app\$f") (Join-Path $Pkg 'app')
}
Copy-Item (Join-Path $Root 'app\ui\*') (Join-Path $Pkg 'app\ui')
Copy-Item (Join-Path $Root 'LICENSE') $Pkg
Copy-Item (Join-Path $PSScriptRoot 'LEIA-ME.txt') $Pkg

# 3. Portable
$zip = Join-Path $Dist "Transcritor-Portable-$Versao.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $Pkg -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Gerado: $zip"

# 4. Instalador
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6.7+ não encontrado (winget install JRSoftware.InnoSetup).' }
& $iscc /Q "/DSourceDir=$Pkg" (Join-Path $PSScriptRoot 'transcritor.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
Write-Host "Gerado: $(Join-Path $Dist "Transcritor-Setup-$Versao.exe")"
