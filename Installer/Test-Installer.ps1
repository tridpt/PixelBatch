param([string]$Directory)
$ErrorActionPreference = 'Stop'
if (-not ('PixelBatchShortcutReader' -as [type])) { Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ShortcutReader.cs.txt') -Raw) }
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Directory) { $Directory = Join-Path $projectRoot ('qa\installer-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Directory = [IO.Path]::GetFullPath($Directory)
if (Test-Path -LiteralPath $Directory) { throw 'Use a new QA directory for each run.' }
New-Item -ItemType Directory -Path $Directory | Out-Null
$identity = 'install-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$appName = 'PixelBatch QA ' + $identity
$registryName = 'PixelBatch-QA-' + $identity
$installPath = Join-Path $Directory 'Cài thử PixelBatch'
$checks = [Collections.Generic.List[string]]::new()
$newSetup = $null
$oldSetup = $null
$setupForCleanup = $null
$failure = $null
function Check([bool]$condition, [string]$description) {
    if (-not $condition) { throw $description }
    $checks.Add($description)
}
function Invoke-Setup([string]$path, [string[]]$setupArguments) {
    $setupProcess = Start-Process -FilePath $path -ArgumentList $setupArguments -PassThru -WindowStyle Hidden
    $setupProcess.WaitForExit()
    if ($setupProcess.ExitCode -notin @(0, 3010)) { throw "Setup failed with code $($setupProcess.ExitCode): $path" }
}
function Get-UserState {
    $state = [ordered]@{}
    foreach ($name in @('settings.json','presets.json','getting-started.json')) {
        $path = Join-Path $env:LOCALAPPDATA ('PixelBatch\' + $name)
        $state[$name] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
    }
    foreach ($name in @('Desktop','StartMenu')) {
        $folder = if ($name -eq 'Desktop') { [Environment]::GetFolderPath('DesktopDirectory') } else { [Environment]::GetFolderPath('Programs') }
        $state[$name] = @(Get-ChildItem -LiteralPath $folder -Filter '*PixelBatch*' -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
    }
    return $state | ConvertTo-Json -Compress
}
function Get-UninstallEntries {
    return @(Get-ChildItem -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' |
        Get-ItemProperty | Where-Object { $_.DisplayName -eq $appName -and $_.SystemComponent -ne 1 })
}
$initialUserState = Get-UserState
try {
    & (Join-Path $projectRoot 'build-installer.ps1') -TestIdentity $identity -VersionOverride '0.5.99' -OutputDirectory (Join-Path $Directory 'old')
    $oldSetup = Join-Path $Directory 'old\PixelBatch-Setup-0.5.99.exe'
    & (Join-Path $projectRoot 'build-installer.ps1') -TestIdentity $identity -OutputDirectory (Join-Path $Directory 'new')
    [xml]$projectXml = Get-Content -LiteralPath (Join-Path $projectRoot 'PixelBatch.csproj')
    $version = [string]$projectXml.Project.PropertyGroup.Version
    $newSetup = Join-Path $Directory ("new\PixelBatch-Setup-$version.exe")
    Check ((Test-Path -LiteralPath $oldSetup) -and (Test-Path -LiteralPath $newSetup)) 'Both isolated installer versions build and pass MSI validation'
    $setupForCleanup = $oldSetup
    Invoke-Setup $oldSetup @('/install','/quiet','/norestart','/log',('"' + (Join-Path $Directory 'install.log') + '"'),('InstallFolder="' + $installPath + '"'))
    $installedExe = Join-Path $installPath 'PixelBatch.exe'
    Check (Test-Path -LiteralPath $installedExe) 'Setup installs into a chosen path with spaces and Vietnamese characters'
    $distFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'dist') -Recurse -File)
    foreach ($file in $distFiles) {
        $relativePath = [IO.Path]::GetRelativePath((Join-Path $projectRoot 'dist'), $file.FullName)
        $installedFile = Join-Path $installPath $relativePath
        Check ((Test-Path -LiteralPath $installedFile) -and (Get-FileHash -LiteralPath $installedFile).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) ("Installed payload matches release: $relativePath")
    }
    $shortcuts = @(Get-ChildItem -LiteralPath (Join-Path $installPath 'QA-Shortcuts') -Filter '*.lnk' -Recurse -File)
    Check ($shortcuts.Count -eq 2) 'Desktop and Start menu shortcut definitions create two isolated shortcuts'
    foreach ($shortcut in $shortcuts) {
        $linkPaths = [PixelBatchShortcutReader]::Read($shortcut.FullName)
        Check ($linkPaths[0] -eq $installedExe -and $linkPaths[1].TrimEnd('\') -eq $installPath.TrimEnd('\')) 'Installed shortcut targets the installed EXE and working directory'
    }
    Check ((Get-UninstallEntries).Count -eq 1) 'Setup registers one visible per-user Windows uninstall entry'
    $sentinelPath = Join-Path $installPath 'user-created-file.txt'
    [IO.File]::WriteAllText($sentinelPath, 'Uninstall must preserve files that it did not install.')
    $sentinelHash = (Get-FileHash -LiteralPath $sentinelPath).Hash
    $setupForCleanup = $newSetup
    Invoke-Setup $newSetup @('/install','/quiet','/norestart','/log',('"' + (Join-Path $Directory 'upgrade.log') + '"'))
    Check ((Get-ItemProperty -LiteralPath ("HKCU:\Software\$registryName\Installer")).InstallFolder.TrimEnd('\') -eq $installPath.TrimEnd('\')) 'Upgrade remembers the custom installation folder without another path argument'
    Check ((Get-UninstallEntries).Count -eq 1) 'Upgrade replaces the previous version without a second visible uninstall entry'
    Check ((Get-FileHash -LiteralPath $sentinelPath).Hash -eq $sentinelHash) 'Upgrade preserves user-created files'
    Check ((Get-FileHash -LiteralPath $installedExe).Hash -eq (Get-FileHash -LiteralPath (Join-Path $projectRoot 'dist\PixelBatch.exe')).Hash) 'Upgrade preserves the exact current release binary'
    Invoke-Setup $newSetup @('/repair','/quiet','/norestart','/log',('"' + (Join-Path $Directory 'repair.log') + '"'))
    Check ((Test-Path -LiteralPath $installedExe) -and (Get-ChildItem -LiteralPath (Join-Path $installPath 'QA-Shortcuts') -Filter '*.lnk' -Recurse -File).Count -eq 2) 'Repair reuses the custom path and keeps both shortcuts'
    $uiDirectory = Join-Path $Directory 'installed-app'
    $appProcess = Start-Process -FilePath $installedExe -ArgumentList @('--self-test',('"' + $uiDirectory + '"')) -PassThru -WindowStyle Hidden
    $appProcess.WaitForExit()
    $uiReport = Get-Content -LiteralPath (Join-Path $uiDirectory 'ui-report.json') -Raw | ConvertFrom-Json
    Check ($appProcess.ExitCode -eq 0 -and $uiReport.passed) 'Installed self-contained EXE passes the complete UI and image-export checks'
    Invoke-Setup $newSetup @('/uninstall','/quiet','/norestart','/log',('"' + (Join-Path $Directory 'uninstall.log') + '"'))
    $setupForCleanup = $null
    Check (-not (Test-Path -LiteralPath $installedExe)) 'Uninstall removes the installed application'
    Check ((Get-UninstallEntries).Count -eq 0) 'Uninstall removes the visible Windows uninstall entry'
    Check (-not (Test-Path -LiteralPath (Join-Path $installPath 'QA-Shortcuts'))) 'Uninstall removes both shortcut definitions and empty shortcut folders'
    Check ((Test-Path -LiteralPath $sentinelPath) -and (Get-FileHash -LiteralPath $sentinelPath).Hash -eq $sentinelHash) 'Uninstall preserves user-created files'
    foreach ($file in $distFiles) {
        Check (-not (Test-Path -LiteralPath (Join-Path $installPath ([IO.Path]::GetRelativePath((Join-Path $projectRoot 'dist'), $file.FullName))))) ("Uninstall removes its tracked file: $($file.Name)")
    }
    Check ((Get-UserState) -eq $initialUserState) 'Installer QA leaves real settings, presets, guide state and user shortcuts unchanged'
}
catch { $failure = $_.Exception.ToString() }
finally {
    if ($setupForCleanup -and (Get-UninstallEntries).Count -gt 0) {
        try { Invoke-Setup $setupForCleanup @('/uninstall','/quiet','/norestart','/log',('"' + (Join-Path $Directory 'cleanup.log') + '"')) }
        catch { $failure += "`nCleanup: " + $_.Exception.ToString() }
    }
    @{ passed=($null -eq $failure); checks=$checks.ToArray(); error=$failure; identity=$identity } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Directory 'installer-report.json') -Encoding utf8
}
if ($failure) { throw $failure }
Write-Host ("PASS: $($checks.Count) installer checks. Report: " + (Join-Path $Directory 'installer-report.json'))
