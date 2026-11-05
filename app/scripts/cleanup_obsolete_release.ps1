$ErrorActionPreference = "Stop"

$obsoleteFolder = [IO.Path]::GetFullPath("C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools_Portable")
if ($obsoleteFolder -ne "C:\Users\HeliumMarcos\Downloads\Music\HeliumVideoTools_Portable") {
    throw "Pasta obsoleta inesperada."
}
if (Test-Path -LiteralPath $obsoleteFolder) {
    Remove-Item -LiteralPath $obsoleteFolder -Recurse -Force
}

$obsoleteShortcut = "C:\Users\HeliumMarcos\Downloads\Music\Helium Vídeo Tools.lnk"
if (Test-Path -LiteralPath $obsoleteShortcut) {
    Remove-Item -LiteralPath $obsoleteShortcut -Force
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
foreach ($name in @("build-debug", "dist-debug", "build-debug312", "dist-debug312")) {
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $name))
    if (-not $target.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar)) {
        throw "Diretório de diagnóstico fora do projeto."
    }
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
