param(
    [string]$TestIdentity,
    [string]$VersionOverride,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if ($TestIdentity -and $TestIdentity -notmatch '^[a-zA-Z0-9-]{1,40}$') { throw 'TestIdentity must contain only letters, numbers or hyphens.' }
if ($VersionOverride -and -not $TestIdentity) { throw 'VersionOverride is only supported for isolated QA packages.' }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'dist\PixelBatch.exe'))) { throw 'Run build.ps1 first.' }

# Official portable build tools: nothing is installed into Windows.
$toolVersion = '3.14.1'
$toolRoot = Join-Path $projectRoot ".tools\wix-$toolVersion"
if (-not (Test-Path -LiteralPath (Join-Path $toolRoot 'light.exe'))) {
    $downloadDirectory = Join-Path $projectRoot '.tools\downloads'
    New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
    $archivePath = Join-Path $downloadDirectory 'wix314-binaries.zip'
    Invoke-WebRequest -Uri 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip' -OutFile $archivePath
    $expectedHash = '6AC824E1642D6F7277D0ED7EA09411A508F6116BA6FAE0AA5F2C7DAA2FF43D31'
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'WiX build tool download checksum mismatch.' }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $toolRoot -Force
}

[xml]$projectXml = Get-Content -LiteralPath (Join-Path $projectRoot 'PixelBatch.csproj')
$releaseVersion = if ($VersionOverride) { $VersionOverride } else { [string]$projectXml.Project.PropertyGroup.Version }
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Installer version must have three numeric parts.' }
$appName = if ($TestIdentity) { 'PixelBatch QA ' + $TestIdentity } else { 'PixelBatch' }
$registryName = if ($TestIdentity) { 'PixelBatch-QA-' + $TestIdentity } else { 'PixelBatch' }
$family = if ($TestIdentity) { 'PixelBatch-QA-' + $TestIdentity } else { 'PixelBatch-Production' }
function Get-StableGuid([string]$key) {
    $bytes = [System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key))
    return '{' + [Guid]::new([byte[]]$bytes[0..15]).ToString().ToUpperInvariant() + '}'
}
if (-not $OutputDirectory) { $OutputDirectory = $projectRoot }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$workName = $family + '-' + $releaseVersion
$workRoot = Join-Path $projectRoot ('.tools\installer-work\' + $workName)
New-Item -ItemType Directory -Path $workRoot -Force | Out-Null

# Every installed file is explicitly tracked; user-created files are never wildcard-deleted.
$distRoot = Join-Path $projectRoot 'dist'
$xmlSettings = [System.Xml.XmlWriterSettings]::new()
$xmlSettings.Indent = $true
$xmlSettings.Encoding = [Text.UTF8Encoding]::new($false)
$payloadSource = Join-Path $workRoot 'Payload.wxs'
$writer = [System.Xml.XmlWriter]::Create($payloadSource, $xmlSettings)
$wixNamespace = 'http://schemas.microsoft.com/wix/2006/wi'
try {
    $writer.WriteStartDocument()
    $writer.WriteStartElement('Wix', $wixNamespace)
    $writer.WriteStartElement('Fragment', $wixNamespace)
    $writer.WriteStartElement('ComponentGroup', $wixNamespace)
    $writer.WriteAttributeString('Id', 'ApplicationFiles')
    $payloadFiles = @(Get-ChildItem -LiteralPath $distRoot -Recurse -File | Sort-Object FullName)
    $payloadIndex = 0
    foreach ($payloadFile in $payloadFiles) {
        $relativePath = [IO.Path]::GetRelativePath($distRoot, $payloadFile.FullName)
        if ($relativePath -match '\.(pdb|msi|exe)$' -and $relativePath -ne 'PixelBatch.exe') { throw "Unexpected release payload: $relativePath" }
        $fileDirectory = Split-Path $relativePath -Parent
        if ($fileDirectory -and $fileDirectory -ne 'Licenses') { throw "Unsupported payload subdirectory: $fileDirectory" }
        $componentId = 'Payload' + $payloadIndex
        $writer.WriteStartElement('Component', $wixNamespace)
        $writer.WriteAttributeString('Id', $componentId)
        $writer.WriteAttributeString('Guid', (Get-StableGuid "$family/$relativePath"))
        $writer.WriteAttributeString('Directory', $(if ($fileDirectory) { 'LicenseFolder' } else { 'INSTALLFOLDER' }))
        $writer.WriteAttributeString('Win64', 'yes')
        $writer.WriteStartElement('File', $wixNamespace)
        $writer.WriteAttributeString('Id', 'File' + $payloadIndex)
        $writer.WriteAttributeString('Source', $payloadFile.FullName)
        $writer.WriteEndElement()
        $writer.WriteStartElement('RegistryValue', $wixNamespace)
        $writer.WriteAttributeString('Root', 'HKCU')
        $writer.WriteAttributeString('Key', "Software\$registryName\Installer")
        $writer.WriteAttributeString('Name', $relativePath.Replace('\', '_'))
        $writer.WriteAttributeString('Type', 'integer')
        $writer.WriteAttributeString('Value', '1')
        $writer.WriteAttributeString('KeyPath', 'yes')
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $payloadIndex++
    }
    $writer.WriteEndElement(); $writer.WriteEndElement(); $writer.WriteEndElement(); $writer.WriteEndDocument()
}
finally { $writer.Dispose() }

$definitions = @(
    "-dProjectRoot=$projectRoot", "-dVersion=$releaseVersion", "-dAppName=$appName", "-dRegistryName=$registryName",
    ('-dProductCode=' + (Get-StableGuid "$family/product/$releaseVersion")),
    ('-dUpgradeCode=' + (Get-StableGuid "$family/upgrade")),
    ('-dBundleUpgradeCode=' + (Get-StableGuid "$family/bundle"))
)
if ($TestIdentity) { $definitions += '-dTestIdentity=' + $TestIdentity }
$candle = Join-Path $toolRoot 'candle.exe'
$light = Join-Path $toolRoot 'light.exe'
$msiPath = Join-Path $OutputDirectory ("PixelBatch-$releaseVersion-x64.msi")
$setupPath = Join-Path $OutputDirectory ("PixelBatch-Setup-$releaseVersion.exe")
& $candle -nologo -arch x64 @definitions -ext WixUIExtension -out ($workRoot + '\') (Join-Path $projectRoot 'Installer\Product.wxs') $payloadSource
if ($LASTEXITCODE -ne 0) { throw 'MSI source compilation failed.' }
# ICE91 only warns that per-user files cannot become per-machine; this package is intentionally per-user.
& $light -nologo -ext WixUIExtension -cultures:en-us -sice:ICE91 -out $msiPath (Join-Path $workRoot 'Product.wixobj') (Join-Path $workRoot 'Payload.wixobj')
if ($LASTEXITCODE -ne 0) { throw 'MSI validation or linking failed.' }
& $candle -nologo @definitions "-dMsiPath=$msiPath" -ext WixBalExtension -ext WixUtilExtension -out ($workRoot + '\') (Join-Path $projectRoot 'Installer\Bundle.wxs')
if ($LASTEXITCODE -ne 0) { throw 'Setup source compilation failed.' }
& $light -nologo -ext WixBalExtension -ext WixUtilExtension -out $setupPath (Join-Path $workRoot 'Bundle.wixobj')
if ($LASTEXITCODE -ne 0) { throw 'Setup linking failed.' }
foreach ($debugPath in @([IO.Path]::ChangeExtension($msiPath, '.wixpdb'), [IO.Path]::ChangeExtension($setupPath, '.wixpdb'))) {
    if (Test-Path -LiteralPath $debugPath) { Move-Item -LiteralPath $debugPath -Destination (Join-Path $workRoot ([IO.Path]::GetFileName($debugPath))) -Force }
}
Write-Host ('Setup: ' + $setupPath)
Write-Host ('MSI: ' + $msiPath)
