param()

$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'TH06NCTrainer.csproj'
$outputDir = Join-Path $PSScriptRoot 'artifacts\app'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 9 SDK before building.'
}

& dotnet publish $projectFile -c Release --self-contained false -p:DebugType=None -p:DebugSymbols=false -o $outputDir
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $outputDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.zh-CN.md') -Destination $outputDir
Write-Host "Built application: $outputDir\TH06NCTrainer.exe"
Write-Host 'Keep all files in this output directory together. Requires .NET 9 Desktop Runtime x64.'
