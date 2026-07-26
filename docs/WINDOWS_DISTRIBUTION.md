# Windows distribution

`scripts/package-windows.ps1` creates a self-contained Windows x64 publish folder and an Inno Setup installer. The resulting installation does not require the end user to install the .NET runtime.

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

For publish-only validation without Inno Setup:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1 -SkipInstaller
```

The installer places the app in `%LOCALAPPDATA%\Programs\CodexUsage`, creates a Start menu shortcut, optionally creates a desktop shortcut, and registers an uninstaller. It does not install Codex CLI or Node.js: users still need Codex CLI installed and signed in before usage data is available.

## Release checklist

1. Run the test suite and package script from a clean Release build.
2. Install the generated installer on a clean Windows x64 test account.
3. Verify launch, Codex CLI guidance, live usage lookup, tray controls, uninstall, and reinstall.
4. Code-sign the setup executable and installed application before public distribution to reduce Windows SmartScreen warnings.
