# ADR 0002: Use Codex App Server for live usage

- Status: Accepted for PoC
- Date: 2026-07-21

## Context

현재 로그인 계정의 사용량을 토큰이나 인증 파일을 직접 읽지 않고 조회해야 합니다. 설치된 Codex CLI 0.136.0의 도움말, 생성 JSON Schema, 공식 문서와 실제 호출에서 App Server auth/rate-limit surface가 확인됐습니다.

## Decision

`codex app-server --stdio`의 JSONL 프로토콜을 사용합니다. `account/read`로 로그인 상태와 계정 plan을 읽고 `account/rateLimits/read`로 usage window를 읽습니다. 설치 버전에서 생성한 schema와 실제 응답을 구현 계약의 우선 근거로 사용합니다.

## Consequences

Codex가 관리하는 인증정보를 재사용하므로 애플리케이션이 토큰을 소유하지 않습니다. 반면 App Server 인터페이스와 응답은 Codex 버전에 따라 변할 수 있어 protocol DTO를 Core domain과 분리하고 response-format 오류를 별도 상태로 처리해야 합니다. Windows 동작과 공식 Usage 화면 일치는 추가 검증이 필요합니다.
