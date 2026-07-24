# Codex Usage

## Windows distribution

Create a self-contained Windows x64 installer with `scripts\package-windows.ps1`. See [Windows distribution](docs/WINDOWS_DISTRIBUTION.md) for prerequisites, output paths, and the release checklist.

Codex Usage는 현재 컴퓨터에 로그인된 Codex 계정의 사용 한도를 안전하게 조회해 데스크톱에 표시하기 위한 크로스플랫폼 유틸리티입니다.

## 현재 단계

Phase 1 실데이터 PoC, macOS Apple Silicon 프리뷰 앱, Windows x64 기반 위젯이 구현되어 있습니다. 두 앱은 Codex App Server의 로그인 상태와 ChatGPT rate-limit window를 읽어 표시하며 60초마다 갱신합니다. Windows는 macOS 메뉴 막대처럼 사용률을 요약한 작은 Topmost 위젯과 별도의 상세 창을 제공하며, 포커스 미탈취, 편집/클릭 통과 전환과 시스템 트레이 복구 경로를 지원합니다. 마지막 위젯 위치 저장과 화면 밖 위치 복구를 지원합니다. 다중 모니터별 배율 복원, 설정 저장, 디스크 캐시, 로그인 시 자동 실행과 알림은 아직 구현하지 않았습니다.

## 요구 사항

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- 로그인된 Codex CLI 또는 `/Applications/Codex.app`

.NET 10은 2028년 11월까지 지원되는 현재 LTS이므로 선택했습니다. `global.json`은 검증에 사용한 SDK 10.0.302를 기준으로 최신 10.0 patch roll-forward를 허용합니다.

`artifacts/osx-arm64/CodexUsage.app`은 .NET 런타임을 포함하므로 실행할 Mac에는 .NET SDK가 필요하지 않습니다.

## macOS 프리뷰 실행

빌드된 앱을 Finder에서 더블클릭하거나 다음 명령으로 실행합니다.

```bash
open artifacts/osx-arm64/CodexUsage.app
```

앱은 Dock을 차지하지 않는 메뉴 막대 전용 모드로 시작하며, 같은 사용자 세션에서는 한 인스턴스만 실행됩니다. 상태 항목의 SF Symbol 아이콘 옆에는 `5H 64%`처럼 우선 한도의 실제 사용률이 표시됩니다. 클릭하면 시안의 Minimal Split 구조를 따르는 네이티브 팝오버에서 사용·잔여율, 초기화 시간, 플랜과 마지막 갱신 상태를 확인하고 상세 창 열기, 즉시 새로고침, 종료를 선택할 수 있습니다.

로컬에서 번들을 다시 만들려면 다음을 실행합니다.

```bash
DOTNET_COMMAND=dotnet scripts/package-macos.sh osx-arm64
```

현재 번들은 로컬 확인용 ad-hoc 서명이며 Developer ID 서명과 공증은 아직 적용하지 않았습니다.

## Windows 기반 위젯 실행

Windows x64 개발 빌드는 다음 명령으로 실행합니다.

```powershell
dotnet run --project src/CodexUsage.Windows/CodexUsage.Windows.csproj --configuration Debug
```

시작 시 `5H 64% · W 37%`처럼 요약된 `160×34` 위젯만 표시됩니다. macOS와 같은 `>_` 터미널 마크를 사용하며 Windows 작업표시줄 높이에 맞춘 크기입니다. 위젯은 작업표시줄과 Alt+Tab에 나타나지 않는 테두리 없는 Topmost 창입니다. 작업표시줄이나 다른 Topmost 창이 앞으로 재정렬되면 foreground와 작업표시줄 Z-order 이벤트에서 포커스를 가져오지 않고 위젯의 Topmost를 다시 적용합니다. 마지막 위치는 다음 실행 시 복원하며, 저장된 위치가 화면 밖이면 현재 작업 영역 안으로 옮깁니다. 더블클릭하거나 트레이의 `Open details`를 선택하면 단기·주간 한도, reset, 갱신 및 오류 상태를 포함한 상세 창이 열립니다.

최초 실행은 위젯 전체를 드래그할 수 있는 편집 모드입니다. 시스템 트레이의 `Lock widget (click-through)`으로 잠그고 `Enter edit mode`로 반드시 복귀할 수 있습니다. 트레이 메뉴는 위젯 표시/숨기기, 편집/잠금 전환, 새로고침, 상세 보기와 종료를 제공합니다.

## 빌드와 테스트

```bash
dotnet restore CodexUsage.sln
dotnet build CodexUsage.sln --configuration Debug
dotnet build CodexUsage.sln --configuration Release
dotnet test CodexUsage.sln --configuration Release
```

일부 제한된 샌드박스에서는 MSBuild 공유 컴파일러나 테스트 호스트의 로컬 소켓이 차단될 수 있습니다. 그런 환경에서는 공유 빌드 서버를 끈 뒤 실행합니다.

```bash
dotnet build CodexUsage.sln --configuration Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

## PoC 실행

```bash
dotnet run --project src/CodexUsage.Poc/CodexUsage.Poc.csproj --configuration Release
```

PoC는 App Server가 반환한 window만 출력합니다. 없는 단기 또는 주간 window를 `0%`로 대체하지 않고 `Unavailable in response`로 표시합니다.

## 지원 상태

- macOS 26.5 ARM64와 Codex CLI 0.136.0에서 로그인 및 주간 사용량 실조회 성공
- 제한된 Finder형 `PATH`에서도 `/Applications/Codex.app/Contents/Resources/codex` 탐색 및 실조회 성공
- 자체 포함 `CodexUsage.app` 실행, 동적 사용률 상태 항목, Minimal Split 네이티브 팝오버, 상세 창, 수동/60초 갱신 확인
- 최신 Minimal Split 단일 인스턴스 번들의 팝오버 종료와 종료 후 앱·소유 자식 프로세스 정리, 재실행을 실제 macOS GUI에서 확인
- Windows 10 Home 22H2 x64에서 Codex CLI 0.145.0-alpha.30의 로그인·주간 사용량 실조회와 시간 변환 확인
- Windows 간략 위젯과 상세 창의 live·미로그인·잠금 화면 렌더, HWND Topmost/Layered/ToolWindow/NoActivate/Transparent 스타일과 종료 후 자식 프로세스 정리 확인
- 제어된 빈 Chrome과 빈 VS Code 창을 foreground로 전환한 뒤에도 Topmost 스타일 유지 확인
- 격리된 빈 `CODEX_HOME`에서 실제 미로그인 상태와 `NotAuthenticated` 매핑 확인
- 관리형 ChatGPT 인증 만료는 안정적인 공개 식별 신호가 없어 live 매핑을 추측하지 않음
- 현재 계정 응답에는 주간 window만 존재하고 단기 window는 없음
- 2026-07-22 공식 Codex Usage 화면과 사용률·초기화 시각 비교 일치
- Windows 11, Windows Terminal, 실제 트레이 메뉴를 통한 잠금 해제, 다중 모니터와 125% 이상 DPI는 아직 미검증
- 현재 Windows 검증 샌드박스에서는 WindowsApps의 Codex 실행 권한이 없어 설치 파일의 동일한 복사본을 임시 경로에서 실행했으며, 일반 사용자 세션의 패키지 경로 직접 실행은 추가 확인 필요
- macOS 로그인 시 자동 실행, 알림, 절전 복구, Intel Mac은 아직 미검증

## 보안 원칙

- Codex 인증 파일, 브라우저 쿠키, 토큰을 직접 읽거나 저장하지 않습니다.
- experimental 외부 토큰 주입 경로로 인증 만료를 모의하지 않습니다.
- App Server stderr는 비동기로 소비하되 출력하거나 저장하지 않습니다.
- 계정 이메일은 역직렬화 모델에 포함하지 않으며 콘솔에 출력하지 않습니다.
- 외부 자체 서버로 데이터를 전송하지 않습니다. Codex App Server가 기존 로그인 컨텍스트로 수행하는 OpenAI 통신만 사용합니다.

자세한 내용은 [보안 문서](docs/SECURITY.md)와 [PoC 검증 기록](docs/CODEX_USAGE_POC.md)을 참고하십시오.
