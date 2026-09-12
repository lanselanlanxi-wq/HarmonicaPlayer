$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project .\Tests\ParserTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Publishing cancelled.' }

    $version = '0.1.3'
    $buildStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $outputDir = Join-Path $PSScriptRoot "bin\Release\publish-$version-$buildStamp"
    dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outputDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. ZIP was not created.' }
    if (!(Test-Path (Join-Path $outputDir 'HarmonicaPlayer.exe'))) { throw 'EXE missing.' }

    Copy-Item .\rhythm-demo.txt $outputDir
    Copy-Item .\README.md $outputDir
    $zipPath = Join-Path $PSScriptRoot "bin\Release\HarmonicaPlayer-v$version-win-x64.zip"
    Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath $zipPath -Force
    Write-Host "ZIP: $zipPath"
    Write-Host 'Run the new EXE and verify hotkeys before publishing the ZIP.'
    explorer.exe $outputDir
}
finally { Pop-Location }
