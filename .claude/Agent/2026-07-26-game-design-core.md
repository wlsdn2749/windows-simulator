---
date: 2026-07-26
title: GameDesignCore 도입 및 시스템별 상세 기획안 구축
tags: [design, docs, skills, claude-md]
---

# GameDesignCore 도입 및 시스템별 상세 기획안 구축

## 목적 / 배경

- 게임 기획이 `GameDesign/Design/Window Simulator GDD.md`(v0.1 draft) 한 장뿐이었고,
  Agent가 게임 시스템·콘텐츠를 개발할 때 참고할 고정 진입점이 없었다.
- 요구: ① 정리된 단일 문서 `GameDesignCore.md` 생성 ② 시스템별 폴더 + 상세 기획안
  ③ 상세 기획안 평가를 한 파일로 ④ Agent가 항상 Core를 참조하도록 스킬·규칙 추가.

## 변경 내용

- `GameDesign/GameDesignCore.md` — 신규. 단일 진입점.
  정체성 / 설계 원칙 P1~P4 / 코어 루프 / 시스템 지도 / 콘텐츠 지도 /
  **확정·미확정 현황표** / 개발 접점 규칙 / 문서 지도.
- `GameDesign/Design/<시스템>/README.md` — 신규 7종.
  `Gathering` · `Quest` · `Trait` · `Item` · `Trade` · `Progression` · `Presentation`.
  전부 `확정 사항 → 설계(제안) → 데이터 설계 → 서버/클라 책임 → 결정 필요` 구성으로 통일.
- `GameDesign/Design/DesignReview.md` — 신규. 설계 평가(좋은 점 / 우려 / 차별점 /
  리스크 등급표 R1~R10 / 권장 처리 순서).
- `.claude/skills/common/game-design-reference/SKILL.md` — 신규 스킬.
  게임 작업 착수 전 Core → 상세 기획안 순으로 읽는 절차, 미확정 항목 임의 결정 금지,
  기획 변경 시 문서 갱신 절차.
- `CLAUDE.md` — 상단에 "⚠️ 게임 작업 전 필독" 섹션 추가(항상 로드되는 위치),
  폴더 구조 표에 `GameDesignCore.md`·`GameDesign/Design/` 행 추가,
  협업 규칙 1항 추가, 공용 스킬 표에 `game-design-reference` 추가, 날짜 갱신.

## 주요 결정 / 근거

- **Core를 `GameDesign/` 루트에 둠** — `Design/` 안에 넣으면 원본 GDD·상세안과 같은 층이 되어
  "진입점"이라는 위상이 드러나지 않는다.
- **원본 GDD를 지우지 않고 유지** — Core는 GDD에서 도출한 요약이므로 상위 근거가 남아야 한다.
  기획자가 편집하는 문서와 Agent가 읽는 문서를 분리.
- **상세 기획안의 추정 내용에 전부 `⚠️ 제안` 표기** — GDD가 draft라 확정 사항이 적다.
  Agent가 제안을 확정으로 오인해 구현하는 것을 막기 위해 확정/제안을 문서 안에서 분리했다.
- **Core 5장에 "확정/미확정 현황표"를 둠** — 이 표가 이 문서 체계의 핵심.
  Agent가 임의로 밸런스를 정하지 않게 만드는 실질적 장치다.
- **강제 참조는 CLAUDE.md + 스킬 이중으로** — 스킬은 조건부 로드라 누락 가능성이 있어,
  항상 로드되는 CLAUDE.md 상단에 필독 섹션을 함께 넣었다.
- **폴더 내 파일명을 `README.md`로** — `.claude/Agent/README.md` 관례와 맞추고,
  나중에 같은 폴더에 밸런스표·서브 문서를 추가할 여지를 남겼다.

## 후속 작업 / 주의사항

- 기획 내용의 상당 부분이 **Agent 제안**이다. 기획자 검토 후 `⚠️` 제거 및 Core 5장 상태 갱신 필요.
- `DesignReview.md` 권장 처리 순서 1~2번(핵심 재미 정의 → 성장 곡선)이 끝나기 전에는
  밸런스 수치를 엑셀에 넣지 않는 편이 좋다. 순환 의존으로 재계산이 발생한다.
- 미해결로 남긴 실제 코드 이슈 2건:
  - `ItemTable.Desciption` 오타 (엑셀 헤더 수정 → 생성물 전체 재생성 필요)
  - `ItemType`이 "산업 출처"와 "용도 분류" 두 축을 겸함 → 실 데이터 입력 전 분리 검토
- 현재 `Item.xlsx` 데이터는 `A`~`F` 더미다. 실 데이터 교체 필요.
- 새 시스템 추가 시 갱신할 곳 3군데: 상세안 폴더, Core 3장 시스템 지도,
  `game-design-reference` 스킬의 문서 지도 표.

## 업데이트 (2026-07-27) — 1차 산업 5종 명시

### 변경 내용

- 1차 산업을 **농사·벌목·낚시·채굴·사냥 5종**으로 확정 명시. GDD의 "+α" 해소.
- `GameDesign/Design/Gathering/<산업>/README.md` 신규 5종
  (`Farming` · `Logging` · `Fishing` · `Mining` · `Hunting`).
- `Gathering/README.md` — 1-1장(산업 5종 표·역할 분담) 추가, 2.3 요일 로테이션을
  월~금 1:1 배정 기본안으로 구체화, Open Questions 갱신.
- `GameDesignCore.md` — 4장 콘텐츠 지도에 산업 5종·요일 구조·enum 현황 반영,
  5장 확정 현황 갱신, 7장 문서 지도에 산업 문서 행 추가.
- `DesignReview.md` — 업데이트 섹션 추가, 리스크 R11(산업당 주 1회) 신규.
- `game-design-reference` 스킬 문서 지도에 산업 문서 행 추가.

### 주요 결정 / 근거

- **산업 문서를 `Design/Gathering/` 하위에 배치** — 산업은 독립 시스템이 아니라
  채취 시스템의 콘텐츠다. Gathering README가 인덱스 역할을 한다.
- **산업마다 고유 축을 1개씩 배정** — 5개가 스킨만 다른 반복이 되지 않게 하기 위함.
  농사=파종 선택 / 벌목=고갈·재생 / 낚시=변동성 / 채굴=누적 깊이 / 사냥=성공·실패.
  경제 역할도 함께 갈라 두었다(기반 물량 / 총량 제어 / 티켓 / 성장 재료 / 고가 재화).
- **낚시와 사냥을 "고변동성"에서 서로 다른 방향으로 분리** — 잦은 소액 ↔ 드문 고액.
  둘 다 랜덤 산업이라 겹치기 쉬웠다.
- **요일 배정을 1일 1산업(평일 5일 = 산업 5종)으로 제안** — 수가 정확히 맞는다.
  다만 산업당 주 1회가 되는 부작용을 문서에 명시하고 미결로 남겼다.

### 후속 작업 / 주의사항 (추가)

- 🔴 **`ItemType.Hunting`이 enum에 없다.** `GameDesign/Excel/Enum.xlsx`에 **값 7로 뒤에 추가**
  후 `Server/generate-tables.ps1` 실행 필요. **중간 삽입 금지** —
  `Misc=5`/`Special=6`이 밀려 기존 `.bytes`·DB 값이 전부 어긋난다.
  이번 작업에서는 문서로 명시만 하고 엑셀은 건드리지 않았다.
- R11(산업당 주 1회로 성장 정체) — 비활성 산업을 저효율로 돌릴지 먼저 결정해야
  채취 수치를 잡을 수 있다.
- `ItemType.Mining` 주석이 "채광"인데 기획 용어는 "채굴"이다. 용어 통일 필요.

## 업데이트 (2026-07-27) — 기획 문서 구조 재편 (Core 이동 · GDD 삭제 · 폴더 한글화)

### 변경 내용

- `GameDesign/GameDesignCore.md` → `GameDesign/기획/GameDesignCore.md` 로 이동.
  Core가 기획 문서 트리의 최상위이자 유일한 진입점이 됐다.
- **`Window Simulator GDD.md` 삭제.** 내용은 Core에 전부 흡수돼 있었다.
- 기획 문서 폴더를 한글로 변경:
  `Design`→`기획`, `Gathering`→`자원채취`, `Quest`→`퀘스트`, `Trait`→`특성`,
  `Item`→`아이템`, `Trade`→`거래`, `Progression`→`진행성장`, `Presentation`→`데스크톱표현`,
  `Farming`→`농사`, `Logging`→`벌목`, `Fishing`→`낚시`, `Mining`→`채굴`, `Hunting`→`사냥`.
- 전 문서의 상대 링크·본문 경로 갱신. `CLAUDE.md`·`game-design-reference` 스킬도 함께 수정.
- GDD를 가리키던 본문 서술(“GDD 5장”, “GDD 공란” 등)을 Core 기준 표현으로 교체.

### 주요 결정 / 근거

- **`GameDesign/Excel`·`GameDesign/DataLog`는 영문 유지.**
  `Server/ExcelGenerator/Program.cs:12,21`이 이 경로를 문자열로 직접 참조한다.
  한글로 바꾸면 데이터 파이프라인이 깨진다.
- **GDD 삭제 전 스크래치패드에 사본 보관.** git 미추적 파일이라 삭제 시 복구 수단이 없었다.
  (사본은 세션 임시 폴더에 있으므로 영구 보관이 필요하면 별도 조치 필요)
- 링크 정합성은 스크립트로 전수 검증했다 (relative link resolve 체크, broken 0).

### 후속 작업 / 주의사항 (추가)

- 문서 폴더가 한글이라 git 로그에서 이스케이프되어 보일 수 있다.
  거슬리면 `git config core.quotepath false`.
- 새 시스템/산업 폴더도 한글로 만든다. 영문 폴더를 섞지 않는다.

### 이름 규칙 명문화 (같은 날 추가 요청)

한글 폴더/문서가 1회성 정리로 끝나지 않도록 규칙을 문서에 고정했다.

- `CLAUDE.md` — 협업 규칙에 한 줄 + **"이름 규칙 (문서·폴더)" 섹션** 신설.
  한글로 만들 대상과 **영문을 유지할 예외**(`GameDesign/Excel`·`DataLog`, 코드 폴더,
  `.claude/` 하위, `README.md` 등 관례 파일명)를 표로 분리.
- `game-design-reference` 스킬 — "새 시스템·산업을 추가할 때"에 한글 명명 의무 명시.
- `GameDesignCore.md` 6장 접점 표에 "문서 이름" 행 추가.

> 규칙을 3곳에 나눠 둔 이유: CLAUDE.md는 항상 로드되는 최종 근거,
> 스킬은 실제로 폴더를 만드는 시점에 읽히는 위치, 게임기획코어는 게임 작업 진입점이다.
> 예외 목록은 CLAUDE.md 한 곳에만 두고 나머지는 링크로 참조한다(중복 방지).

### 기획 문서 파일명 한글화 (같은 날 추가 요청)

- `GameDesignCore.md` → **`게임기획코어.md`**
- `DesignReview.md` → **`기획평가.md`**
- 본문에서 문서를 가리키던 영문 약칭 `Core` 도 전부 `게임기획코어`로 통일.
  (`Core를`·`Core의`처럼 조사가 붙은 형태는 `\bCore\b` 정규식에 안 잡혀 2차로 처리했다)
- `README.md`는 **바꾸지 않았다.** CLAUDE.md "이름 규칙"의 관례 예외이고,
  GitHub·에디터가 폴더 인덱스로 자동 렌더링하는 기능을 잃는다.
  각 폴더명이 이미 한글이라 `자원채취/README.md` 형태로 충분히 읽힌다.
- 링크 전수 재검증 완료 (broken 0).

### 시스템 명칭 변경 (같은 날 추가 요청)

- `데스크톱표현` → **`게임UI`** (표시명 "게임 UI")
- `진행성장` → **`진행및성장`** (표시명 "진행 및 성장")
- 폴더명/문서 제목/게임기획코어 3장 시스템 지도/스킬 문서 지도 모두 갱신.
  문서 제목의 영문 병기(`(Presentation)`·`(Progression)`)도 제거했다.

> **폴더명에는 공백을 넣지 않는다.** 요청은 "게임 UI"·"진행 및 성장"이었지만
> 공백이 들어가면 마크다운 링크가 `%20`으로 인코딩되어 가독성이 떨어진다.
> (삭제된 `Window Simulator GDD.md`가 정확히 그 문제를 갖고 있었다)
> 폴더는 붙여 쓰고(`게임UI`·`진행및성장`), 표시되는 텍스트만 띄어 쓴다.
