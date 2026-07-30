# CodexUsage 0.1.4

## Highlights

- Added Windows support for the official PowerShell standalone Codex CLI installer.
- Detects the default standalone command at `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe`.
- Also supports a standalone command location selected with `CODEX_INSTALL_DIR`.

## Reliability and privacy

- Keeps npm `codex.cmd`, WinGet, WindowsApps, and PATH discovery as fallbacks.
- Runs standalone `codex.exe` directly and does not invoke the PowerShell `codex.ps1` shim, avoiding execution-policy issues.
- Does not read authentication files, tokens, or browser cookies.

## Notes

- Codex CLI must be installed and signed in before live usage data can be shown.
- The Windows installer is self-contained; a separate .NET installation is not required.
