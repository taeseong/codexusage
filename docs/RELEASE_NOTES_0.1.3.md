# CodexUsage 0.1.3

## Highlights

- Refined Settings and Detail windows with a Windows Fluent-style header, command bar, cards, and controls.
- Added independent widget visibility controls for 5-hour and weekly usage.
- Automatically hides the weekly progress bar when weekly usage is hidden.
- Prevents saving an empty widget configuration and keeps a safe weekly fallback for malformed local settings.

## Reliability and validation

- Prevented hidden limits from appearing in the widget tooltip or in partial App Server responses.
- Added coverage for widget visibility persistence, validation, tooltip filtering, and partial limit responses.

## Privacy

- Usage settings remain local. No tokens, account identifiers, prompts, or conversations are collected or transmitted.

## Notes

- Codex CLI must be installed and signed in for live usage data.
- The Windows installer is self-contained; a separate .NET installation is not required.
