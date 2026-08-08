---
date: 2026-08-01
title: mattpocock-skills에서 가져온 개념으로 프로젝트 스킬 갱신 2건 + 서버 TDD 스킬 신설
tags: [skills, task-writer, agent-log-writer, server-tdd]
---

# 프로젝트 스킬 갱신 (mattpocock-skills 각색)

## 목적 / 배경

- 사용자가 mattpocock-skills 플러그인(1.2.0) 설치 후 "프로젝트 스킬 중 갱신할 만한 것"을 요청.
- 클라이언트 스킬(`feature-design` 등)은 **제외** — 클라 담당 영역이라 합의 필요.
- 플러그인은 읽기 전용이므로 "덮어쓰기"가 아니라 **개념만 각색**해서 프로젝트 규약과 합쳤다.

## 변경 내용

- `.claude/skills/common/task-writer/SKILL.md` — **"큰 작업 쪼개기" 절 신설** (`to-tickets` 각색)
  - 수직 슬라이스(층 단위 분할 금지) · 선행 일감 = 진짜 게이트만 · 착수 가능선
  - 전면 개명·이설은 확장–이행–수축 순서 (실사례: 낚시 TID 이설)
  - 등록 전 분해안 검토 — 일감 번호 재사용 불가라 등록 후 재편이 비싸다
- `.claude/skills/common/agent-log-writer/SKILL.md` — **"압축 기준" 절 신설** (`handoff` 각색)
  - 기준 질문: "이 로그 없이 다음 작업자가 무엇을 다시 파야 하는가"
  - 다른 산출물(커밋·일감·기획)에 있는 내용은 경로 참조만 — 원본 이중화 금지
- `Server/.claude/skills/server-tdd/SKILL.md` — **신설** (`tdd`+`tests.md`+`mocking.md` 각색)
  - red-green 루프 + 이 저장소 규약(한글 테스트명·Shouldly·전역 Using·SmokeTest 제외) 병합
  - 기대값은 손 계산 리터럴 + 근거 주석 (동어반복 금지 — WorkSpeedTest를 실례로 인용)
  - 목은 시스템 경계(시각·난수·DB)에서만, 자기 모듈 목 금지
- `CLAUDE.md` — 서버 스킬 표가 "(없음)"이었는데 실제로는 `Server/.claude/skills/`에
  2개가 있었다. **실제 위치 기준으로 3개 등재** + 서버 테스트 절에 스킬 링크.

## 주요 결정 / 근거

- **도입 안 한 것과 이유** (재검토 방지):
  - `domain-modeling` — `CONTEXT.md`+ADR 체계 요구. **게임기획코어.md + Agent 로그가 이미 그 역할.** 진실이 두 곳이 된다
  - `triage`·`to-spec`·`wayfinder` — 일감 4상태·기획 문서 체계와 정면 충돌
  - `setup-pre-commit` — Husky/lint-staged 전제(JS용), .NET에 안 맞음
  - `grilling`·`diagnosing-bugs`·`research`·`prototype` — 범용이라 각색 없이 플러그인 버전 그대로 쓰면 됨
- 서버 TDD 스킬 위치는 `.claude/skills/server/`가 아니라 **`Server/.claude/skills/`** —
  기존 서버 스킬(packet-creator·sqlite-sql-creator)이 이미 거기 있고 디렉터리 스코프가 걸린다.

## 후속 작업 / 주의사항

- `feature-design`(클라)에 `codebase-design`의 deep module·deletion test를 합칠지는 **클라 담당 합의 후**.
- 플러그인 내장 `code-review`는 프로젝트 `/code-review`와 이름이 겹친다 —
  플러그인 것을 쓰려면 `mattpocock-skills:code-review`로 명시.
