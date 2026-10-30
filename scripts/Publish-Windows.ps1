param([string]$Dotnet = 'dotnet', [switch]$Installer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$version = ([xml](Get-Content -Raw 'Directory.Build.props')).Project.PropertyGroup.Version
New-Item -ItemType Directory -Force 'artifacts\release' | Out-Null
& $Dotnet publish src/HeliumVideoTools.Desktop -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o artifacts/publish
if ($LASTEXITCODE -ne 0) { throw 'Publicação WPF falhou' }
& $Dotnet publish src/HeliumVideoTools.UpdateHost -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/update-host
if ($LASTEXITCODE -ne 0) { throw 'Publicação auxiliar falhou' }
& "$PSScriptRoot\Prepare-MediaTools.ps1" -Destination (Join-Path $projectRoot 'artifacts\publish\tools')
Copy-Item 'tools\manifest.json' 'artifacts\publish\tools\manifest.json' -Force
Copy-Item 'FFmpeg-LICENSE.txt' 'artifacts\publish\FFmpeg-LICENSE.txt' -Force
if ($Installer) {
  $nsis = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'
  if (-not (Test-Path -LiteralPath $nsis)) { throw 'NSIS ausente. Instale no runner de build.' }
  & $nsis "/DPROJECT_DIR=$projectRoot" "/DAPP_VERSION=$version" installer/HeliumVideoTools.nsi
  if ($LASTEXITCODE -ne 0) { throw 'NSIS falhou' }
  $setup = Get-Item "artifacts\release\HeliumVideoTools-$version-win-x64-Setup.exe"
  $hash = (Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
  [IO.File]::WriteAllText((Join-Path $projectRoot 'artifacts\release\SHA256SUMS.txt'), "$hash  $($setup.Name)`n", [Text.UTF8Encoding]::new($false))
}
