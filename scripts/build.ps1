param(
    [ValidateSet('en', 'zh-CN')][string]$Language = 'en',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "artifacts\app-$Language" }
$project = Join-Path $repoRoot 'src\TH06NCTrainer.csproj'
& dotnet publish $project -c Release -r win-x64 --self-contained false "-p:PracticeLanguage=$Language" "-p:IntermediateOutputPath=obj/build-$Language/" "-p:OutputPath=bin/build-$Language/" -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$docs = Join-Path $repoRoot "docs\$Language"
Copy-Item -LiteralPath (Join-Path $docs 'README.md'), (Join-Path $docs 'RELEASE_NOTES.md') -Destination $OutputDirectory -Force
Write-Host "Built: $OutputDirectory\TH06NCTrainer.exe"
