# CodexUsage 0.1.5

## Highlights

- Improved high-DPI widget rendering on Windows.
- The widget calculates its intended physical size for 125%, 150%, and 200% display scaling.
- Moving the widget between monitors with different display scales reapplies its size without taking focus.

## Reliability and validation

- Strengthened the Windows runtime validation tool so it reads monitor DPI with a per-monitor-V2 awareness context.
- Added automated contract checks for 100% to 200% and 200% to 100% mixed-DPI transitions.
- On a real Windows 10 two-monitor host at 100% scaling, verified topmost behavior, click-through mode, saved position, startup-registration preservation, and clean process exit.

## Privacy

- Usage data remains local. This release does not collect or transmit tokens, account identifiers, prompts, conversations, or display information.

## Notes

- Codex CLI must be installed and signed in before live usage data can be shown.
- The Windows installer is self-contained; a separate .NET installation is not required.
- Real-device verification at 125%, 150%, 200%, mixed DPI, and Windows 11 is still pending; the automated checks do not replace those runtime checks.
