# ADR 0003: macOS는 격리된 AppKit 상태 항목과 Avalonia 상세 창을 사용한다

## 상태

Accepted, 2026-07-22. Preview-only TrayIcon decision superseded on the same date.

## 결정

macOS 앱은 공통 Avalonia 상세 창과 macOS 프로젝트에만 존재하는 AppKit 상태 항목·팝오버 어댑터를 사용한다. 상태 항목은 SF Symbol 템플릿 아이콘 옆에 동적 사용량을 표시한다. 클릭하면 `NSPopover`가 Minimal Split 시안의 사용/남음 2열, 초기화, 계정·갱신 상태와 상세 보기·새로고침·종료 동작을 제공한다. 앱 창을 닫아도 명시적 종료 전까지 메뉴 막대에서 동작한다.

## 근거

- Core 및 Codex 프로토콜 계층을 플랫폼 UI에서 분리한다.
- 실제 계정 연동과 오류 상태를 사용자가 즉시 확인할 수 있다.
- 종료 시 앱이 시작한 자식 App Server와 트레이 리소스를 함께 정리할 수 있다.
- 공개 Avalonia API에 없는 상태 항목 제목을 AppKit의 공식 `NSStatusItem.button.title`로 제공한다.
- 네이티브 호출은 UI 표현만 담당하며 인증 토큰, 원본 응답, 계정 식별자를 취급하지 않는다.

## 제약

어댑터는 Objective-C 런타임 메시지 전송을 사용하므로 macOS 프로젝트 밖에서 참조하지 않는다. AppKit 객체의 생성·갱신·해제는 Avalonia UI 스레드에서만 수행하고, 자동화 테스트는 네이티브 호출과 분리된 표시 모델을 검증한다.

프리뷰 번들은 ad-hoc 서명만 적용하며 배포용 Developer ID 서명과 공증을 포함하지 않는다.
