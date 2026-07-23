# Architecture

## 현재 구조

```text
src/
├─ CodexUsage.Core/       도메인 모델과 provider 계약
├─ CodexUsage.Codex/      Codex 탐색, App Server 프로토콜, DTO와 mapper
├─ CodexUsage.Poc/        실환경 검증용 콘솔 진입점
├─ CodexUsage.Desktop/    공통 Avalonia 컨트롤, ViewModel, 디자인 토큰
└─ CodexUsage.macOS/      macOS 앱 수명, 메뉴 막대, 패키징 진입점
tests/
├─ CodexUsage.Core.Tests/
├─ CodexUsage.Codex.Tests/
└─ CodexUsage.Desktop.Tests/
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

`UsageViewModel`은 `ICodexUsageProvider`만 참조하고 60초 `PeriodicTimer`와 수동 새로고침을 제공합니다. 정상 응답이 한 번이라도 있으면 후속 오류에서 마지막 정상 값을 메모리에 유지하고 stale 상태를 함께 표시합니다. 디스크 캐시는 아직 없습니다.

화면의 요금제 표시는 `account/read`의 현재 account `planType`을 우선하며, 해당 값이 없을 때만 rate-limit bucket의 plan을 사용합니다. rate-limit plan은 특정 사용량 bucket에 연결되어 요금제 변경 직후 이전 플랜을 유지할 수 있습니다. 두 값은 Core snapshot에 계속 별도로 보존되어 프로토콜 불일치를 숨기지 않습니다.

열린 macOS 팝오버는 live/loading처럼 구조가 같은 갱신에서는 기존 프레임을 유지합니다. failure 또는 secondary-limit 유무가 바뀔 때만 content geometry를 다시 계산하며, 상태 항목 제목의 실제 변경으로 메뉴 막대 anchor가 이동하면 열린 팝오버를 새 anchor에 다시 맞춥니다.

macOS 프로젝트가 앱 수명과 `MacOSStatusItem`·`NativeUsagePopover` AppKit 어댑터를 소유합니다. `Program`은 프로세스 수명 동안 이름 있는 mutex를 보유해 같은 사용자 세션의 중복 실행을 즉시 종료합니다. `MenuBarPresentation`이 ViewModel 상태를 상태 항목 제목과 구조화된 primary/secondary limit 데이터로 변환하므로 AppKit 계층은 provider 응답을 알지 못합니다. 상태 항목은 `NSPopover`를 열고, 팝오버는 Minimal Split 시안의 헤더·사용/남음 2열·한도/초기화 행·footer를 네이티브 AppKit 뷰로 렌더링합니다. 앱은 Dock 없는 메뉴 막대 모드로 시작하고, 사용자가 `상세`를 선택할 때만 공통 Avalonia 창을 표시합니다. 명시적 종료는 ViewModel 취소, 창 닫기, 팝오버·상태 항목 해제 순서로 리소스를 정리합니다. 플랫폼 코드는 Core 또는 Codex 프로토콜 계층에 포함하지 않습니다.
