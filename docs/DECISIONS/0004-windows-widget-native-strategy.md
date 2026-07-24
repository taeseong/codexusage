# ADR 0004: Windows 위젯은 Avalonia 창과 격리된 Win32 보강 계층을 사용한다

- Status: Accepted for Windows foundation
- Date: 2026-07-23

## Context

Windows 위젯은 일반 애플리케이션 위에 유지되고 자동 갱신 중 포커스를 가져오지 않아야 하며, 사용자가 선택한 잠금 상태에서는 클릭을 아래 창으로 전달해야 합니다. Avalonia의 공통 창 속성만으로는 HWND의 실제 확장 스타일과 비활성 Topmost 재적용을 검증하기 어렵습니다.

## Decision

공통 사용량 UI와 ViewModel은 재사용하고 Windows 프로젝트가 HWND 제어를 소유합니다. 시작 화면은 macOS 메뉴 막대와 같은 짧은 사용률 요약과 동일한 `>_` 마크를 표시하는 `190×36` 위젯이며, 전체 사용량 카드는 별도의 상세 창에서 제공합니다. 작은 화면에는 Lock 버튼을 배치하지 않고 트레이 메뉴가 잠금과 복구를 모두 담당합니다. 일반 top-level 창의 최소 높이 보정을 피하기 위해 `WS_POPUP`을 적용하고, `WS_EX_LAYERED`, `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`를 함께 사용합니다. 잠금 상태에만 `WS_EX_TRANSPARENT`를 추가합니다. `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`는 창 DPI로 환산한 `190×36` 크기를 창 열기, 표시와 모드 변경 이벤트에서 적용합니다.

`WidgetInteractionState`를 편집/잠금 상태의 단일 출처로 사용하고 시스템 트레이가 같은 상태를 전환합니다. 트레이 생성 또는 네이티브 스타일 적용이 실패하면 잠금을 해제하고 편집 모드를 유지합니다.

작업표시줄과 다른 Topmost 창이 위젯보다 앞으로 재정렬되는 상황은 폴링하지 않습니다. `SetWinEventHook`으로 foreground 변경과 `Shell_TrayWnd`·`Shell_SecondaryTrayWnd`의 Z-order 재정렬을 관찰하고, 해당 이벤트에서만 `SWP_NOACTIVATE`로 기존 네이티브 상태를 다시 적용합니다. 연속 이벤트는 UI 스레드에 하나의 재적용 작업으로 합칩니다.

## Consequences

P/Invoke와 트레이 코드는 Windows 프로젝트 밖으로 노출되지 않습니다. 짧은 폴링 타이머가 없으므로 대기 CPU 부담을 추가하지 않습니다. 위치 영구 저장, 모니터 복구, 자동 시작, 알림과 패키징은 후속 ADR과 구현 범위로 남습니다.
