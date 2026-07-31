[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [string]$OutputDirectory,
    [ValidateRange(1000, 30000)]
    [int]$CaptureDelayMilliseconds = 5000,
    [ValidateRange(100, 500)]
    [int[]]$RequiredScalePercent = @()
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

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
        private static extern IntPtr SetThreadDpiAwarenessContext(
            IntPtr dpiAwarenessContext);

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
            var previousContext = SetThreadDpiAwarenessContext(new IntPtr(-4));

            try
            {
                uint dpiX;
                uint dpiY;
                return GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0
                    ? dpiX
                    : 0;
            }
            finally
            {
                SetThreadDpiAwarenessContext(previousContext);
            }
        }

        public static bool MoveWindowWithoutActivation(long handle, int x, int y)
        {
            const uint NoSize = 0x0001;
            const uint NoZOrder = 0x0004;
            const uint NoActivate = 0x0010;
            return SetWindowPos(
                new IntPtr(handle),
                IntPtr.Zero,
                x,
                y,
                0,
                0,
                NoSize | NoZOrder | NoActivate);
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
        [bool]$Locked,
        [System.Windows.Forms.Screen]$ExpectedScreen = $null
    )

    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $lastSnapshot = $null
    do {
        if ($Process.HasExited) {
            if ($null -ne $lastSnapshot) {
                throw "The CodexUsage widget HWND did not reach its native state before the capture probe exited: " +
                    "style=0x$('{0:X}' -f $lastSnapshot.ExtendedStyle), " +
                    "size=$($lastSnapshot.Width)x$($lastSnapshot.Height), dpi=$($lastSnapshot.Dpi)"
            }

            throw "CodexUsage exited before the widget HWND was available."
        }

        $snapshot = [CodexUsageQa.NativeWindowProbe]::FindWidget($Process.Id)
        if ($null -ne $snapshot) {
            $lastSnapshot = $snapshot
            $dpi = if ($snapshot.Dpi -eq 0) { 96 } else { $snapshot.Dpi }
            $expectedWidth = [Math]::Ceiling(160 * $dpi / 96)
            $expectedHeight = [Math]::Ceiling(34 * $dpi / 96)
            $style = $snapshot.ExtendedStyle
            $hasRequiredStyles =
                ($style -band $topmostStyle) -ne 0 -and
                ($style -band $toolWindowStyle) -ne 0 -and
                ($style -band $layeredStyle) -ne 0 -and
                ($style -band $noActivateStyle) -ne 0
            $clickThroughMatches =
                (($style -band $transparentStyle) -ne 0) -eq $Locked
            $matchesExpectedScreen =
                $null -eq $ExpectedScreen -or
                ($snapshot.Left -ge $ExpectedScreen.WorkingArea.Left -and
                 $snapshot.Top -ge $ExpectedScreen.WorkingArea.Top -and
                 ($snapshot.Left + $snapshot.Width) -le $ExpectedScreen.WorkingArea.Right -and
                 ($snapshot.Top + $snapshot.Height) -le $ExpectedScreen.WorkingArea.Bottom)
            if ($hasRequiredStyles -and
                $clickThroughMatches -and
                $snapshot.Width -eq $expectedWidth -and
                $snapshot.Height -eq $expectedHeight -and
                $matchesExpectedScreen) {
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
        [System.Windows.Forms.Screen]$TargetScreen,
        [System.Windows.Forms.Screen]$TransitionTargetScreen = $null
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
        $transition = $null
        $effectiveTargetScreen = $TargetScreen
        if ($null -ne $TransitionTargetScreen) {
            $transitionX = $TransitionTargetScreen.WorkingArea.Left + 40
            $transitionY = $TransitionTargetScreen.WorkingArea.Top + 40
            if (-not [CodexUsageQa.NativeWindowProbe]::MoveWindowWithoutActivation(
                    $snapshot.Handle,
                    $transitionX,
                    $transitionY)) {
                throw "The widget could not be moved to the mixed-DPI target monitor without activation."
            }

            $initialSnapshot = $snapshot
            $snapshot = Wait-ForWidget `
                -Process $process `
                -Locked $Locked `
                -ExpectedScreen $TransitionTargetScreen
            $effectiveTargetScreen = $TransitionTargetScreen
            $transition = [ordered]@{
                FromPosition = "$($initialSnapshot.Left),$($initialSnapshot.Top)"
                FromPhysicalSize = "$($initialSnapshot.Width)x$($initialSnapshot.Height)"
                FromDpi = $initialSnapshot.Dpi
                ToPosition = "$($snapshot.Left),$($snapshot.Top)"
                ToPhysicalSize = "$($snapshot.Width)x$($snapshot.Height)"
                ToDpi = $snapshot.Dpi
                TargetScreen = $TransitionTargetScreen.DeviceName
            }
        }
        Start-Sleep -Milliseconds 100
        $foregroundAfter = [CodexUsageQa.NativeWindowProbe]::ForegroundWindow()
        $startupDuring = Get-CodexUsageStartupRegistration

        $expectedDpi = if ($snapshot.Dpi -eq 0) { 96 } else { $snapshot.Dpi }
        $expectedWidth = [Math]::Ceiling(160 * $expectedDpi / 96)
        $expectedHeight = [Math]::Ceiling(34 * $expectedDpi / 96)
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
                $snapshot.Left -ge $effectiveTargetScreen.WorkingArea.Left -and
                $snapshot.Top -ge $effectiveTargetScreen.WorkingArea.Top -and
                ($snapshot.Left + $snapshot.Width) -le $effectiveTargetScreen.WorkingArea.Right -and
                ($snapshot.Top + $snapshot.Height) -le $effectiveTargetScreen.WorkingArea.Bottom
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
            $effectiveTargetScreen.Bounds.Left,
            $snapshot.Left - $contextPadding)
        $contextTop = [Math]::Max(
            $effectiveTargetScreen.Bounds.Top,
            $snapshot.Top - $contextPadding)
        $contextRight = [Math]::Min(
            $effectiveTargetScreen.Bounds.Right,
            $snapshot.Left + $snapshot.Width + $contextPadding)
        $contextBottom = [Math]::Min(
            $effectiveTargetScreen.Bounds.Bottom,
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
            $savedPosition.X -ge $effectiveTargetScreen.WorkingArea.Left -and
            $savedPosition.Y -ge $effectiveTargetScreen.WorkingArea.Top -and
            $savedPosition.X -lt $effectiveTargetScreen.WorkingArea.Right -and
            $savedPosition.Y -lt $effectiveTargetScreen.WorkingArea.Bottom
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
            Transition = $transition
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
$screenScaleEntries = @($screens | ForEach-Object {
    $centerX = $_.Bounds.Left + [Math]::Max(0, [int]($_.Bounds.Width / 2))
    $centerY = $_.Bounds.Top + [Math]::Max(0, [int]($_.Bounds.Height / 2))
    $dpi = [CodexUsageQa.NativeWindowProbe]::DpiAtPoint($centerX, $centerY)
    [pscustomobject]@{
        Screen = $_
        Dpi = $dpi
        ScalePercent = if ($dpi -eq 0) { $null } else { [Math]::Round($dpi * 100 / 96) }
    }
})
$actualScalePercent = @($screenScaleEntries | ForEach-Object ScalePercent | Where-Object { $null -ne $_ })
$requestedScalePercent = @($RequiredScalePercent | Sort-Object -Unique)
$missingScalePercent = @($requestedScalePercent | Where-Object { $_ -notin $actualScalePercent })
if ($missingScalePercent.Count -gt 0) {
    $actualDescription = if ($actualScalePercent.Count -eq 0) { "unknown" } else { $actualScalePercent -join ", " }
    throw "Required display scale percent is unavailable: $($missingScalePercent -join ', '). Actual monitor scale percent: $actualDescription. Change Windows display scaling and rerun this isolated probe."
}
$primaryScreen = @($screenScaleEntries | Where-Object { $_.Screen.Primary })[0].Screen
$secondaryScreen = if ($screens.Count -gt 1) { $screenScaleEntries[1].Screen } else { $primaryScreen }
$casePlans = if ($requestedScalePercent.Count -eq 0) {
    @(
        [pscustomobject]@{ Name = "editing-primary"; Locked = $false; TargetScreen = $primaryScreen }
        [pscustomobject]@{ Name = "locked-secondary"; Locked = $true; TargetScreen = $secondaryScreen }
    )
}
else {
    @($requestedScalePercent | ForEach-Object {
        $scale = $_
        $matchingScreens = @($screenScaleEntries | Where-Object { $_.ScalePercent -eq $scale })
        if ($matchingScreens.Count -eq 0) {
            throw "No target screen was found for required scale percent $scale."
        }

        $targetScreen = $matchingScreens[0].Screen
        [pscustomobject]@{ Name = "editing-scale-$scale"; Locked = $false; TargetScreen = $targetScreen }
        [pscustomobject]@{ Name = "locked-scale-$scale"; Locked = $true; TargetScreen = $targetScreen }
    })
}
if ($requestedScalePercent.Count -ge 2) {
    $firstScale = $requestedScalePercent[0]
    $lastScale = $requestedScalePercent[$requestedScalePercent.Count - 1]
    $firstScreen = @($screenScaleEntries | Where-Object { $_.ScalePercent -eq $firstScale })[0].Screen
    $lastScreen = @($screenScaleEntries | Where-Object { $_.ScalePercent -eq $lastScale })[0].Screen
    $casePlans += [pscustomobject]@{
        Name = "mixed-dpi-$firstScale-to-$lastScale"
        Locked = $false
        TargetScreen = $firstScreen
        TransitionTargetScreen = $lastScreen
    }
    $casePlans += [pscustomobject]@{
        Name = "mixed-dpi-$lastScale-to-$firstScale"
        Locked = $false
        TargetScreen = $lastScreen
        TransitionTargetScreen = $firstScreen
    }
}
$windowsVersion = Get-ItemProperty `
    "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" `
    -ErrorAction SilentlyContinue
$screenReports = @($screenScaleEntries | ForEach-Object {
    [ordered]@{
        DeviceName = $_.Screen.DeviceName
        Bounds = $_.Screen.Bounds.ToString()
        WorkingArea = $_.Screen.WorkingArea.ToString()
        Primary = $_.Screen.Primary
        Dpi = $_.Dpi
        ScalePercent = $_.ScalePercent
    }
})

$results = @($casePlans | ForEach-Object {
    Invoke-WidgetCase `
        -Name $_.Name `
        -Locked $_.Locked `
        -TargetScreen $_.TargetScreen `
        -TransitionTargetScreen $_.TransitionTargetScreen
})

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
    RequiredScalePercent = $RequiredScalePercent
    ScreenCount = $screens.Count
    Screens = $screenReports
    Cases = $results
}

$reportPath = Join-Path $OutputDirectory "runtime-report.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8

Write-Output $reportPath
$results | Format-Table Name, Locked, ProcessExitCode, Position, PhysicalSize, Dpi, ExtendedStyle
