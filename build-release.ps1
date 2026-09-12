param([string]$OutputRoot = (Join-Path $PSScriptRoot 'artifacts\v1.0.0'))

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'TH06NCTrainer.csproj'
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'assets\background.png'))) { throw 'Release background is missing.' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$packages = @()
foreach ($standalone in @($false, $true)) {
    $suffix = if ($standalone) { '-self-contained' } else { '' }
    $folder = Join-Path $OutputRoot "app$suffix"
    & dotnet publish $project -c Release -r win-x64 --self-contained $standalone.ToString().ToLowerInvariant() -p:IncludeLocalBackground=true -p:DebugType=None -p:DebugSymbols=false -o $folder
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $suffix" }
    $exe = Join-Path $folder 'TH06NCTrainer.exe'
    if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.0.0.0') { throw 'Incorrect application version.' }
    $report = Join-Path $OutputRoot "ui-test$suffix.txt"
    $test = Start-Process $exe -ArgumentList @('--ui-self-test', ('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($test.ExitCode -ne 0) { Get-Content -LiteralPath $report; throw "UI regression failed: $suffix" }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'README.zh-CN.md'), (Join-Path $PSScriptRoot 'RELEASE_NOTES.md') -Destination $folder -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $folder -Recurse -Force
    $archive = Join-Path $OutputRoot "TH06NCPractice-v1.0.0-win-x64$suffix.zip"
    if (Test-Path -LiteralPath $archive) { throw "Existing package retained; use a new OutputRoot: $archive" }
    Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $folder).FullName -DestinationPath $archive
    $packages += $archive
}
Get-FileHash -LiteralPath $packages -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash.ToLowerInvariant()) *$(Split-Path $_.Path -Leaf)" } |
    Set-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release packages: $OutputRoot"
