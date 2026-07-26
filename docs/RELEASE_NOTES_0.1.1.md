# CodexUsage 0.1.1

## Highlights

- Added a local weekly usage history view grouped by calendar month.
- Added monthly average and highest observed usage summaries, while preserving individual weekly windows and early reset events.
- Increased the default detail window height so usage cards and status information are visible without immediate scrolling.
- Refined the compact Current / History tabs.

## History and privacy

- History records peak observed weekly-limit usage and plan changes only. It does not estimate usage while the app is closed.
- All history remains local. Codex tokens, account identifiers, prompts, and conversation content are not collected or stored.

## Windows

- Added an English/Korean per-user Windows installer for x64 systems.
- The installer is self-contained; a separate .NET installation is not required.

## Notes

- Codex CLI must be installed and signed in for live usage data.
- This release is not code-signed, so Windows may show a SmartScreen warning.
