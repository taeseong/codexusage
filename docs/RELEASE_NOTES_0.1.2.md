# CodexUsage 0.1.2

## Highlights

- Improved recovery when Windows settings are unreadable or cannot be preserved.
- Prevented recovery defaults from adding, repairing, or removing the existing Windows startup registration before the user reviews and saves.
- Reflected the actual startup registration in Settings and the tray while recovery is active.
- Routed startup and alert tray toggles to Settings during recovery so external state and saved preferences cannot diverge.
- Added a staged Restore defaults action for startup and notification preferences without clearing usage history, cached usage, or saved window placement.

## Reliability and validation

- Added repeatable native Windows runtime validation for Topmost, focus preservation, click-through mode, DPI-sized widget bounds, multi-monitor placement, and clean shutdown.
- Added desktop-context captures that include the layered widget above normal applications.
- Expanded Windows release and recovery regression coverage.

## Privacy

- Usage history, cache, settings, and diagnostics remain local.
- Codex tokens, browser cookies, account identifiers, prompts, and conversations are not collected or included in diagnostics.

## Notes

- Codex CLI must be installed and signed in for live usage data.
- The Windows installer is self-contained; a separate .NET installation is not required.
- This release is not code-signed, so Windows may show a SmartScreen warning.
