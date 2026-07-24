[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\CodexUsage.Windows\CodexUsage.Windows.csproj"
$publishDirectory = Join-Path $projectRoot "artifacts\publish\$RuntimeIdentifier"
$installerDirectory = Join-Path $projectRoot "artifacts\installer"
$installerScript = Join-Path $projectRoot "installer\CodexUsage.iss"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectFile = Get-Content -LiteralPath $projectPath
    $Version = @($projectFile.Project.PropertyGroup.Version | Where-Object { $_ })[0]
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "A package version is required. Set <Version> in $projectPath or pass -Version."
}

$publishRoot = Join-Path $projectRoot "artifacts\publish"
$resolvedPublishRoot = [IO.Path]::GetFullPath($publishRoot)
$resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
if (-not $resolvedPublishDirectory.StartsWith($resolvedPublishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must remain under artifacts\publish."
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --disable-build-servers `
    -m:1 `
    -p:UseSharedCompilation=false `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained publish failed with exit code $LASTEXITCODE."
}

if ($SkipInstaller) {
    Write-Output $publishDirectory
    exit 0
}

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $defaultLocations = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    foreach ($defaultIscc in $defaultLocations) {
        if (Test-Path -LiteralPath $defaultIscc) {
            $iscc = Get-Item -LiteralPath $defaultIscc
            break
        }
    }
}

if ($null -eq $iscc) {
    throw "Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact"
}

New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
$isccPath = if ($iscc -is [System.Management.Automation.CommandInfo]) {
    $iscc.Source
}
else {
    $iscc.FullName
}
& $isccPath "/DPublishDir=$publishDirectory" "/DOutputDir=$installerDirectory" "/DAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

Write-Output (Join-Path $installerDirectory "CodexUsage-Setup-$Version-win-x64.exe")
