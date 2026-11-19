# Compila o Transcritor.exe com o compilador C# que acompanha o Windows (.NET Framework 4.x).
param([string]$Saida = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Transcritor.exe'))

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw 'csc.exe não encontrado (.NET Framework 4.x).' }
$fx = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$root = Split-Path -Parent $PSScriptRoot

& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    "/out:$Saida" `
    "/win32icon:$root\app\transcritor.ico" `
    "/win32manifest:$PSScriptRoot\Transcritor.manifest" `
    "/reference:$fx\System.Web.Extensions.dll" `
    "/reference:$fx\System.IO.Compression.dll" `
    "/reference:$fx\System.IO.Compression.FileSystem.dll" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "$PSScriptRoot\Transcritor.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }
Write-Host "Gerado: $Saida"
