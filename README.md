# CodexUsage

CodexUsage is a small desktop utility that keeps your Codex usage limits visible while you work.

## What it does

- Shows short-term and weekly Codex usage in a compact Windows floating widget.
- Keeps the widget above normal application windows without stealing focus.
- Supports drag-to-position, click-through lock mode, position and display-state restore, and system-tray controls.
- Lets you choose 5-hour and weekly widget visibility, then adjust widget size, opacity, weekly progress visibility, and system/light/dark color mode.
- Can run at Windows sign-in and alert once at 80% and 95% usage within each reset window.
- Lets you pause usage alerts for a chosen number of hours without disabling the saved alert rules.
- Opens the detail window when a Windows usage notification is clicked.
- Provides a Settings window for per-limit alerts, custom thresholds, quiet hours, reset reminders, test notifications, and Windows startup repair.
- Reports recovered settings damage and can stage safe startup and notification defaults without clearing history or window placement. When settings cannot be read safely, the existing Windows startup registration is preserved until the user reviews and saves.
- Can retry Codex CLI detection after installation without restarting CodexUsage.
- Provides copyable, sanitized diagnostics with app, OS, CLI, lookup, and startup state.
- Provides a detail window with usage, reset, refresh, login, stale-data, and error states.
- Provides status-specific recovery actions for a missing CLI, sign-in checks, unsupported versions, and transient lookup failures.
- Restores a recent last-known-good usage snapshot after restart when live lookup is temporarily unavailable.
- Refreshes immediately after Windows resumes or the network returns, with bounded retry backoff during transient failures.
- Keeps an optional local history of weekly-limit peak observations and plan changes, with direct month selection, completed/partial observation filters, and explicit partial-observation labels; it never estimates usage while the app is closed.
- Restores the detail window size, position, and selected tab between launches.
- Supports keyboard refresh and tab navigation in the detail window, with Automation names for essential controls.
- Reads usage through the locally authenticated Codex App Server; it does not require a separate account or API key.

## Windows

Windows 10/11 x64 is the current primary target.

Install the latest `CodexUsage-Setup-*-win-x64.exe` from the project's releases. The installer is self-contained, so the target PC does not need a separate .NET installation.

Codex CLI must be installed and signed in before live usage data can be shown:

```powershell
npm install --global @openai/codex
```

The official PowerShell standalone installer is also supported. CodexUsage
detects its default Windows command location and a custom `CODEX_INSTALL_DIR`
location without reading authentication files.

Use the system tray to show or hide the widget, switch between edit and click-through modes, refresh data, open details, view About information, or quit the app.
The About window can copy diagnostics for issue reports. User profile paths are replaced with environment tokens, and credentials, account identifiers, usage values, and conversation data are not included.

## Build from source

```powershell
dotnet restore CodexUsage.sln
dotnet build CodexUsage.sln --configuration Release
dotnet test CodexUsage.sln --configuration Release
```

Create a Windows x64 installer with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-windows.ps1
```

The installer is written to `artifacts\installer`.
The package command also creates a SHA-256 checksum next to the installer.

## Privacy

CodexUsage runs locally. It does not read or save Codex tokens, browser cookies, passwords, or chat content, and it does not send usage data to an external service. A bounded local cache contains only the last observed percentages, reset timing, and normalized plan labels so temporary lookup failures can show clearly marked stale data. See [SECURITY.md](docs/SECURITY.md) for details.

## Status

The Windows widget is under active development. macOS support remains experimental.

CodexUsage is an independent utility and is not affiliated with or endorsed by OpenAI.
