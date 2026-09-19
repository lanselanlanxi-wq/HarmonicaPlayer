$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project .\Tests\ParserTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Publishing cancelled.' }

    dotnet run --project .\Tests\WindowsSmoke\WindowsSmokeTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows close tests failed. Publishing cancelled.' }

    $version = '0.2.1'
    $buildStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $outputDir = Join-Path $PSScriptRoot "bin\Release\publish-$version-$buildStamp"
    dotnet publish .\HarmonicaPlayer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outputDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. ZIP was not created.' }
    if (!(Test-Path (Join-Path $outputDir 'HarmonicaPlayer.exe'))) { throw 'EXE missing.' }

    Copy-Item .\rhythm-demo.txt $outputDir
    Copy-Item .\score-format-demo.txt $outputDir
    Copy-Item .\CONTRIBUTIONS.md $outputDir
    Copy-Item .\UPDATE-0.2.1.md $outputDir
    Copy-Item .\README.md $outputDir
    Copy-Item -LiteralPath .\ai转谱模板.txt -Destination $outputDir
    Copy-Item -LiteralPath .\使用说明.txt -Destination $outputDir
    Copy-Item -LiteralPath '.\制谱说明.txt' -Destination $outputDir
    if (Test-Path -LiteralPath '.\简谱' -PathType Container) {
        Copy-Item -LiteralPath '.\简谱' -Destination $outputDir -Recurse -Force
    } else {
        Write-Warning '未找到简谱文件夹，本次仅打包内置示例谱。'
    }
    $zipPath = Join-Path $PSScriptRoot "bin\Release\HarmonicaPlayer-v$version-win-x64.zip"
    Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath $zipPath -Force
    Write-Host "ZIP: $zipPath"
    Write-Host 'Run the new EXE and verify hotkeys before publishing the ZIP.'
    explorer.exe $outputDir
}
finally { Pop-Location }
