# 0006: Restore only bounded, display-safe usage snapshots

## Status

Accepted.

## Decision

CodexUsage may keep one local last-known-good snapshot so a temporary lookup failure immediately after restart does not leave the widget empty.

The cache stores only the fields needed to render short-term and weekly usage: observed percentage, window duration, reset time, retrieval time, and constrained plan labels. It does not store Codex credentials, account identifiers, server-supplied limit text, prompts, or conversations. The file is written atomically and malformed files are preserved separately before recovery.

A cached snapshot is clearly shown as stale, is ignored after 24 hours, and drops any limit whose reset time has already passed. It is not shown when the live provider reports a signed-out or expired-authentication state because the app intentionally stores no account identifier with which to prove that a cache belongs to the current session.

Transient live failures use retry delays of 5 seconds, 15 seconds, 30 seconds, 1 minute, then at most 5 minutes. A successful response returns to the normal 60-second interval. Windows resume and restored network availability wake the same refresh loop through a coalesced signal.

## Consequences

- Temporary network and App Server failures preserve useful context across app restarts.
- The cache cannot be used to infer or match a Codex account.
- A signed-out user sees no cached percentage even if a recent snapshot exists.
- Sleep and network recovery do not require short polling timers.
