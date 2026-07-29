[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [string]$OutputDirectory,
    [ValidateRange(1000, 30000)]
    [int]$CaptureDelayMilliseconds = 5000
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
    throw "This runtime probe requires Windows."
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$qaRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\qa"))

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot "artifacts\publish\win-x64\CodexUsage.exe"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $qaRoot "windows-runtime"
}

$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "CodexUsage executable was not found: $ExecutablePath"
}

if (-not $OutputDirectory.StartsWith(
        $qaRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Runtime probe output must remain under artifacts\qa."
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexUsageQa
{
    public sealed class WindowSnapshot
    {
        public long Handle { get; set; }
        public string Title { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Style { get; set; }
        public long ExtendedStyle { get; set; }
        public uint Dpi { get; set; }
        public bool Visible { get; set; }
        public bool Cloaked { get; set; }
    }

    public static class NativeWindowProbe
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            out int value,
            int valueSize);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(
            IntPtr monitor,
            int dpiType,
            out uint dpiX,
            out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destination,
            int destinationX,
            int destinationY,
            int width,
            int height,
            IntPtr source,
            int sourceX,
            int sourceY,
            uint operation);

        public static long ForegroundWindow()
        {
            return GetForegroundWindow().ToInt64();
        }

        public static uint DpiAtPoint(int x, int y)
        {
            var point = new POINT { X = x, Y = y };
            var monitor = MonitorFromPoint(point, 2);
            uint dpiX;
            uint dpiY;
            return GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0
                ? dpiX
                : 0;
        }

        public static bool CopyDesktop(
            IntPtr destination,
            int sourceX,
            int sourceY,
            int width,
            int height)
        {
            var source = GetDC(IntPtr.Zero);
            if (source == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                const uint SourceCopyWithLayeredWindows = 0x40CC0020;
                return BitBlt(
                    destination,
                    0,
                    0,
                    width,
                    height,
                    source,
                    sourceX,
                    sourceY,
                    SourceCopyWithLayeredWindows);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, source);
            }
        }

        public static WindowSnapshot FindWidget(int processId)
        {
            WindowSnapshot result = null;
            EnumWindowsProc callback = delegate(IntPtr hwnd, IntPtr data)
            {
                uint ownerProcessId;
                GetWindowThreadProcessId(hwnd, out ownerProcessId);
                if (ownerProcessId != processId)
                {
                    return true;
                }

                var title = new StringBuilder(256);
                GetWindowTextW(hwnd, title, title.Capacity);
                if (!String.Equals(title.ToString(), "CodexUsage", StringComparison.Ordinal))
                {
                    return true;
                }

                RECT rect;
                if (!GetWindowRect(hwnd, out rect))
                {
                    return true;
                }

                int cloaked;
                var cloakResult = DwmGetWindowAttribute(hwnd, 14, out cloaked, sizeof(int));
                result = new WindowSnapshot
                {
                    Handle = hwnd.ToInt64(),
                    Title = title.ToString(),
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top,
                    Style = GetWindowLongPtr(hwnd, -16).ToInt64(),
                    ExtendedStyle = GetWindowLongPtr(hwnd, -20).ToInt64(),
                    Dpi = GetDpiForWindow(hwnd),
                    Visible = IsWindowVisible(hwnd),
                    Cloaked = cloakResult == 0 && cloaked != 0,
                };
                return false;
            };

            EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return result;
        }
    }
}
'@

$topmostStyle = 0x00000008L
$transparentStyle = 0x00000020L
$toolWindowStyle = 0x00000080L
$layeredStyle = 0x00080000L
$noActivateStyle = 0x08000000L

function Wait-ForWidget {
    param(
        [Parameter(Mandatory)]
        [Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [bool]$Locked
    )

    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $lastSnapshot = $null
    do {
        if ($Process.HasExited) {
            throw "CodexUsage exited before the widget HWND was available."
        }

        $snapshot = [CodexUsageQa.NativeWindowProbe]::FindWidget($Process.Id)
        if ($null -ne $snapshot) {
            $lastSnapshot = $snapshot
            $dpi = if ($snapshot.Dpi -eq 0) { 96 } else { $snapshot.Dpi }
            $expectedWidth = [Math]::Round(160 * $dpi / 96)
            $expectedHeight = [Math]::Round(34 * $dpi / 96)
            $style = $snapshot.ExtendedStyle
            $hasRequiredStyles =
                ($style -band $topmostStyle) -ne 0 -and
                ($style -band $toolWindowStyle) -ne 0 -and
                ($style -band $layeredStyle) -ne 0 -and
                ($style -band $noActivateStyle) -ne 0
            $clickThroughMatches =
                (($style -band $transparentStyle) -ne 0) -eq $Locked
            if ($hasRequiredStyles -and
                $clickThroughMatches -and
                $snapshot.Width -eq $expectedWidth -and
                $snapshot.Height -eq $expectedHeight) {
                return $snapshot
            }
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($null -eq $lastSnapshot) {
        throw "The CodexUsage widget HWND was not found within 15 seconds."
    }

    throw "The CodexUsage widget HWND did not reach its native state within 15 seconds: " +
        "style=0x$('{0:X}' -f $lastSnapshot.ExtendedStyle), " +
        "size=$($lastSnapshot.Width)x$($lastSnapshot.Height), dpi=$($lastSnapshot.Dpi)"
}

function Get-CodexUsageStartupRegistration {
    $runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey(
        "Software\Microsoft\Windows\CurrentVersion\Run",
        $false)
    if ($null -eq $runKey) {
        return [pscustomobject]@{ Exists = $false; Value = $null }
    }

    try {
        $exists = @($runKey.GetValueNames()) -contains "CodexUsage"
        $value = if ($exists) {
            $runKey.GetValue(
                "CodexUsage",
                $null,
                [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        }
        else {
            $null
        }
        return [pscustomobject]@{ Exists = $exists; Value = $value }
    }
    finally {
        $runKey.Dispose()
    }
}

function Invoke-WidgetCase {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [bool]$Locked,
        [Parameter(Mandatory)]
        [System.Windows.Forms.Screen]$TargetScreen
    )

    $caseDirectory = Join-Path $OutputDirectory $Name
    New-Item -ItemType Directory -Path $caseDirectory -Force | Out-Null

    $positionPath = Join-Path $caseDirectory "widget-position.json"
    $capturePath = Join-Path $caseDirectory "widget.png"
    $desktopContextPath = Join-Path $caseDirectory "desktop-context.png"
    $settingsPath = Join-Path $caseDirectory "settings.json"
    $targetX = $TargetScreen.WorkingArea.Left + 40
    $targetY = $TargetScreen.WorkingArea.Top + 40
    $targetCenterX =
        $TargetScreen.Bounds.Left + [Math]::Max(0, [int]($TargetScreen.Bounds.Width / 2))
    $targetCenterY =
        $TargetScreen.Bounds.Top + [Math]::Max(0, [int]($TargetScreen.Bounds.Height / 2))
    $targetDpi = [CodexUsageQa.NativeWindowProbe]::DpiAtPoint(
        $targetCenterX,
        $targetCenterY)
    $targetScaling = if ($targetDpi -eq 0) { 1.0 } else { $targetDpi / 96.0 }
    $position = @{
        X = $targetX
        Y = $targetY
        Screen = @{
            BoundsX = $TargetScreen.Bounds.Left
            BoundsY = $TargetScreen.Bounds.Top
            BoundsWidth = $TargetScreen.Bounds.Width
            BoundsHeight = $TargetScreen.Bounds.Height
            Scaling = $targetScaling
        }
    }
    $position | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $positionPath -Encoding utf8
    [IO.File]::WriteAllText($settingsPath, "{`"runtimeProbeLocked`":true}")
    $settingsLock = [IO.File]::Open(
        $settingsPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
    $startupBefore = Get-CodexUsageStartupRegistration

    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $ExecutablePath
    $processInfo.WorkingDirectory = Split-Path -Parent $ExecutablePath
    $processInfo.UseShellExecute = $false
    $processInfo.EnvironmentVariables["CODEX_USAGE_CAPTURE_PATH"] = $capturePath
    $processInfo.EnvironmentVariables["CODEX_USAGE_CAPTURE_DELAY_MS"] =
        $CaptureDelayMilliseconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $processInfo.EnvironmentVariables["CODEX_USAGE_SETTINGS_PATH"] = $settingsPath
    $processInfo.EnvironmentVariables["CODEX_USAGE_HISTORY_PATH"] =
        (Join-Path $caseDirectory "usage-history.json")
    $processInfo.EnvironmentVariables["CODEX_USAGE_CACHE_PATH"] =
        (Join-Path $caseDirectory "usage-cache.json")
    $processInfo.EnvironmentVariables["CODEX_USAGE_WIDGET_POSITION_PATH"] = $positionPath
    if ($Locked) {
        $processInfo.EnvironmentVariables["CODEX_USAGE_START_LOCKED"] = "1"
    }

    $foregroundBefore = [CodexUsageQa.NativeWindowProbe]::ForegroundWindow()
    $process = $null
    try {
        $process = [Diagnostics.Process]::Start($processInfo)
        $snapshot = Wait-ForWidget -Process $process -Locked $Locked
        Start-Sleep -Milliseconds 100
        $foregroundAfter = [CodexUsageQa.NativeWindowProbe]::ForegroundWindow()
        $startupDuring = Get-CodexUsageStartupRegistration

        $expectedDpi = if ($snapshot.Dpi -eq 0) { 96 } else { $snapshot.Dpi }
        $expectedWidth = [Math]::Round(160 * $expectedDpi / 96)
        $expectedHeight = [Math]::Round(34 * $expectedDpi / 96)
        $extendedStyle = $snapshot.ExtendedStyle

        $checks = [ordered]@{
            Topmost = ($extendedStyle -band $topmostStyle) -ne 0
            ToolWindow = ($extendedStyle -band $toolWindowStyle) -ne 0
            Layered = ($extendedStyle -band $layeredStyle) -ne 0
            NoActivate = ($extendedStyle -band $noActivateStyle) -ne 0
            ClickThroughMatchesMode =
                (($extendedStyle -band $transparentStyle) -ne 0) -eq $Locked
            Visible = $snapshot.Visible
            NotCloaked = -not $snapshot.Cloaked
            PhysicalWidth = $snapshot.Width -eq $expectedWidth
            PhysicalHeight = $snapshot.Height -eq $expectedHeight
            TargetMonitor =
                $snapshot.Left -ge $TargetScreen.WorkingArea.Left -and
                $snapshot.Top -ge $TargetScreen.WorkingArea.Top -and
                ($snapshot.Left + $snapshot.Width) -le $TargetScreen.WorkingArea.Right -and
                ($snapshot.Top + $snapshot.Height) -le $TargetScreen.WorkingArea.Bottom
            ForegroundNotWidget = $foregroundAfter -ne $snapshot.Handle
            ForegroundPreserved =
                $foregroundBefore -eq 0 -or
                $foregroundAfter -eq $foregroundBefore
            StartupRegistrationPreserved =
                $startupBefore.Exists -eq $startupDuring.Exists -and
                [string]::Equals(
                    [string]$startupBefore.Value,
                    [string]$startupDuring.Value,
                    [StringComparison]::Ordinal)
        }

        $failedChecks = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
        if ($failedChecks.Count -gt 0) {
            throw "Widget runtime checks failed: $(($failedChecks.Name) -join ', '); " +
                "style=0x$('{0:X}' -f $extendedStyle), " +
                "size=$($snapshot.Width)x$($snapshot.Height), " +
                "expected=$($expectedWidth)x$($expectedHeight), dpi=$expectedDpi"
        }

        $contextPadding = 80
        $contextLeft = [Math]::Max(
            $TargetScreen.Bounds.Left,
            $snapshot.Left - $contextPadding)
        $contextTop = [Math]::Max(
            $TargetScreen.Bounds.Top,
            $snapshot.Top - $contextPadding)
        $contextRight = [Math]::Min(
            $TargetScreen.Bounds.Right,
            $snapshot.Left + $snapshot.Width + $contextPadding)
        $contextBottom = [Math]::Min(
            $TargetScreen.Bounds.Bottom,
            $snapshot.Top + $snapshot.Height + $contextPadding)
        $contextWidth = $contextRight - $contextLeft
        $contextHeight = $contextBottom - $contextTop
        Start-Sleep -Milliseconds 500
        $desktopBitmap = [Drawing.Bitmap]::new($contextWidth, $contextHeight)
        try {
            $desktopGraphics = [Drawing.Graphics]::FromImage($desktopBitmap)
            try {
                $destination = $desktopGraphics.GetHdc()
                try {
                    if (-not [CodexUsageQa.NativeWindowProbe]::CopyDesktop(
                            $destination,
                            $contextLeft,
                            $contextTop,
                            $contextWidth,
                            $contextHeight)) {
                        throw "Desktop context capture failed."
                    }
                }
                finally {
                    $desktopGraphics.ReleaseHdc($destination)
                }
            }
            finally {
                $desktopGraphics.Dispose()
            }
            $desktopBitmap.Save($desktopContextPath, [Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $desktopBitmap.Dispose()
        }

        $waitMilliseconds = $CaptureDelayMilliseconds + 30000
        if (-not $process.WaitForExit($waitMilliseconds)) {
            throw "CodexUsage did not exit after the capture probe."
        }
        if ($process.ExitCode -ne 0) {
            throw "CodexUsage exited with code $($process.ExitCode)."
        }
        if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf)) {
            throw "The widget capture was not created."
        }

        $startupAfter = Get-CodexUsageStartupRegistration
        if ($startupBefore.Exists -ne $startupAfter.Exists -or
            -not [string]::Equals(
                [string]$startupBefore.Value,
                [string]$startupAfter.Value,
                [StringComparison]::Ordinal)) {
            throw "The Windows startup registration changed after the runtime probe."
        }

        $savedPosition = Get-Content -LiteralPath $positionPath -Raw | ConvertFrom-Json
        $savedOnTarget =
            $savedPosition.X -ge $TargetScreen.WorkingArea.Left -and
            $savedPosition.Y -ge $TargetScreen.WorkingArea.Top -and
            $savedPosition.X -lt $TargetScreen.WorkingArea.Right -and
            $savedPosition.Y -lt $TargetScreen.WorkingArea.Bottom
        if (-not $savedOnTarget) {
            throw "The saved widget position did not remain on the target monitor."
        }

        return [pscustomobject]@{
            Name = $Name
            Locked = $Locked
            ProcessExitCode = $process.ExitCode
            Hwnd = ("0x{0:X}" -f $snapshot.Handle)
            Position = "$($snapshot.Left),$($snapshot.Top)"
            PhysicalSize = "$($snapshot.Width)x$($snapshot.Height)"
            Dpi = $expectedDpi
            ExtendedStyle = ("0x{0:X}" -f $extendedStyle)
            ForegroundBefore = ("0x{0:X}" -f $foregroundBefore)
            ForegroundAfter = ("0x{0:X}" -f $foregroundAfter)
            StartupRegistrationBefore = $startupBefore
            StartupRegistrationAfter = $startupAfter
            Checks = $checks
            CapturePath = $capturePath
            DesktopContextPath = $desktopContextPath
            PositionPath = $positionPath
        }
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            if (-not $process.WaitForExit($CaptureDelayMilliseconds + 30000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
        $settingsLock.Dispose()
    }
}

$screens = @([System.Windows.Forms.Screen]::AllScreens)
$primaryScreen = @($screens | Where-Object Primary)[0]
$secondaryScreen = if ($screens.Count -gt 1) { $screens[1] } else { $primaryScreen }
$windowsVersion = Get-ItemProperty `
    "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" `
    -ErrorAction SilentlyContinue

$results = @(
    Invoke-WidgetCase -Name "editing-primary" -Locked $false -TargetScreen $primaryScreen
    Invoke-WidgetCase -Name "locked-secondary" -Locked $true -TargetScreen $secondaryScreen
)

$report = [ordered]@{
    GeneratedAt = [DateTimeOffset]::Now.ToString("O")
    OperatingSystem = [ordered]@{
        ProductName = $windowsVersion.ProductName
        DisplayVersion = $windowsVersion.DisplayVersion
        CurrentBuild = $windowsVersion.CurrentBuild
        EnvironmentVersion = [Environment]::OSVersion.VersionString
    }
    ExecutablePath = $ExecutablePath
    FileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath).FileVersion
    ScreenCount = $screens.Count
    Screens = @($screens | ForEach-Object {
        $centerX = $_.Bounds.Left + [Math]::Max(0, [int]($_.Bounds.Width / 2))
        $centerY = $_.Bounds.Top + [Math]::Max(0, [int]($_.Bounds.Height / 2))
        $dpi = [CodexUsageQa.NativeWindowProbe]::DpiAtPoint($centerX, $centerY)
        [ordered]@{
            DeviceName = $_.DeviceName
            Bounds = $_.Bounds.ToString()
            WorkingArea = $_.WorkingArea.ToString()
            Primary = $_.Primary
            Dpi = $dpi
            ScalePercent = if ($dpi -eq 0) { $null } else { [Math]::Round($dpi * 100 / 96) }
        }
    })
    Cases = $results
}

$reportPath = Join-Path $OutputDirectory "runtime-report.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8

Write-Output $reportPath
$results | Format-Table Name, Locked, ProcessExitCode, Position, PhysicalSize, Dpi, ExtendedStyle
