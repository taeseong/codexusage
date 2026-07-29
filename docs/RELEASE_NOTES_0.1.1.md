# CodexUsage 0.1.1

## Highlights

- Added a local weekly usage history view grouped by calendar month.
- Added monthly average and highest observed usage summaries, while preserving individual weekly windows and early reset events.
- Increased the default detail window height so usage cards and status information are visible without immediate scrolling.
- Refined the compact Current / History tabs.
- Added a centralized Windows Settings window with per-limit alerts, custom thresholds, quiet hours, and optional reset reminders.
- Added test notifications, Windows startup registration diagnosis and repair, and atomic settings recovery.
- Added in-process Codex CLI re-detection after installation and refreshed standard npm/WinGet search paths.
- Added sanitized About diagnostics and detail-window activation from clicked notifications.
- Added a privacy-bounded last-known-good usage cache for temporary lookup failures after restart.
- Added adaptive retry backoff and immediate refresh after network recovery or Windows resume.
- Restores the detail window position, size, and selected tab.
- Added History retry recovery, previous/next month navigation, and direct month selection.
- Added status-specific recovery actions for CLI installation guidance, sign-in rechecks, version updates, and transient lookup failures.
- Added explicit partial-observation labels and comparable-window counts to weekly history.
- Added `Ctrl+R`, `Ctrl+1`, and `Ctrl+2` detail-window shortcuts plus Automation names for essential controls.
- Added a visible recovery notice when Windows settings are damaged or unreadable.
- Added a staged `Restore defaults` action for startup and notification preferences while preserving history and window placement.

## History and privacy

- History records peak observed weekly-limit usage and plan changes only. It does not estimate usage while the app is closed.
- All history remains local. Codex tokens, account identifiers, prompts, and conversation content are not collected or stored.

## Windows

- Added an English/Korean per-user Windows installer for x64 systems.
- The installer is self-contained; a separate .NET installation is not required.
- Added reproducible isolated packaging, release checksums, and tag-based draft GitHub Release automation.

## Notes

- Codex CLI must be installed and signed in for live usage data.
- This release is not code-signed, so Windows may show a SmartScreen warning.
