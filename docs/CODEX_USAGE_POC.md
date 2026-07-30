# Codex Usage PoC

## Windows PowerShell CLI discovery (2026-07-30)

The official PowerShell installer places its visible command at
`%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe` by default and supports
an installer-time `CODEX_INSTALL_DIR` override. Windows discovery probes both
locations on every refresh, in addition to existing npm, WinGet, WindowsApps,
and PATH candidates. It runs the selected executable directly (or an npm
`.cmd` shim through `cmd.exe`) and never invokes `codex.ps1`, so PowerShell
execution policy does not affect usage lookup. No authentication files, tokens,
or cookies are read as part of discovery.

## History consumer boundary

The history feature consumes only `usedPercent`, `windowDurationMins`, `resetsAt`, and the rate-limit/account plan values already returned by `account/rateLimits/read` and `account/read`. It does not access credentials, cookies, or any additional local Codex data.

The Windows last-known-good cache uses the same mapped domain snapshot and performs no additional Codex request. It stores only the two display-limit kinds, observed percentages, window/reset timing, retrieval time, and constrained plan labels. It does not persist server-provided limit text or account/authentication data.

## 검증 환경

- 최종 PoC 및 공식 화면 비교 시각: 2026-07-22 09:25 KST
- 운영체제: macOS 26.5 (Build 25F71)
- 아키텍처: Apple Silicon ARM64
- .NET SDK: 10.0.302, 임시 로컬 설치로 검증
- Codex CLI: 0.136.0
- Codex 위치: `/opt/homebrew/bin/codex`
- 로그인 상태 명령: `codex login status` 결과 `Logged in using ChatGPT`
- Avalonia: 이번 Phase에는 UI 프로젝트와 패키지를 추가하지 않음

## 조사한 연동 방식

1. `codex --help`, `codex login --help`, `codex app-server --help`로 설치본 기능을 확인했습니다.
2. `codex app-server generate-json-schema --experimental`로 설치 버전 자체의 JSON Schema를 생성했습니다.
3. 생성 스키마에서 `initialize`, `account/read`, `account/rateLimits/read`와 응답 필드를 확인했습니다.
4. stdio JSONL 프로브로 실제 로그인 계정 응답을 호출했습니다.
5. 최신 [공식 Codex App Server 문서](https://developers.openai.com/codex/app-server)에서 프로토콜, 초기화 순서, auth와 rate-limit endpoint 의미를 대조했습니다.

브라우저 쿠키, 웹페이지 스크래핑, 인증 파일 직접 읽기, 비공식 API 직접 호출은 조사 대상에서 제외했습니다.

## 실제 지원된 방식

Codex CLI 0.136.0은 `codex app-server --stdio`를 지원합니다. 공식 문서와 설치본 스키마 모두 newline-delimited JSON, 요청 ID 상관관계, `initialize` 후 `initialized`, `account/read`, `account/rateLimits/read` 흐름을 확인했습니다.

실제 요청 흐름은 다음과 같습니다.

```text
start codex app-server --stdio
-> initialize
<- initialize result
-> initialized
-> account/read { refreshToken: false }
<- account result
-> account/rateLimits/read
<- rate-limit result
-> close stdin and wait; kill owned process only if graceful timeout expires
```

## 민감정보 제거 응답 구조

실제 응답을 값과 필드 수준에서 정리한 결과입니다. 이메일과 인증 데이터는 수집하거나 기록하지 않았습니다.

```json
{
  "account": {
    "type": "chatgpt",
    "planType": "pro"
  },
  "rateLimits": {
    "limitId": "codex",
    "primary": {
      "usedPercent": 1,
      "windowDurationMins": 10080,
      "resetsAt": 1785284661
    },
    "secondary": null,
    "planType": "plus"
  }
}
```

해당 reset timestamp는 2026-07-29 09:24:21 KST입니다. `primary`가 주간 길이였으므로 primary를 단기라고 가정하지 않고 duration으로 분류합니다.

## 확인 결과

| 항목 | 결과 |
|---|---|
| Codex 설치 | 확인됨 |
| 로그인 상태 | ChatGPT 로그인 및 격리된 미로그인 상태 확인 |
| 사용량 endpoint | 확인됨 |
| 단기 사용률 | 현재 응답에 없음 |
| 주간 사용률 | 1% used, 99% remaining |
| 단기 reset | 현재 응답에 없음 |
| 주간 reset | 2026-07-29 09:24:21 KST |
| 계정 plan | `pro` |
| rate-limit snapshot plan | `plus`, 계정 plan과 불일치 |
| Mock | 사용하지 않음 |

플랜 불일치는 추측으로 합치지 않습니다. PoC는 account plan과 rate-limit plan을 별도로 보존하고 불일치를 표시합니다. 공식 App Server 문서상 account plan은 현재 ChatGPT 계정 요금제이고 rate-limit plan은 특정 bucket과 연결된 요금제입니다. 사용자 화면은 account plan을 우선 표시하고, 이 값이 없을 때만 rate-limit plan으로 대체합니다.

## 격리된 미로그인 상태 검증

2026-07-22에 사용자의 기존 로그인 저장소를 변경하지 않고 임시 빈 디렉터리를 `CODEX_HOME`으로 지정해 실제 설치본의 미로그인 경로를 검증했습니다.

```text
codex login status
-> Not logged in

account/read
-> account: null
-> requiresOpenaiAuth: true

CodexUsage.Poc
-> Authentication: Signed out
-> Usage provider: NotAuthenticated
-> Detail: Codex login is required.
```

프로브와 PoC 자식 프로세스에만 격리 환경 변수를 전달했으며 사용자의 일반 Codex 로그인 상태, 인증 파일 또는 실행 중인 Codex 앱은 변경하지 않았습니다. 이를 통해 mock 없이 실제 App Server의 미로그인 응답과 provider 상태 매핑이 일치함을 확인했습니다.

## 인증 만료 상태 조사

2026-07-22에 공식 App Server 문서와 Codex CLI 0.136.0이 생성한 JSON Schema를 다시 대조했습니다.

- 관리형 ChatGPT 로그인에서는 `account/read`의 `refreshToken: true`로 토큰 갱신을 요청할 수 있습니다.
- 공식 문서에는 관리형 ChatGPT 토큰 갱신 실패를 `AuthenticationExpired`로 안정적으로 구분할 수 있는 전용 오류 코드나 typed 응답 필드가 정의되어 있지 않습니다.
- 일반 JSON-RPC 오류 스키마는 숫자 `code`, 문자열 `message`, 선택적 `data`만 제공하며 인증 만료용 안정적 discriminator가 없습니다.
- `chatgptAuthTokens`는 공식 문서에서 experimental이고, 설치본 스키마에서도 unstable/internal-use 경로로 표시됩니다. 이 경로에 가짜 만료 토큰을 주입해 운영 동작을 추정하지 않았습니다.

사용자의 실제 로그인 토큰을 폐기하거나 로그아웃하는 테스트도 수행하지 않았습니다. 따라서 `AuthenticationExpired`는 도메인 상태로 예약되어 있지만 현재 live provider는 오류 메시지 문자열을 추측해 이 상태로 매핑하지 않습니다. 공식 typed 신호가 추가되거나 격리된 테스트 계정으로 실제 만료 상태를 안전하게 재현할 수 있을 때 별도로 검증해야 합니다.

## 실패하거나 제한된 방식

- 일반 샌드박스에서는 App Server가 Codex state SQLite를 열 때 read-only 오류가 발생했습니다. 권한이 있는 실제 사용자 실행에서는 성공했습니다.
- 설치본은 단기 window를 반환하지 않았으므로 단기 사용률과 reset을 검증하지 못했습니다.
- 관리형 ChatGPT 인증 만료를 구분하는 안정적인 공개 프로토콜 신호가 없어 `AuthenticationExpired` live 매핑은 미검증 상태입니다.
- 초기 기본 MSBuild 실행은 이 환경에서 공유 컴파일러가 정지했습니다. 공유 컴파일러와 build server를 끈 단일 노드 빌드는 정상 완료됐습니다.

## 공식 Usage 화면 비교

2026-07-22 09:25 KST에 동일한 Chrome 로그인 세션의 공식 Codex Usage 화면과 App Server 응답을 연속으로 확인했습니다. 쿠키, 토큰, 네트워크 응답 또는 페이지 내부 상태를 읽지 않고 화면에 렌더링된 값만 비교했습니다.

| 비교 항목 | App Server | 공식 Codex Usage 화면 | 결과 |
|---|---|---|---|
| 주간 사용률 | 1% used, 99% remaining | 99% 남음 | 일치 |
| 주간 reset | 2026-07-29 09:24:21 KST | 2026-07-29 09:24 초기화 | 화면 분 단위 정밀도에서 일치 |
| 사용률 방향 | `usedPercent` 1 | remaining 99 | used/remaining 방향 일치 |
| 단기 한도 | 응답에 없음 | 화면에 없음 | 일치 |
| rate-limit plan | `plus` | 프로필 표시 `PLUS` | 일치 |
| account plan | `pro` | 프로필 표시 `PLUS` | 불일치, 별도 보존 |

이 비교로 현재 live provider가 표시하는 주간 사용률과 reset의 정확성을 확인했습니다. `account/read`의 plan과 rate-limit bucket plan은 의미와 갱신 시점이 다르므로 두 출처를 도메인에서 별도로 보존합니다. 요금제 변경 직후에는 현재 account plan을 표시하고 기존 bucket의 plan이 새 구독을 덮어쓰지 않게 합니다.

## Windows 추가 검증

2026-07-23 21:50 KST에 Windows 환경에서 기존 PoC를 실행했습니다.

- 운영체제: Windows 10 Home 22H2 x64, build 19045.6466
- .NET SDK: 10.0.302
- Codex CLI: 0.145.0-alpha.30
- 설치 위치: Microsoft Store Codex 앱의 `app/resources/codex.exe`
- 로그인 상태: `Logged in using ChatGPT`
- Provider: `LiveCodexUsageProvider`
- Mock: 사용하지 않음

검증 샌드박스 계정은 WindowsApps 실행 정책 때문에 설치 위치의 실행 파일을 직접 시작할 수 없었습니다. 동일한 설치 파일을 사용자 임시 디렉터리에 복사하고, 자식 프로세스에만 사용자의 기존 `CODEX_HOME`을 지정해 App Server를 실행했습니다. 인증 파일 내용은 읽거나 출력하지 않았습니다. 일반 사용자 컨텍스트에서 패키지 경로를 직접 실행하는 동작은 별도 확인이 필요합니다.

| 항목 | Windows 결과 |
|---|---|
| Codex 탐색 | PATH에서 패키지의 `codex.exe` 발견 |
| App Server 초기화 | 성공 |
| 로그인 감지 | ChatGPT 로그인 확인 |
| 단기 한도 | 응답에 없음 |
| 주간 한도 | 26% used, 74% remaining |
| 주간 reset | 2026-07-30 11:57:43 KST |
| account plan | `pro` |
| rate-limit plan | `prolite`, 별도 보존 |
| 격리된 미로그인 | `NotAuthenticated` 매핑 확인 |
| 프로세스 정리 | 앱 종료 후 새 Codex 자식 프로세스 없음 |
| 공식 Usage 화면 비교 | 인앱 브라우저 미로그인으로 현재 값 비교 미수행 |

Windows에서 동일한 App Server method와 DTO 매핑, Unix timestamp의 KST 변환, 미로그인 상태와 소유 자식 프로세스 정리를 확인했습니다. 공식 Usage 화면과의 현재 값 비교, Windows 11, Windows Terminal과 패키지 경로 직접 실행은 미검증입니다.

## 다음 단계 판단

App Server 기반 live provider의 기술적 실현 가능성, macOS 공식 Usage 화면 값 일치, macOS와 Windows의 실제 미로그인 상태 매핑을 확인했습니다. 단기 window가 양쪽 모두 없는 것은 현재 계정의 실제 상태로 확인됐고, account plan 불일치는 별도 보존 중입니다. 인증 만료는 안정적인 공개 식별 신호가 없어 추측 매핑을 보류했습니다. Windows는 live App Server 조회까지 확인했지만 Windows 11과 공식 화면의 현재 값 비교가 남아 있습니다.

## Windows native runtime evidence (2026-07-30)

The self-contained `0.1.1` publish was exercised on Windows 10 Home 22H2
(build 19045) with two real monitors:

- `DISPLAY1`: 2560×1440, 96 DPI / 100%
- `DISPLAY2`: 1440×2560, 96 DPI / 100%

The editing widget was restored to the primary monitor and the locked
click-through widget to the secondary monitor. Live HWND inspection confirmed
Topmost, ToolWindow, Layered, NoActivate, visibility, non-cloaked composition,
mode-correct click-through, 160×34 physical size, foreground preservation, and
clean exit in both cases. A CAPTUREBLT desktop-context image shows the locked
widget above Chrome on the secondary monitor. The probe locked its isolated
settings file to activate the recovery gate and verified that the user's HKCU
startup registration was identical before, during, and after each run.

The repeatable evidence is generated by `scripts/qa-windows-runtime.ps1` under
`artifacts/qa/windows-runtime`. These results do not cover Windows 11 or
mixed-DPI configurations.

A fresh official Usage comparison was attempted without reading cookies,
tokens, local storage, or network responses. The available in-app browser
session was signed out and redirected the Usage URL to the ChatGPT login page,
and no alternative signed-in browser connection was available. The current
rendered Usage value therefore remains unverified until the user signs in
through that browser session.
