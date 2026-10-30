param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$expected = Join-Path $env:LOCALAPPDATA 'Programs\HeliumVideoTools\app\HeliumVideoTools.exe'
$link = Join-Path ([Environment]::GetFolderPath('Programs')) 'Helium Vídeo Tools\Helium Vídeo Tools.lnk'
$first = Start-Process -FilePath $Installer -ArgumentList '/S' -PassThru -Wait -WindowStyle Hidden
if ($first.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $expected)) {
    Get-ChildItem -LiteralPath (Split-Path -Parent (Split-Path -Parent $expected)) -Recurse -ErrorAction SilentlyContinue | Select-Object -First 15 FullName
    throw "Primeira instalação falhou: código=$($first.ExitCode); executável esperado=$expected"
}
if (-not (Test-Path -LiteralPath $link)) {
    Get-ChildItem -LiteralPath ([Environment]::GetFolderPath('Programs')) -Filter '*Helium*' -Recurse | Select-Object FullName
    throw "Atalho esperado ausente: $link"
}
$before = (Get-FileHash -LiteralPath $link -Algorithm SHA256).Hash
$startup = [Diagnostics.Stopwatch]::StartNew()
$app = Start-Process -FilePath $expected -PassThru -WindowStyle Hidden
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do { Start-Sleep -Milliseconds 100; $app.Refresh() } while ($app.MainWindowHandle -eq 0 -and -not $app.HasExited -and [DateTime]::UtcNow -lt $deadline)
if ($app.HasExited -or $app.MainWindowHandle -eq 0) { throw 'Aplicação instalada não exibiu janela' }
$startup.Stop()
Write-Output ('Janela disponível no runner em {0:0.00}s (não é medição de abertura fria no computador do usuário).' -f $startup.Elapsed.TotalSeconds)
$upgrade = Start-Process -FilePath $Installer -ArgumentList '/S /RESTART' -PassThru -WindowStyle Hidden
# Start-Process -Wait acompanha toda a árvore, incluindo o app reaberto.
# Aqui só interessa a saída do instalador, não o fechamento do novo aplicativo.
if (-not $upgrade.WaitForExit(180000)) { throw 'Instalador excedeu o tempo de reinstalação' }
if ($upgrade.ExitCode -ne 0) { throw 'Reinstalação/upgrade falhou' }
$app.Refresh()
if (-not $app.HasExited) { throw 'Instalador não fechou o app alvo' }
$reopened = $null
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    Start-Sleep -Milliseconds 200
    $reopened = Get-Process -Name HeliumVideoTools -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $app.Id -and $_.Path -eq $expected -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
} while (-not $reopened -and [DateTime]::UtcNow -lt $deadline)
if (-not $reopened) { throw 'Aplicativo não reabriu após atualização /RESTART' }
if ((Get-FileHash -LiteralPath $link -Algorithm SHA256).Hash -ne $before) { throw 'Atalho existente foi alterado' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($link)
if ($shortcut.TargetPath -ne $expected) { throw 'Destino do atalho mudou' }
$registry = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools'
if ($registry.InstallLocation -ne (Join-Path $env:LOCALAPPDATA 'Programs\HeliumVideoTools')) { throw 'Pasta registrada divergente' }
Write-Output 'PASS: instalar, iniciar WPF, reinstalar com app aberto, reabrir automaticamente, manter pasta e bytes do atalho.'
$null = $reopened.CloseMainWindow()
