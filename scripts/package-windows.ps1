[CmdletBinding()]
param(
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version,
    [string]$ReleaseTag,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\CodexUsage.Windows\CodexUsage.Windows.csproj"
$publishDirectory = Join-Path $projectRoot "artifacts\publish\$RuntimeIdentifier"
$buildDirectory = Join-Path $projectRoot "artifacts\build-graph"
$installerDirectory = Join-Path $projectRoot "artifacts\installer"
$installerScript = Join-Path $projectRoot "installer\CodexUsage.iss"
$gitSafeDirectory = $projectRoot.Replace("\", "/")

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectFile = Get-Content -LiteralPath $projectPath
    $Version = @($projectFile.Project.PropertyGroup.Version | Where-Object { $_ })[0]
}

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = & git -c "safe.directory=$gitSafeDirectory" @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }

    return @($output)
}

$sourceRevision = (Invoke-Git -Arguments @("rev-parse", "HEAD") | Select-Object -First 1).Trim()
if ($sourceRevision -notmatch "^[0-9a-fA-F]{40}$") {
    throw "Git did not return a full source revision."
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseTag)) {
    if ($ReleaseTag -cne "v$Version") {
        throw "Release tag $ReleaseTag must exactly match version tag v$Version."
    }

    $tagRevision = (Invoke-Git -Arguments @("rev-list", "-n", "1", $ReleaseTag) | Select-Object -First 1).Trim()
    if (-not [string]::Equals($tagRevision, $sourceRevision, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release tag $ReleaseTag does not resolve to the checked-out source revision."
    }

    $workingTree = Invoke-Git -Arguments @("status", "--porcelain")
    if ($workingTree.Count -gt 0) {
        throw "Release packaging requires a clean Git working tree. Commit or discard local changes first."
    }
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
    $blockingProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            $processPath = $_.MainModule.FileName
            -not [string]::IsNullOrWhiteSpace($processPath) -and
                [IO.Path]::GetFullPath($processPath).StartsWith(
                    $resolvedPublishDirectory + [IO.Path]::DirectorySeparatorChar,
                    [StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            $false
        }
    }
    if ($blockingProcesses) {
        $blockingProcessList = ($blockingProcesses | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ", "
        throw "Publish output is in use by: $blockingProcessList. Exit CodexUsage and retry."
    }

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
    -p:UseArtifactsOutput=true `
    "-p:ArtifactsPath=$buildDirectory" `
    --disable-build-servers `
    -m:1 `
    -p:UseSharedCompilation=false `
    "-p:SourceRevisionId=$sourceRevision" `
    "-p:ContinuousIntegrationBuild=$(-not [string]::IsNullOrWhiteSpace($ReleaseTag))" `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained publish failed with exit code $LASTEXITCODE."
}

Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter "*.pdb" |
    Remove-Item -Force

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

$installerPath = Join-Path $installerDirectory "CodexUsage-Setup-$Version-win-x64.exe"
$checksumPath = "$installerPath.sha256"
$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$installerHash  $(Split-Path -Leaf $installerPath)" -Encoding ascii

Write-Output $installerPath
Write-Output $checksumPath
