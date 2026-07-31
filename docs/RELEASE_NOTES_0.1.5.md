# CodexUsage 0.1.5

## Highlights

- Improved high-DPI widget rendering on Windows.
- The widget now keeps its intended physical size at 125%, 150%, and 200% display scaling.
- Moving the widget between monitors with different display scales reapplies its size without taking focus.

## Reliability and validation

- Strengthened the Windows runtime validation tool so it reads monitor DPI with a per-monitor-V2 awareness context.
- Added automated checks for 100% to 200% and 200% to 100% mixed-DPI transitions.
- Verified topmost behavior, click-through mode, saved position, startup registration preservation, and clean process exit across the DPI test cases.

## Privacy

- Usage data remains local. This release does not collect or transmit tokens, account identifiers, prompts, conversations, or display information.

## Notes

- Codex CLI must be installed and signed in before live usage data can be shown.
- The Windows installer is self-contained; a separate .NET installation is not required.
