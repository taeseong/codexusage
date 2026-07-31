# Windows distribution

`scripts/package-windows.ps1` creates a self-contained Windows x64 or ARM64 publish folder and an Inno Setup installer. The resulting installation does not require the end user to install the .NET runtime.

## Prerequisites

- .NET 10 SDK
- Inno Setup 6 for the installer stage

Install Inno Setup if necessary:

```powershell
winget install --id JRSoftware.InnoSetup --exact
```

## Create a release installer

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1
```

Outputs:

- `artifacts/publish/win-x64/`: self-contained app files
- `artifacts/installer/CodexUsage-Setup-<version>-win-x64.exe`: per-user installer
- `artifacts/installer/CodexUsage-Setup-<version>-win-x64.exe.sha256`: SHA-256 checksum

The build graph uses an isolated directory under `artifacts/build-graph`, so a
running development build does not lock the package output. Packaging stops
with a clear error if an application is running directly from the publish
directory that is about to be replaced.
PDB symbol files are removed from the public publish payload.

For publish-only validation without Inno Setup:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1 -SkipInstaller
```

The installer places the app in `%LOCALAPPDATA%\Programs\CodexUsage`, creates a Start menu shortcut, optionally creates a desktop shortcut, and registers an uninstaller. It does not install Codex CLI or Node.js: users still need Codex CLI installed and signed in before usage data is available.

## ARM64 and portable packages

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1 -RuntimeIdentifier win-arm64
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1 -RuntimeIdentifier win-x64 -Portable
```

The first command writes `CodexUsage-Setup-<version>-win-arm64.exe`. `-Portable` additionally writes `artifacts/portable/CodexUsage-<version>-<runtime>-portable.zip` plus a SHA-256 checksum. A portable ZIP has no installer, uninstaller, shortcuts, or start-at-sign-in registration; launch `CodexUsage.exe` directly after extracting it.

## Release checklist

1. Run the test suite and package script from a clean Release build.
2. Install each generated installer on a clean matching Windows test account (x64 package on x64, ARM64 package on ARM64).
3. Verify launch, Codex CLI guidance, live usage lookup, tray controls, uninstall, and reinstall.
4. Code-sign the setup executable and installed application before public distribution to reduce Windows SmartScreen warnings.

For a repeatable native widget probe on a real Windows desktop, run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\qa-windows-runtime.ps1
```

The probe launches isolated editing and locked widget sessions, targets the primary
and secondary monitor when available, and verifies the live HWND's Topmost,
ToolWindow, Layered, NoActivate, click-through, physical size, foreground-focus,
position restore, clean exit, unchanged Windows startup registration, and both
isolated-window plus desktop-context capture
behavior. JSON evidence and screenshots
are written under `artifacts\qa\windows-runtime`. It records the actual host and
screen configuration; it does not substitute a 100% DPI host for mixed-DPI QA.

To make a particular physical display scale a required QA precondition, pass it explicitly. Run separate isolated probes after changing the Windows scale to 125%, 150%, and 200%; retain each output directory as evidence. The script reads each monitor with a per-monitor-V2 DPI awareness context, so its precondition reflects the actual monitor scale rather than PowerShell's DPI virtualization. It fails before launching the app if no connected monitor reports the requested scale. When a scale is required, it runs both editing and locked widget cases on a monitor at that scale instead of merely detecting that the scale exists elsewhere. When two or more required scales are present, it also moves an editing widget in both directions between the lowest and highest scale monitors without activation, and records the before/after DPI and physical size.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\qa-windows-runtime.ps1 `
  -RequiredScalePercent 125 `
  -OutputDirectory artifacts\qa\windows-runtime-125
```

For a clean-user upgrade check, run the baseline installer first through the
isolated probe and then the candidate installer. The probe refuses to run if
CodexUsage is already installed for that Windows user, verifies the registered
version and installed file version after the upgrade, launches the candidate,
checks that isolated settings/history/cache/position files remain intact, and
uninstalls it again:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\qa-windows-installer-upgrade.ps1 `
  -BaselineInstallerPath artifacts\installer\CodexUsage-Setup-0.1.1-win-x64.exe `
  -CandidateInstallerPath artifacts\installer\CodexUsage-Setup-0.1.4-win-x64.exe
```

On an ARM64 Windows device, pass matching ARM64 baseline and candidate installers instead. The script rejects mismatched package architectures before changing local installation state.

Pushing a version tag such as `v0.1.4` runs `.github/workflows/release-windows.yml`.
The workflow requires the tag, project version, and `docs/RELEASE_NOTES_<version>.md`
to match, checks that the tag resolves to the checked-out clean commit, and embeds a
short source revision in the build metadata before it creates a draft GitHub Release. Review the uploaded installer,
checksum, and release notes in GitHub before manually publishing the release.
