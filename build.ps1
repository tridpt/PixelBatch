param([switch]$SkipChecks, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'PixelBatch.csproj'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Installer\Theme.xml') -Destination (Join-Path $PSScriptRoot 'Licenses\WIX-THEME.xml') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Installer\Theme.wxl') -Destination (Join-Path $PSScriptRoot 'Licenses\WIX-THEME.wxl') -Force
if (-not $SkipChecks) {
    dotnet run --project (Join-Path $PSScriptRoot 'Checks\PixelBatch.Checks.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet publish $projectFile -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $PSScriptRoot 'dist')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'dist') -Filter '*.pdb' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $PSScriptRoot 'dist\README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $PSScriptRoot 'dist\THIRD-PARTY-NOTICES.md') -Force
$licenseDirectory = Join-Path $PSScriptRoot 'dist\Licenses'
New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Licenses') -File | Copy-Item -Destination $licenseDirectory -Force
[xml]$projectXml = Get-Content -LiteralPath $projectFile
$releaseVersion = [string]$projectXml.Project.PropertyGroup.Version
$releaseArchive = Join-Path $PSScriptRoot ('PixelBatch-Windows-x64-' + $releaseVersion + '.zip')
Compress-Archive -Path (Join-Path $PSScriptRoot 'dist\*') -DestinationPath $releaseArchive -Force
Write-Host ('App: ' + (Join-Path $PSScriptRoot 'dist\PixelBatch.exe'))
Write-Host ('ZIP: ' + $releaseArchive)
if (-not $SkipInstaller) {
    & (Join-Path $PSScriptRoot 'build-installer.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
