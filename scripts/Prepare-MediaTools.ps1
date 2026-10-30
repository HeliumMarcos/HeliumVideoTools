param([string]$Destination = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Destination) { $Destination = $projectRoot }
$Destination = [IO.Path]::GetFullPath($Destination)
$manifest = Get-Content -Raw (Join-Path $projectRoot 'tools\manifest.json') | ConvertFrom-Json
$workRoot = Join-Path $projectRoot 'work\media-tools'
New-Item -ItemType Directory -Force $workRoot | Out-Null
$archive = Join-Path $workRoot ('ffmpeg-' + $manifest.version + '.zip')
if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest $manifest.url -OutFile $archive }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'SHA-256 do pacote FFmpeg divergente.' }
$staging = Join-Path $workRoot ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($name in @('ffmpeg.exe','ffprobe.exe','LICENSE')) {
        $matches = @($zip.Entries | Where-Object { ($_.Name -eq $name) -and ($_.FullName -notmatch '\.\.') })
        if ($matches.Count -ne 1) { throw "Arquivo esperado ausente/duplicado: $name" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($matches[0], (Join-Path $staging $name), $false)
    }
} finally { $zip.Dispose() }
foreach ($name in @('ffmpeg','ffprobe')) {
    $binary = Join-Path $staging ($name + '.exe')
    $versionOutput = @(& $binary -version)
    $version = $versionOutput[0]
    if ($LASTEXITCODE -ne 0 -or $version -notlike "$name version $($manifest.version)*") { throw "Versão inesperada: $version" }
    Write-Output $version
}
$encoders = & (Join-Path $staging 'ffmpeg.exe') -hide_banner -encoders 2>&1 | Out-String
foreach ($encoder in @('libx264','libx265','libvpx','libvpx-vp9','libopus')) { if ($encoders -notmatch "\b$encoder\b") { throw "Codificador ausente: $encoder" } }
New-Item -ItemType Directory -Force $Destination | Out-Null
$backup = Join-Path $workRoot ('backup-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $backup | Out-Null
$installed = @()
try {
    foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
        $path = Join-Path $Destination $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $backup $name) }
    }
    foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
        Copy-Item -LiteralPath (Join-Path $staging $name) -Destination (Join-Path $Destination $name) -Force
        $installed += $name
    }
    Copy-Item -LiteralPath (Join-Path $staging 'LICENSE') -Destination (Join-Path $Destination 'FFmpeg-LICENSE.txt') -Force
} catch {
    foreach ($name in $installed) {
        $old = Join-Path $backup $name
        if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination (Join-Path $Destination $name) -Force }
        else { Remove-Item -LiteralPath (Join-Path $Destination $name) -Force }
    }
    throw
}
Write-Output "Conjunto verificado instalado em $Destination; backup: $backup"
