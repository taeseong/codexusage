[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [string]$OutputDirectory,
    [ValidateRange(10, 7200)]
    [int]$DurationSeconds = 300
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
    throw "This stability probe requires Windows."
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\qa"))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot "artifacts\publish\win-x64\CodexUsage.exe"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $qaRoot "windows-stability"
}

$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "CodexUsage executable was not found: $ExecutablePath"
}
if (-not $OutputDirectory.StartsWith(
        $qaRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Stability probe output must remain under artifacts\qa."
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$settingsPath = Join-Path $OutputDirectory "settings.json"
$historyPath = Join-Path $OutputDirectory "usage-history.json"
$cachePath = Join-Path $OutputDirectory "usage-cache.json"
$positionPath = Join-Path $OutputDirectory "widget-position.json"
$diagnosticsPath = Join-Path $OutputDirectory "diagnostics.log"
$reportPath = Join-Path $OutputDirectory "stability-report.json"
$captureVariableNames = @(
    "CODEX_USAGE_CAPTURE_PATH",
    "CODEX_USAGE_CAPTURE_DELAY_MS",
    "CODEX_USAGE_CAPTURE_TRAY_MENU",
    "CODEX_USAGE_CAPTURE_INSTALL_GUIDANCE",
    "CODEX_USAGE_CAPTURE_CLI_RETRY",
    "CODEX_USAGE_CAPTURE_DETAILS",
    "CODEX_USAGE_CAPTURE_HISTORY",
    "CODEX_USAGE_CAPTURE_ABOUT",
    "CODEX_USAGE_CAPTURE_DIAGNOSTICS",
    "CODEX_USAGE_CAPTURE_DIAGNOSTICS_TEXT_PATH",
    "CODEX_USAGE_CAPTURE_SETTINGS",
    "CODEX_USAGE_CAPTURE_TEST_NOTIFICATION"
)

$testEnvironment = [ordered]@{}
foreach ($name in $captureVariableNames) {
    # A blank value is treated as disabled by the app and also overrides any
    # capture variable inherited from the test host.
    $testEnvironment[$name] = ""
}
$testEnvironment["CODEX_USAGE_SETTINGS_PATH"] = $settingsPath
$testEnvironment["CODEX_USAGE_HISTORY_PATH"] = $historyPath
$testEnvironment["CODEX_USAGE_CACHE_PATH"] = $cachePath
$testEnvironment["CODEX_USAGE_WIDGET_POSITION_PATH"] = $positionPath
$testEnvironment["CODEX_USAGE_DIAGNOSTICS_LOG_PATH"] = $diagnosticsPath
$originalEnvironment = @{}
foreach ($name in $testEnvironment.Keys) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$process = $null
$failure = $null
$report = [ordered]@{
    GeneratedAt = [DateTimeOffset]::Now.ToString("O")
    ExecutablePath = $ExecutablePath
    FileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath).FileVersion
    DurationSecondsRequested = $DurationSeconds
    CaptureVariablesCleared = $captureVariableNames
}

try {
    foreach ($name in $testEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $testEnvironment[$name], "Process")
    }
    $process = Start-Process `
        -FilePath $ExecutablePath `
        -WorkingDirectory (Split-Path -Parent $ExecutablePath) `
        -WindowStyle Hidden `
        -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        if ($process.HasExited) {
            throw "CodexUsage exited before the stability measurement began (exit code $($process.ExitCode))."
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    $process.Refresh()
    $startCpu = $process.CPU
    $startWorkingSet = $process.WorkingSet64
    $startPrivateMemory = $process.PrivateMemorySize64
    $measurementStartedAt = [DateTimeOffset]::UtcNow
    Start-Sleep -Seconds $DurationSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "CodexUsage exited during the stability measurement (exit code $($process.ExitCode))."
    }

    $elapsedSeconds = ([DateTimeOffset]::UtcNow - $measurementStartedAt).TotalSeconds
    $report.ProcessId = $process.Id
    $report.DurationSecondsMeasured = [Math]::Round($elapsedSeconds, 1)
    $report.CpuPercent = [Math]::Round((($process.CPU - $startCpu) / $elapsedSeconds) * 100, 3)
    $report.StartWorkingSetMB = [Math]::Round($startWorkingSet / 1MB, 1)
    $report.EndWorkingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 1)
    $report.StartPrivateMemoryMB = [Math]::Round($startPrivateMemory / 1MB, 1)
    $report.EndPrivateMemoryMB = [Math]::Round($process.PrivateMemorySize64 / 1MB, 1)
    $report.Lifecycle = [string[]]@(Get-Content `
        -LiteralPath $diagnosticsPath `
        -ErrorAction SilentlyContinue | ForEach-Object { $_.ToString() })
    $report.ProcessAliveAtMeasurementEnd = $true
}
catch {
    $failure = $_.Exception.Message
    $report.Failure = $failure
    if ($null -ne $process) {
        $process.Refresh()
        $report.ProcessId = $process.Id
        $report.ProcessAliveAtMeasurementEnd = -not $process.HasExited
        if ($process.HasExited) {
            $report.ExitCode = $process.ExitCode
        }
    }
    $report.Lifecycle = [string[]]@(Get-Content `
        -LiteralPath $diagnosticsPath `
        -ErrorAction SilentlyContinue | ForEach-Object { $_.ToString() })
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $process.Kill()
            if (-not $process.WaitForExit(15000)) {
                $report.CleanupSucceeded = $false
                if ($null -eq $failure) {
                    $failure = "The owned CodexUsage process did not exit after cleanup."
                    $report.Failure = $failure
                }
            }
            else {
                $report.CleanupSucceeded = $true
            }
        }
        else {
            $report.CleanupSucceeded = $true
        }
    }
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], "Process")
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
}

if ($null -ne $failure) {
    throw $failure
}

Write-Output $reportPath
