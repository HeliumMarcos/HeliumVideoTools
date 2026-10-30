param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$expected = Join-Path $env:LOCALAPPDATA 'Programs\HeliumVideoTools\app\HeliumVideoTools.exe'
$link = Join-Path ([Environment]::GetFolderPath('Programs')) 'Helium Vídeo Tools\Helium Vídeo Tools.lnk'
$first = Start-Process -FilePath $Installer -ArgumentList '/S' -PassThru -Wait -WindowStyle Hidden
if ($first.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $expected)) { throw 'Primeira instalação falhou' }
$before = (Get-FileHash -LiteralPath $link -Algorithm SHA256).Hash
$app = Start-Process -FilePath $expected -PassThru -WindowStyle Hidden
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do { Start-Sleep -Milliseconds 100; $app.Refresh() } while ($app.MainWindowHandle -eq 0 -and -not $app.HasExited -and [DateTime]::UtcNow -lt $deadline)
if ($app.HasExited -or $app.MainWindowHandle -eq 0) { throw 'Aplicação instalada não exibiu janela' }
$upgrade = Start-Process -FilePath $Installer -ArgumentList '/S' -PassThru -Wait -WindowStyle Hidden
if ($upgrade.ExitCode -ne 0) { throw 'Reinstalação/upgrade falhou' }
$app.Refresh()
if (-not $app.HasExited) { throw 'Instalador não fechou o app alvo' }
if ((Get-FileHash -LiteralPath $link -Algorithm SHA256).Hash -ne $before) { throw 'Atalho existente foi alterado' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($link)
if ($shortcut.TargetPath -ne $expected) { throw 'Destino do atalho mudou' }
$registry = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HeliumVideoTools'
if ($registry.InstallLocation -ne (Join-Path $env:LOCALAPPDATA 'Programs\HeliumVideoTools')) { throw 'Pasta registrada divergente' }
Write-Output 'PASS: instalar, iniciar WPF, reinstalar com app aberto, manter pasta e bytes do atalho.'
