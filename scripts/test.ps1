param(
    [Parameter(Mandatory)][string]$ApplicationDirectory,
    [string]$GamePath
)
$ErrorActionPreference = 'Stop'
$ApplicationDirectory = [IO.Path]::GetFullPath($ApplicationDirectory)
$exe = Join-Path $ApplicationDirectory 'TH06NCTrainer.exe'
$reportRoot = Join-Path (Split-Path -Parent $ApplicationDirectory) ('tests-' + (Split-Path -Leaf $ApplicationDirectory))
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$report = Join-Path $reportRoot 'ui.txt'
$test = Start-Process $exe -ArgumentList @('--ui-self-test', ('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
if ($test.ExitCode -ne 0) { if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }; throw 'UI tests failed.' }
if ($GamePath) {
    $GamePath = (Resolve-Path -LiteralPath $GamePath).Path
    $report = Join-Path $reportRoot 'native.txt'
    $test = Start-Process $exe -ArgumentList @('--self-test', ('"' + $GamePath + '"'), ('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($test.ExitCode -ne 0) { if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }; throw 'Isolated native tests failed.' }
}
Write-Host "Tests passed: $reportRoot"
