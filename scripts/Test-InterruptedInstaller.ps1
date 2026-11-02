param([Parameter(Mandatory)][string]$Nsis)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$testParent = Join-Path $projectRoot ('work\installer-interruption-' + [guid]::NewGuid().ToString('N'))
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$payload = Join-Path $testParent 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $payload 'HeliumVideoTools.exe'),'new fixture executable')
[IO.File]::WriteAllText((Join-Path $payload 'runtime.dat'),'new fixture runtime')
foreach ($stage in @('extracted','renamed','residual')) {
    $caseRoot = [IO.Path]::GetFullPath((Join-Path $testParent $stage))
    $installRoot = Join-Path $caseRoot 'HeliumVideoTools'
    # Resolved, isolated targets are checked before the installer is allowed to mutate them.
    if (-not $installRoot.StartsWith($testParent + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Raiz de teste fora da pasta isolada' }
    $oldApp = Join-Path $installRoot 'app\HeliumVideoTools.exe'
    New-Item -ItemType Directory -Path (Split-Path -Parent $oldApp) -Force | Out-Null
    [IO.File]::WriteAllText($oldApp,'old fixture executable')
    $sentinel = Join-Path $caseRoot 'user-data.json'
    [IO.File]::WriteAllText($sentinel,'preserve user data')
    $oldHash = (Get-FileHash -LiteralPath $oldApp).Hash
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
    $setup = Join-Path $caseRoot 'interruption-test.exe'
    $compileOutput = & $Nsis /INPUTCHARSET UTF8 "/DPROJECT_DIR=$projectRoot" "/DAPP_VERSION=$version" /DRECOVERY_TEST "/DTEST_INSTALL_DIR=$installRoot" "/DTEST_PAYLOAD=$payload" "/DTEST_OUTPUT=$setup" (Join-Path $projectRoot 'installer\HeliumVideoTools.nsi')
    if ($LASTEXITCODE -ne 0) { $compileOutput | Select-Object -Last 20 | Write-Output; throw 'Build do instalador de teste falhou' }
    function Stop-AtCheckpoint([string]$checkpoint) {
        $marker = Join-Path $installRoot 'checkpoint.txt'
        if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
        $process = Start-Process -FilePath $setup -ArgumentList @('/S',"/CHECKPOINT=$checkpoint") -PassThru -WindowStyle Hidden
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(60)
            do { Start-Sleep -Milliseconds 100; $process.Refresh() } while (-not (Test-Path -LiteralPath $marker) -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
            if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw) -ne $checkpoint) { throw "Checkpoint não atingido: $checkpoint" }
            $process.Kill() # Only this isolated test installer, without killing its tree.
            $process.WaitForExit()
        } finally { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
    }
    Stop-AtCheckpoint $(if ($stage -eq 'residual') { 'renamed' } else { $stage })
    if ($stage -ne 'extracted') {
        if (Test-Path -LiteralPath $oldApp) { throw 'Teste não atingiu intervalo sem app ativo' }
        if ((Get-FileHash -LiteralPath (Join-Path $installRoot 'app.previous\HeliumVideoTools.exe')).Hash -ne $oldHash) { throw 'Backup anterior perdido' }
        if ($stage -eq 'residual') {
            New-Item -ItemType Directory -Path (Split-Path -Parent $oldApp) -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path (Split-Path -Parent $oldApp) 'unknown.txt'),'preserve unknown contents')
        }
        Stop-AtCheckpoint 'extracted'
        if ($stage -eq 'residual' -and (Get-Content -LiteralPath (Join-Path $installRoot 'app.interrupted\unknown.txt') -Raw) -ne 'preserve unknown contents') { throw 'Conteúdo residual foi apagado' }
    }
    if ((Get-FileHash -LiteralPath $oldApp).Hash -ne $oldHash) { throw 'Instalação anterior não foi restaurada/preservada antes da nova extração' }
    $finish = Start-Process -FilePath $setup -ArgumentList '/S' -PassThru -WindowStyle Hidden
    if (-not $finish.WaitForExit(60000) -or $finish.ExitCode -ne 0) { throw 'Reinstalação após interrupção falhou' }
    if ((Get-FileHash -LiteralPath $oldApp).Hash -ne (Get-FileHash -LiteralPath (Join-Path $payload 'HeliumVideoTools.exe')).Hash) { throw 'Novo payload não foi ativado' }
    if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash) { throw 'Dados externos à instalação foram alterados' }
    Write-Output "PASS: instalador interrompido em $stage, versão anterior preservada/restaurada e nova instalação concluída em raiz isolada."
}
