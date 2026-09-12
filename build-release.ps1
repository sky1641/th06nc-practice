param([string]$OutputRoot = (Join-Path $PSScriptRoot 'artifacts\v1.0.1'))

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'TH06NCTrainer.csproj'
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'assets\background.png'))) { throw 'Release background is missing.' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
foreach ($language in @('en', 'zh-CN')) {
    $languageRoot = Join-Path $OutputRoot $language
    New-Item -ItemType Directory -Path $languageRoot -Force | Out-Null
    $packages = @()
    foreach ($standalone in @($false, $true)) {
        $suffix = if ($standalone) { '-self-contained' } else { '' }
        $folder = Join-Path $languageRoot "app$suffix"
        if ((Test-Path -LiteralPath $folder) -and (Get-ChildItem -LiteralPath $folder -Force)) { throw "Use a fresh OutputRoot; retaining existing output: $folder" }
        & dotnet publish $project -c Release -r win-x64 --self-contained $standalone.ToString().ToLowerInvariant() "-p:PracticeLanguage=$language" "-p:IntermediateOutputPath=obj/release-$language$suffix/" "-p:OutputPath=bin/release-$language$suffix/" -p:IncludeLocalBackground=true -p:DebugType=None -p:DebugSymbols=false -o $folder
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $language$suffix" }
        $exe = Join-Path $folder 'TH06NCTrainer.exe'
        if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.0.1.0') { throw 'Incorrect application version.' }
        $report = Join-Path $languageRoot "ui-test$suffix.txt"
        $test = Start-Process $exe -ArgumentList @('--ui-self-test', ('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
        if ($test.ExitCode -ne 0) { Get-Content -LiteralPath $report; throw "UI regression failed: $language$suffix" }
        if (-not (Select-String -LiteralPath $report -SimpleMatch "PASS: $language standalone language")) { throw 'Wrong package language.' }
        $docs = Join-Path $PSScriptRoot "distribution\$language"
        Copy-Item -LiteralPath (Join-Path $docs 'README.md'), (Join-Path $docs 'RELEASE_NOTES.md') -Destination $folder
        $archive = Join-Path $languageRoot "TH06NCPractice-v1.0.1-$language-win-x64$suffix.zip"
        if (Test-Path -LiteralPath $archive) { throw "Existing archive retained: $archive" }
        Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $folder).FullName -DestinationPath $archive
        $packages += $archive
    }
    Get-FileHash -LiteralPath $packages -Algorithm SHA256 |
        ForEach-Object { "$($_.Hash.ToLowerInvariant()) *$(Split-Path $_.Path -Leaf)" } |
        Set-Content -LiteralPath (Join-Path $languageRoot 'SHA256SUMS.txt') -Encoding ascii
}
Write-Host "Release packages: $OutputRoot"
