# 0005: Weekly usage history is observation-based

CodexUsage records only the plan and weekly rate-limit observations returned by the local Codex App Server. It never records tokens, account identifiers, prompts, cookies, or authentication material.

A history item represents a server rate-limit window, not a calendar week. Each item has a locally generated ID because a server reset timestamp may change. The displayed value is the highest observed percentage; periods while the app is not running are not inferred. A detected early reset is retained as a separate item and excluded from normal-window averages.
