# Architecture

## Weekly usage history

`UsageHistoryService` in Core turns successful weekly-limit snapshots into locally identified window observations. `resetsAt` is treated as a scheduling signal rather than an identity. The Windows JSON store writes `usage-history.json` atomically and the common detail window reads it through `IUsageHistoryStore`. Only peak observed percent, reset timing, and plan labels are retained; app-off time is not backfilled. CSV export is an explicit Save dialog action and serializes only those local observations; it has no automatic or network export path.

History presentation marks in-progress, early, uncertain, or incompletely observed normal windows as partial observations. This is derived at display time and does not change the history JSON schema or rollover rules. Comparable metrics show the count of eligible normal completed windows and are hidden when history is empty or unavailable.

## Windows preferences and release pipeline

Windows preferences stay in the local `settings.json`. The file is flushed to a temporary file and atomically replaced; an interrupted complete temporary file is recovered, while malformed files are preserved with a `.corrupt-*` suffix before safe defaults are used. The Settings window controls startup registration, startup status repair, per-limit notifications, custom warning and critical thresholds, quiet hours, reset reminders, and test notifications. Alert delivery history remains tied to a reset window so automatic refresh does not produce duplicate notifications. A reset schedule correction of up to five minutes keeps the current alert history; larger changes are evaluated with the window duration, last observation, and usage change so history resets only when rollover is observed.

The same settings file keeps non-sensitive presentation preferences: 5-hour and weekly widget visibility (at least one remains visible), widget scale (75–150%), opacity (65–100%), weekly progress visibility, and the system/light/dark theme preference. These values affect only local rendering and retain the default compact 160×34 logical widget at 100%. Users can also pause alerts for one to 24 hours; the pause stores only a local expiry timestamp, suppresses delivery without discarding per-window alert history, and automatically expires. History filters are view-only and never change `usage-history.json`.

The settings store distinguishes a normal first launch from malformed or unreadable settings. The app retains that recovery notice for the current session even if automatic state persistence later succeeds, then shows it in Settings. If the file was unreadable, or a malformed file could not be preserved, automatic settings writes pause so a transient I/O or permission failure cannot overwrite an uninspected file; an explicit successful Save resumes persistence. While that gate is active, launch initialization reads and reflects the current Windows startup registration without adding, repairing, or removing it, and tray startup or alert toggles route to Settings instead of creating an unsaved split state. `Restore defaults` only stages startup and notification defaults; it is applied by Save, resets notification deduplication history, and deliberately preserves usage history plus widget/detail placement.

The detail window restores its size, position, and selected tab. Restored coordinates are clamped to an available monitor work area. History storage flushes a temporary file before atomic replacement and can recover a complete temporary file after interruption.

Windows release builds use an isolated .NET artifacts graph, remove symbols from the public payload, compile the per-user Inno Setup package, and write a SHA-256 checksum. The same script supports `win-x64` and `win-arm64` and may emit an opt-in portable ZIP with its own checksum. Version tags are validated against the project version before a draft GitHub Release is created for manual review and publication. Tagged packages require a clean working tree and carry their source revision; an explicitly manual package from uncommitted work reports `local build` rather than incorrectly claiming that HEAD reproduces it.

Codex CLI discovery is reevaluated for every refresh. On Windows it combines the current process, user, and machine PATH values with the official PowerShell standalone install directory (`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe`), an optional `CODEX_INSTALL_DIR` override, and known npm, WinGet, WindowsApps, and user-local locations while continuing to ignore PowerShell `.ps1` shims. Each live account lookup requests the documented App Server managed-token refresh before interpreting `account: null` plus `requiresOpenaiAuth: true` as sign-in required; CodexUsage never reads or handles the token itself. The installation guidance can trigger the same live provider refresh without restarting the app and closes automatically after the CLI becomes discoverable.

The About window builds diagnostics locally. It invokes only `codex --version`, accepts only a constrained `codex-cli <version>` token from stdout, discards stderr, replaces known user and program roots with environment tokens, reduces unknown locations to a file name, and reports only app/OS architecture, lookup status, CLI status/source, and startup registration state. Its update check runs only after the user chooses it, reads GitHub's public latest-release response, and sends no Codex usage, account, or authentication data. Windows notification activation opens the existing detail window.

Release builds carry the source revision in assembly informational metadata. About displays only a short revision and diagnostics include that same revision. A bounded local diagnostics log retains up to 40 timestamped application lifecycle and usage-status enum events; it never accepts exception text, paths, tokens, account identifiers, percentages, prompts, or conversations. The log is best-effort and a write failure does not affect the usage refresh path.

## 현재 구조

```text
src/
├─ CodexUsage.Core/       도메인 모델과 provider 계약
├─ CodexUsage.Codex/      Codex 탐색, App Server 프로토콜, DTO와 mapper
├─ CodexUsage.Poc/        실환경 검증용 콘솔 진입점
├─ CodexUsage.Desktop/    공통 Avalonia 컨트롤, ViewModel, 디자인 토큰
├─ CodexUsage.macOS/      macOS 앱 수명, 메뉴 막대, 패키징 진입점
└─ CodexUsage.Windows/    Windows 앱 수명, 위젯, Win32 창 제어와 트레이
tests/
├─ CodexUsage.Core.Tests/
├─ CodexUsage.Codex.Tests/
├─ CodexUsage.Desktop.Tests/
└─ CodexUsage.Windows.Tests/
```

`CodexUsage.Core`는 Codex 원본 JSON을 알지 못합니다. `CodexUsage.Codex`가 버전별 프로토콜 DTO를 역직렬화하고 `RateLimitMapper`를 통해 `UsageLimit`과 `CodexUsageSnapshot`으로 변환합니다.

```text
Codex App Server JSONL
  -> Protocol DTO
  -> RateLimitMapper
  -> Core domain model
  -> ICodexUsageProvider
  -> PoC 또는 UsageViewModel
  -> Avalonia 상세 창과 격리된 AppKit 상태 항목
```

## 프로세스 경계

`LiveCodexUsageProvider`는 PATH에서 Codex 실행 파일을 찾습니다. Finder 실행처럼 PATH가 제한된 macOS에서는 공식 앱 번들의 `/Applications/Codex.app/Contents/Resources/codex`와 사용자 Applications·`.local/bin` 후보를 추가로 확인합니다. 발견한 실행 파일로 `codex app-server --stdio`를 자식 프로세스로 실행합니다. `AppServerClient`는 `initialize` 요청과 `initialized` 알림 뒤 `account/read`, `account/rateLimits/read` 요청을 보냅니다. 요청 ID로 응답을 연결하며 알림은 건너뜁니다.

표준 출력은 한 줄당 하나의 JSON 메시지로 비동기 처리합니다. 표준 오류는 버퍼 정지를 방지하기 위해 별도 비동기 drain으로 소비하고 보존하지 않습니다. timeout 또는 종료 시 해당 provider가 시작한 자식 프로세스만 정리합니다.

## 한도 매핑

서버의 `primary`/`secondary` 위치 자체를 단기/주간 의미로 가정하지 않습니다. `windowDurationMins`를 기준으로 하루 이하를 단기, 7일 이상을 주간, 그 사이는 알 수 없는 한도로 보존합니다. 이는 이번 실환경 응답에서 `primary`가 10,080분 주간 window였기 때문입니다.

## macOS 메뉴 막대 UI

`UsageViewModel`은 `ICodexUsageProvider`와 선택적 `IUsageSnapshotCache`만 참조합니다. 정상 상태에서는 60초마다 갱신하고, 네트워크·시간 초과·프로토콜 계열의 일시적 오류가 연속되면 5초, 15초, 30초, 1분, 최대 5분 순서로 재시도합니다. 외부 복구 신호는 대기 중인 갱신 루프를 깨우되 여러 신호를 하나로 합칩니다.

상세 창의 오류 카드는 typed 상태에 따라 고정된 복구 동작을 제공합니다. Codex CLI 미설치는 Windows 설치 안내 창을 다시 열고, 로그인·버전·일시적 오류는 기존 provider를 통해 다시 조회합니다. 원문 서버 오류나 인증정보는 복구 동작에 전달하지 않습니다. 상세 창은 `Ctrl+R`, `Ctrl+1`, `Ctrl+2`와 핵심 컨트롤의 Automation 이름을 제공합니다.

Windows의 `usage-cache.json`은 마지막 정상 snapshot 중 단기·주간 퍼센트, reset, window duration과 제한된 plan label만 원자적으로 저장합니다. 서버가 준 limit ID와 display name은 저장하지 않으며 토큰·계정 식별자도 포함하지 않습니다. 최대 24시간 전 데이터만 복원하고 이미 reset을 지난 limit은 제외합니다. 로그인 실패나 인증 만료에서는 이전 세션의 캐시를 표시하지 않습니다. 캐시의 쓰기 실패·손상은 현재 live 조회를 실패시키지 않으며, 손상 파일은 별도 보존합니다. 값이 변하지 않으면 매 refresh마다 다시 쓰지 않습니다.

Windows의 `WindowsRefreshRecoveryService`는 네트워크가 다시 사용 가능해지거나 시스템이 절전에서 복귀할 때 즉시 갱신을 요청합니다. 이벤트 구독은 앱 종료 시 해제되며, 복구 모니터 초기화 실패는 앱 전체 종료로 이어지지 않습니다.

화면의 요금제 표시는 `account/read`의 현재 account `planType`을 우선하며, 해당 값이 없을 때만 rate-limit bucket의 plan을 사용합니다. rate-limit plan은 특정 사용량 bucket에 연결되어 요금제 변경 직후 이전 플랜을 유지할 수 있습니다. 두 값은 Core snapshot에 계속 별도로 보존되어 프로토콜 불일치를 숨기지 않습니다.

열린 macOS 팝오버는 live/loading처럼 구조가 같은 갱신에서는 기존 프레임을 유지합니다. failure 또는 secondary-limit 유무가 바뀔 때만 content geometry를 다시 계산하며, 상태 항목 제목의 실제 변경으로 메뉴 막대 anchor가 이동하면 열린 팝오버를 새 anchor에 다시 맞춥니다.

macOS 프로젝트가 앱 수명과 `MacOSStatusItem`·`NativeUsagePopover` AppKit 어댑터를 소유합니다. `Program`은 프로세스 수명 동안 이름 있는 mutex를 보유해 같은 사용자 세션의 중복 실행을 즉시 종료합니다. `MenuBarPresentation`이 ViewModel 상태를 상태 항목 제목과 구조화된 primary/secondary limit 데이터로 변환하므로 AppKit 계층은 provider 응답을 알지 못합니다. 상태 항목은 `NSPopover`를 열고, 팝오버는 Minimal Split 시안의 헤더·사용/남음 2열·한도/초기화 행·footer를 네이티브 AppKit 뷰로 렌더링합니다. 앱은 Dock 없는 메뉴 막대 모드로 시작하고, 사용자가 `상세`를 선택할 때만 공통 Avalonia 창을 표시합니다. 명시적 종료는 ViewModel 취소, 창 닫기, 팝오버·상태 항목 해제 순서로 리소스를 정리합니다. 플랫폼 코드는 Core 또는 Codex 프로토콜 계층에 포함하지 않습니다.

## Windows 플로팅 위젯

`CodexUsage.Windows`는 공통 `UsageViewModel`과 `UsageLimitCard`를 사용하되 앱 수명, 플로팅 창, Win32 P/Invoke와 트레이를 소유합니다. 시작 창은 `WidgetSummaryViewModel`이 공통 ViewModel의 `MenuSummary`를 투영하는 `160×34` 요약 위젯이며, macOS와 동일한 두 벡터 경로의 `>_` 마크를 사용합니다. 전체 한도와 상태는 별도의 공통 `UsageWindow`에서 표시합니다. `WidgetInteractionState`가 편집/잠금 상태의 단일 출처이며 창과 트레이가 같은 상태를 관찰합니다.

위젯은 Avalonia의 투명·비활성 Topmost 창 설정에 더해 HWND에 `WS_POPUP`, `WS_EX_LAYERED`, `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`를 적용합니다. `WS_POPUP`은 일반 top-level 창의 최소 높이 보정을 제거하며, `SetWindowPos`가 현재 창 DPI로 환산한 실제 `160×34` 크기와 `HWND_TOPMOST`, `SWP_NOACTIVATE`를 함께 적용합니다. 잠금 상태에서만 `WS_EX_TRANSPARENT`를 추가합니다.

Windows 작업표시줄도 Topmost 그룹에 있으므로 클릭 후 위젯보다 앞으로 재정렬될 수 있습니다. `WindowsTopmostGuard`는 `SetWinEventHook`으로 `EVENT_SYSTEM_FOREGROUND`와 작업표시줄의 `EVENT_OBJECT_REORDER`만 관찰하고, UI 스레드에 중복 제거된 Topmost 재적용을 예약합니다. 짧은 반복 타이머나 지속 폴링은 사용하지 않으며, 재적용에도 `SWP_NOACTIVATE`를 사용해 현재 foreground를 유지합니다. 창 종료 시 두 WinEvent hook을 모두 해제합니다.

요약 위젯 전체는 편집 모드에서 드래그 표면으로 동작하며 더블클릭하거나 시스템 트레이의 `Open details`를 선택하면 상세 창을 표시합니다. 공간을 차지하는 인위젯 Lock 버튼은 두지 않고 시스템 트레이에서 편집/잠금을 전환합니다. 트레이 초기화가 실패하면 위젯을 편집 상태에 두어 클릭 통과에서 복구할 수 없는 상태를 방지합니다. 명시적 종료는 자동 갱신 취소, 상세/위젯 창 닫기, 요약 ViewModel과 트레이 해제, 애플리케이션 종료 순서로 수행합니다.

첫 조회 결과가 `CodexNotInstalled`이면 Windows 앱은 프로세스당 한 번 설치 안내 창을 표시합니다. 안내 창은 공식 PowerShell standalone 설치 명령을 우선으로, npm 명령을 대안으로 복사할 수 있게 하고, 설치 후 `다시 확인`으로 live provider를 재호출합니다. 후보 CLI가 여러 개면 최근 성공한 후보를 먼저 시도하고, 지원되지 않거나 프로토콜 오류인 후보는 다음 후보로 안전하게 fallback합니다. CLI를 찾으면 창을 자동으로 닫으므로 앱을 재실행할 필요가 없습니다. 앱이 Codex CLI를 자동 설치하거나 인증 파일을 직접 읽지는 않습니다.

설치 안내 창은 위젯이 있는 모니터의 작업 영역에서 위젯 아래에 우선 배치됩니다. 아래 공간이 부족하면 위·오른쪽·왼쪽 순으로 겹치지 않는 위치를 찾아 화면 내부로 보정합니다.

Windows 실행 파일, 설치 안내 창 제목 표시와 시스템 트레이는 공통 `Assets/codex-usage.ico`를 사용합니다. 이 ICO는 Explorer와 알림 영역의 DPI별 표시를 위해 16~256px 레이어를 포함하며, 기존 macOS 전용 자산을 Windows 트레이에 재사용하지 않습니다.
ICO의 `>_` 글리프는 설치 안내 창 헤더 아이콘과 같은 상대 크기와 선 굵기를 사용해 실행 파일·트레이·화면 표시 사이의 비율 차이를 방지합니다.

Windows 기본 트레이 메뉴는 글꼴과 여백을 앱에서 제어할 수 없으므로, `WindowsTrayIcon`은 Windows Forms `NotifyIcon`과 `ContextMenuStrip`을 사용합니다. `DarkTrayMenuRenderer`가 Segoe UI, 어두운 표면, 좌측 텍스트 정렬, hover와 구분선을 직접 렌더링합니다. 좌클릭은 위젯 표시/숨김을 전환하고 우클릭은 이 스타일 메뉴를 엽니다.

편집 모드의 요약 위젯은 Avalonia `ContextMenu`로 우클릭 `Quit` 항목을 제공합니다. 이 명령은 트레이의 `Quit`과 같은 앱 종료 경로를 사용합니다. 클릭 통과가 켜진 고정 모드에서는 위젯이 마우스 입력을 받지 않으므로 트레이의 복구·종료 메뉴를 사용합니다.

Windows 설정은 사용자 로컬 `settings.json`에 원자적으로 저장하며, 손상 파일은 별도 보존한 뒤 안전한 기본값으로 복구합니다. 위젯 표시 여부와 편집/잠금 상태, 로그인 시 실행 여부, 사용량 알림 활성화 상태 및 한도별 알림 이력을 보존합니다. 위치 파일에는 X/Y와 함께 마지막 화면의 bounds·DPI 배율을 저장해 화면 배치가 바뀌어도 가장 가까운 화면을 선택한 뒤 작업 영역 안으로 보정합니다. 자동 실행은 현재 사용자 `Run` 레지스트리 키만 사용하며 Settings에서 실제 등록 상태와 현재 실행 경로의 일치 여부를 확인하고 복구할 수 있습니다. 알림은 사용자가 지정한 경고·위험 임계치에서 reset 구간별 한 번씩 표시하며, 5분 이내의 reset 예정 시각 보정만으로는 알림 이력을 초기화하지 않습니다.

Windows의 Codex CLI 탐색은 새로고침마다 process/user/machine PATH, 공식 PowerShell standalone 기본 경로(`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe`), 선택적 `CODEX_INSTALL_DIR`, 표준 npm·WinGet 경로를 다시 확인합니다. About 진단은 로컬에서만 생성하고 사용자 경로를 환경 변수 토큰으로 치환하며, 사용량 알림 클릭은 기존 상세 창을 엽니다.
