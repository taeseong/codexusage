# ADR 0001: Use .NET 10 LTS

- Status: Accepted
- Date: 2026-07-21

## Context

프로젝트 초기화 시점에 .NET 8과 .NET 9의 지원 종료가 2026년 11월로 가까우며 .NET 10은 현재 LTS입니다.

## Decision

모든 Phase 0/1 프로젝트는 `net10.0`을 대상으로 하고 SDK 10.0.302를 검증 기준으로 사용합니다. `global.json`은 같은 major/minor의 최신 patch roll-forward를 허용합니다.

## Consequences

장기 지원 기간과 C# 14를 사용할 수 있습니다. 개발 및 배포 환경에는 .NET 10 SDK/runtime이 필요하며 향후 Avalonia 버전 호환성은 UI Phase 시작 전에 별도 확인합니다.

