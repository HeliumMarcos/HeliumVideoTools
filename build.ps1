$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $projectRoot

$buildDir = Join-Path $projectRoot "build"
$distDir = Join-Path $projectRoot "dist"
if ((Resolve-Path -LiteralPath $projectRoot).Path -ne "C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools") {
    throw "Diretório de projeto inesperado; build interrompido por segurança."
}
if (Test-Path -LiteralPath $buildDir) { Remove-Item -LiteralPath $buildDir -Recurse -Force }
if (Test-Path -LiteralPath $distDir) { Remove-Item -LiteralPath $distDir -Recurse -Force }

$buildPython = Join-Path $projectRoot ".venv-build\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $buildPython)) {
    throw "Ambiente de build Python 3.12 ausente. Crie .venv-build e instale requirements.txt."
}
& $buildPython scripts\prepare_brand_assets.py
if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar os ativos da marca." }
& $buildPython scripts\generate_icons.py
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar os ícones." }
& $buildPython -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) { throw "Os testes falharam." }
& $buildPython -m PyInstaller --noconfirm --clean HeliumVideoTools.spec
if ($LASTEXITCODE -ne 0) { throw "O PyInstaller falhou." }
& $buildPython -m PyInstaller --noconfirm --clean HeliumVideoTools-fast.spec
if ($LASTEXITCODE -ne 0) { throw "O PyInstaller da versão rápida falhou." }

$builtExe = Join-Path $distDir "HeliumVideoTools.exe"
if (-not (Test-Path -LiteralPath $builtExe)) { throw "O executável não foi gerado." }
$destinationExe = "C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools.exe"
Copy-Item -LiteralPath $builtExe -Destination $destinationExe -Force

$builtFast = Join-Path $distDir "HeliumVideoTools-Rapido"
if (-not (Test-Path -LiteralPath (Join-Path $builtFast "HeliumVideoTools.exe"))) {
    throw "A versão rápida não foi gerada."
}
$destinationFast = "C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools-Rapido"
$musicRoot = (Resolve-Path -LiteralPath "C:\Users\HeliumMarcos\Downloads\Music").Path
if ((Split-Path -Parent $destinationFast) -ne $musicRoot) {
    throw "Destino da versão rápida inesperado; cópia interrompida por segurança."
}
if (Test-Path -LiteralPath $destinationFast) {
    if ((Resolve-Path -LiteralPath $destinationFast).Path -ne $destinationFast) {
        throw "Destino da versão rápida não confere; remoção interrompida por segurança."
    }
    Remove-Item -LiteralPath $destinationFast -Recurse -Force
}
Copy-Item -LiteralPath $builtFast -Destination $destinationFast -Recurse

Get-FileHash -LiteralPath $destinationExe -Algorithm SHA256
Write-Host "Executável único pronto: $destinationExe"
Get-FileHash -LiteralPath (Join-Path $destinationFast "HeliumVideoTools.exe") -Algorithm SHA256
Write-Host "Versão de abertura rápida pronta: $destinationFast"
