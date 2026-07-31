# Security

## Local usage history

`usage-history.json` contains only weekly limit IDs, locally generated window IDs, observed percentages, reset timing, and plan labels. It does not contain tokens, email addresses, account IDs, prompts, or Codex conversation content. History remains local and is never sent to an external service.

## Last-known-good usage cache

`usage-cache.json` contains only a format version, retrieval time, normalized plan labels, the short-term or weekly limit kind, observed percentage, window duration, and reset time. Server-supplied limit IDs and display names are replaced with local canonical values. Plan labels that contain anything other than ASCII letters, digits, hyphens, or underscores are discarded.

The cache is local, bounded to two display limits, and is used for at most 24 hours. A limit whose reset time already passed is not restored. Cached values are also hidden when Codex reports that the user is signed out or authentication expired, preventing data from a previous login session from being presented as current. Cache load or save failure never changes a successful live lookup into an error.

History writes are flushed to a temporary file before replacement. An interrupted complete temporary file is recovered locally; malformed files are preserved with a `.corrupt-*` suffix without uploading them.

Windows preferences use the same flushed temporary-file and atomic-replacement policy. Malformed `settings.json` files are preserved for local diagnosis and replaced in memory with safe defaults. The settings payload contains UI preferences, window state, and notification deduplication metadata only; it does not contain Codex credentials or account identifiers.

Copy diagnostics is an explicit local user action. It runs only the discovered Codex executable with `--version`; it does not inspect authentication files. Known user paths are replaced with `%APPDATA%`, `%LOCALAPPDATA%`, or `%USERPROFILE%`, and unknown custom directories are reduced to the executable file name. The copied text excludes usage percentages, plan names, email addresses, account identifiers, tokens, prompts, and conversations.

Release packages exclude PDB symbol files and publish a SHA-256 checksum. Public installers remain unsigned until a code-signing certificate is configured, so Windows may show a SmartScreen warning.

## 인증정보 취급

Codex Usage는 기존 Codex CLI가 관리하는 로그인 컨텍스트를 App Server를 통해 사용합니다. 인증 파일이나 macOS Keychain을 직접 읽지 않으며 access token, refresh token, 브라우저 쿠키 또는 비밀번호를 저장하지 않습니다.

`account/read` 응답에 이메일이 포함될 수 있지만 프로토콜 DTO는 이메일 필드를 선언하지 않아 역직렬화 과정에서 버립니다. PoC는 인증 유형과 plan만 사용합니다.

인증 만료를 재현하기 위해 실제 로그인 토큰을 폐기하거나, experimental `chatgptAuthTokens` 경로에 가짜 토큰을 주입하지 않습니다. 현재 공개 프로토콜에는 관리형 ChatGPT 인증 만료를 구분하는 안정적인 typed 신호가 없으므로 오류 메시지 문자열로 만료 여부를 추측하지 않습니다.

## 로그와 출력

현재 PoC는 애플리케이션 로그 파일을 만들지 않습니다. App Server stderr는 deadlock 방지를 위해 비동기로 소비하지만 화면이나 파일에 기록하지 않습니다. `SensitiveDataRedactor`는 향후 진단 문자열에 대비해 Authorization 값, 대표 토큰 필드, 전체 이메일 주소를 제거하거나 마스킹합니다.

JSON-RPC 오류는 서버 메시지 전체 대신 숫자 오류 코드만 애플리케이션 예외에 포함합니다. 예외 원문은 사용자 화면에 그대로 표시하지 않습니다.

macOS UI는 provider의 typed 상태를 고정된 사용자 문구로 변환합니다. App Server의 원문 오류나 계정 이메일은 화면, 메뉴 막대, 툴팁에 넣지 않습니다. 마지막 정상 사용량은 현재 프로세스 메모리에만 유지하며 아직 디스크에 기록하지 않습니다.

Windows 위젯과 트레이도 같은 typed 상태와 고정 문구만 사용합니다. Win32 계층은 HWND와 창 스타일만 취급하며 provider 응답, 계정 정보 또는 인증정보를 받지 않습니다. 트레이 툴팁은 사용률 요약만 포함하고 이메일이나 원문 App Server 오류를 표시하지 않습니다.

오류 카드의 복구 버튼은 typed 상태만 사용합니다. 설치 안내는 공식 CLI 설치 명령을 표시하고, 로그인 확인과 일반 재시도는 기존 App Server provider를 다시 호출할 뿐 토큰 파일을 열거나 로그인 자격 증명을 수집하지 않습니다.

손상된 `settings.json`은 `.corrupt-*` 파일로 로컬에 보존하고 안전한 기본값을 사용합니다. 파일을 읽지 못했거나 손상 파일 보존에 실패한 경우에는 명시적인 Settings Save 전까지 자동 쓰기를 중지해 기존 파일 덮어쓰기를 방지합니다. 이 복구 게이트가 활성화된 동안에는 기존 Windows 시작 프로그램 등록을 읽어 화면에만 반영하고 추가·수정·삭제하지 않습니다. 설정 화면의 기본값 복원은 자동 시작과 알림 설정만 저장 전 상태로 변경하며 사용량 이력, 캐시, 창 위치 또는 Codex 인증정보를 삭제하거나 읽지 않습니다.

Codex CLI 미설치 안내는 고정된 공식 PowerShell standalone 명령과 npm 대안 명령을 화면에 표시하고 사용자가 요청할 때 클립보드에 복사할 뿐입니다. 앱이 설치 명령을 자동 실행하거나 관리자 권한을 요청하지 않으며, 설치 이후에도 기존 Codex 로그인 컨텍스트만 사용합니다. History CSV export is also explicit: it writes only the locally observed fields selected by the user to a file chosen in the Save dialog.

The About-window update check is explicit rather than automatic. It makes a public, unauthenticated request to GitHub only when the user presses `Check for updates`; it never attaches Codex usage, account, token, or diagnostics data.

## 프로세스 보안

- 실행 인자는 고정된 `app-server --stdio` 또는 명시적 진단의 `--version`이며 민감정보를 명령행에 넣지 않습니다.
- 네이티브 실행 파일은 직접 시작합니다. Windows npm의 `.cmd` shim만 `cmd.exe /d /c`와 `ProcessStartInfo.ArgumentList`로 실행하며 PowerShell과 사용자 입력 문자열은 사용하지 않습니다.
- stdin/stdout/stderr를 비동기로 처리합니다.
- 요청별 timeout과 cancellation을 지원합니다.
- 종료 시 Codex Usage가 직접 시작한 App Server만 정리합니다.
- 이미 실행 중인 Codex CLI나 데스크톱 앱 프로세스는 종료하지 않습니다.
- Finder 실행 시 확인하는 공식 Codex 앱 경로는 실행 파일 존재 여부만 검사하며 앱 번들의 인증 파일이나 다른 콘텐츠를 읽지 않습니다.
- Windows P/Invoke는 `user32.dll`의 창 스타일과 Z-order 제어로 제한되며 브라우저, 쿠키 저장소 또는 다른 프로세스 메모리를 읽지 않습니다.
- Topmost 유지를 위한 WinEvent hook은 foreground·Z-order 이벤트와 작업표시줄 창 클래스명만 확인하며 창 제목, 입력 내용 또는 다른 프로세스 메모리를 읽지 않습니다.
- 트레이 복구가 없으면 클릭 통과를 활성화하지 않으며, 트레이 생성 실패 시 위젯을 편집 상태로 유지합니다.

## 외부 통신

Codex Usage 자체의 서버, analytics SDK 또는 telemetry 전송은 없습니다. 사용량 조회 과정에서 Codex App Server가 기존 로그인 컨텍스트로 OpenAI 서비스와 통신할 수 있습니다. 브라우저 스크래핑이나 비공식 원격 API 직접 호출은 사용하지 않습니다.
