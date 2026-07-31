[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BaselineInstallerPath,
    [Parameter(Mandatory)]
    [string]$CandidateInstallerPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
    throw "This installer upgrade probe requires Windows."
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\qa"))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $qaRoot "installer-upgrade"
}

$BaselineInstallerPath = [IO.Path]::GetFullPath($BaselineInstallerPath)
$CandidateInstallerPath = [IO.Path]::GetFullPath($CandidateInstallerPath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $BaselineInstallerPath -PathType Leaf)) {
    throw "Baseline installer was not found: $BaselineInstallerPath"
}
if (-not (Test-Path -LiteralPath $CandidateInstallerPath -PathType Leaf)) {
    throw "Candidate installer was not found: $CandidateInstallerPath"
}
if (-not $OutputDirectory.StartsWith(
        $qaRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer probe output must remain under artifacts\qa."
}

$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C12312E9-92C6-4C1E-A337-54134A9FBA72}_is1"
$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CodexUsage.lnk"
if ((Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $startMenuShortcut)) {
    throw "A CodexUsage installation is already registered for this Windows user. Run this probe from a clean test account."
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$installDirectory = Join-Path $OutputDirectory "app"
$stateDirectory = Join-Path $OutputDirectory "state"
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$settingsPath = Join-Path $stateDirectory "settings.json"
$historyPath = Join-Path $stateDirectory "usage-history.json"
$cachePath = Join-Path $stateDirectory "usage-cache.json"
$positionPath = Join-Path $stateDirectory "widget-position.json"

@{
    Settings = "upgrade-preserve"
} | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding utf8
@{
    History = "upgrade-preserve"
} | ConvertTo-Json | Set-Content -LiteralPath $historyPath -Encoding utf8
@{
    Cache = "upgrade-preserve"
} | ConvertTo-Json | Set-Content -LiteralPath $cachePath -Encoding utf8
@{
    Position = "upgrade-preserve"
} | ConvertTo-Json | Set-Content -LiteralPath $positionPath -Encoding utf8
$stateHashesBefore = @{
    Settings = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
    History = (Get-FileHash -LiteralPath $historyPath -Algorithm SHA256).Hash
    Cache = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash
    Position = (Get-FileHash -LiteralPath $positionPath -Algorithm SHA256).Hash
}

function Get-InstallerInfo([string]$path) {
    $match = [regex]::Match((Split-Path -Leaf $path), '^CodexUsage-Setup-([0-9]+\.[0-9]+\.[0-9]+)-win-(x64|arm64)\.exe$')
    if (-not $match.Success) {
        throw "Installer file name must be CodexUsage-Setup-<version>-win-<x64|arm64>.exe: $path"
    }
    return [pscustomobject]@{
        Version = $match.Groups[1].Value
        Architecture = $match.Groups[2].Value
    }
}

function Install-CodexUsage([string]$installerPath) {
    $logPath = Join-Path $OutputDirectory "install-$([IO.Path]::GetFileNameWithoutExtension($installerPath)).log"
    $process = Start-Process -FilePath $installerPath -ArgumentList @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/DIR=`"$installDirectory`"",
        "/LOG=`"$logPath`"") -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        $logTail = if (Test-Path -LiteralPath $logPath) {
            (Get-Content -LiteralPath $logPath -Tail 12) -join [Environment]::NewLine
        }
        else {
            "No installer log was created."
        }
        throw "Installer exited with code $($process.ExitCode): $installerPath$([Environment]::NewLine)$logTail"
    }
}

function Get-InstalledVersion() {
    if (-not (Test-Path -LiteralPath $uninstallKey)) {
        throw "CodexUsage uninstall registration was not created."
    }
    return (Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion
}

$baselineInstaller = Get-InstallerInfo $BaselineInstallerPath
$candidateInstaller = Get-InstallerInfo $CandidateInstallerPath
if ($baselineInstaller.Architecture -ne $candidateInstaller.Architecture) {
    throw "Baseline and candidate installers must target the same architecture."
}
$expectedHostArchitecture = if ($candidateInstaller.Architecture -eq "arm64") { "Arm64" } else { "X64" }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -ne $expectedHostArchitecture) {
    throw "The $($candidateInstaller.Architecture) installer must be tested on a matching $expectedHostArchitecture Windows device."
}
$baselineVersion = $baselineInstaller.Version
$candidateVersion = $candidateInstaller.Version
$candidateProcess = $null
$probeFailure = $null
$cleanupFailure = $null
$report = $null
try {
    Install-CodexUsage $BaselineInstallerPath
    if ((Get-InstalledVersion) -ne $baselineVersion) {
        throw "Baseline installer version was not registered correctly."
    }

    Install-CodexUsage $CandidateInstallerPath
    if ((Get-InstalledVersion) -ne $candidateVersion) {
        throw "Candidate installer version was not registered correctly."
    }

    $executablePath = Join-Path $installDirectory "CodexUsage.exe"
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw "Candidate executable was not installed."
    }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).FileVersion -notlike "$candidateVersion*") {
        throw "Installed executable file version does not match candidate version $candidateVersion."
    }
    if (Get-ChildItem -LiteralPath $installDirectory -Recurse -File -Filter "*.pdb") {
        throw "The installed payload contains PDB files."
    }

    $stateHashesAfterInstall = @{
        Settings = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
        History = (Get-FileHash -LiteralPath $historyPath -Algorithm SHA256).Hash
        Cache = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash
        Position = (Get-FileHash -LiteralPath $positionPath -Algorithm SHA256).Hash
    }
    foreach ($name in $stateHashesBefore.Keys) {
        if ($stateHashesBefore[$name] -ne $stateHashesAfterInstall[$name]) {
            throw "Upgrade changed the preserved $name state file."
        }
    }

    $capturePath = Join-Path $OutputDirectory "candidate-widget.png"
    $launchStateDirectory = Join-Path $OutputDirectory "candidate-launch-state"
    New-Item -ItemType Directory -Path $launchStateDirectory -Force | Out-Null
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executablePath
    $startInfo.WorkingDirectory = $installDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.EnvironmentVariables["CODEX_USAGE_CAPTURE_PATH"] = $capturePath
    $startInfo.EnvironmentVariables["CODEX_USAGE_SETTINGS_PATH"] =
        (Join-Path $launchStateDirectory "settings.json")
    $startInfo.EnvironmentVariables["CODEX_USAGE_HISTORY_PATH"] =
        (Join-Path $launchStateDirectory "usage-history.json")
    $startInfo.EnvironmentVariables["CODEX_USAGE_CACHE_PATH"] =
        (Join-Path $launchStateDirectory "usage-cache.json")
    $startInfo.EnvironmentVariables["CODEX_USAGE_WIDGET_POSITION_PATH"] =
        (Join-Path $launchStateDirectory "widget-position.json")
    $candidateProcess = [Diagnostics.Process]::Start($startInfo)
    if (-not $candidateProcess.WaitForExit(45000)) {
        throw "Upgraded CodexUsage did not exit within the isolated launch probe timeout."
    }
    if ($candidateProcess.ExitCode -ne 0) {
        throw "Upgraded CodexUsage did not complete the isolated launch probe."
    }
    if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf)) {
        throw "Upgraded CodexUsage did not create its widget capture."
    }

    $report = [ordered]@{
        BaselineVersion = $baselineVersion
        CandidateVersion = $candidateVersion
        Architecture = $candidateInstaller.Architecture
        HostArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        InstalledVersion = Get-InstalledVersion
        InstalledFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).FileVersion
        PdbCount = @(Get-ChildItem -LiteralPath $installDirectory -Recurse -File -Filter "*.pdb").Count
        StateHashesPreserved = $true
        CapturePath = $capturePath
    }
}
catch {
    $probeFailure = $_
}

try {
    if ($null -ne $candidateProcess -and -not $candidateProcess.HasExited) {
        $candidateProcess.Kill($true)
        if (-not $candidateProcess.WaitForExit(15000)) {
            throw "Timed-out upgraded CodexUsage process did not exit after termination."
        }
    }

    $uninstallerPath = Join-Path $installDirectory "unins000.exe"
    if (Test-Path -LiteralPath $uninstallerPath -PathType Leaf) {
        $uninstaller = Join-Path $installDirectory "unins000.exe"
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @(
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-") -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) {
            throw "Uninstaller exited with code $($uninstall.ExitCode)."
        }
    }
    elseif (Test-Path -LiteralPath $installDirectory) {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }

    if (Test-Path -LiteralPath $uninstallKey) {
        Remove-Item -LiteralPath $uninstallKey -Recurse -Force
    }
    if (Test-Path -LiteralPath $startMenuShortcut) {
        Remove-Item -LiteralPath $startMenuShortcut -Force
    }

    $cleanupDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $candidateExited = $null -eq $candidateProcess -or $candidateProcess.HasExited
        $cleanupComplete = $candidateExited -and
            -not (Test-Path -LiteralPath $installDirectory) -and
            -not (Test-Path -LiteralPath $uninstallKey) -and
            -not (Test-Path -LiteralPath $startMenuShortcut)
        if ($cleanupComplete) {
            break
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $cleanupDeadline)

    if (-not $cleanupComplete) {
        throw "Installer probe cleanup did not remove all installation artifacts."
    }
}
catch {
    $cleanupFailure = $_
}

if ($null -ne $probeFailure -or $null -ne $cleanupFailure) {
    $messages = @()
    if ($null -ne $probeFailure) {
        $messages += "Probe failure: $($probeFailure.Exception.Message)"
    }
    if ($null -ne $cleanupFailure) {
        $messages += "Cleanup failure: $($cleanupFailure.Exception.Message)"
    }
    throw ($messages -join [Environment]::NewLine)
}

$report = $report + @{ CleanupSucceeded = $true }
$reportPath = Join-Path $OutputDirectory "upgrade-report.json"
$report | ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output $reportPath
