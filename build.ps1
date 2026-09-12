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
$docsDir = Join-Path $outputDir 'docs'
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\implementation-v3.3.md') -Destination $docsDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\implementation-v3.4.md') -Destination $docsDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\implementation-v3.4.1.md') -Destination $docsDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\implementation-v3.4.2.md') -Destination $docsDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\implementation-v3.4.3.md') -Destination $docsDir
Write-Host "Built application: $outputDir\TH06NCTrainer.exe"
Write-Host 'Keep all files in this output directory together. Requires .NET 9 Desktop Runtime x64.'
