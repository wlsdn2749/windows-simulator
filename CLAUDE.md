# CLAUDE.md

> 최종 업데이트: 2026-08-06 (클라 구조 재편 · Arca 툴킷 위치 명시)

이 문서는 Claude Code로 작업할 때 공통으로 유의·협의해야 할 내용을 정리한 가이드다.
데스크톱 위에서 동작하는 투명 창(데스크톱 윈도우 제어)과 네트워크 기능을 결합하는 프로젝트로,
클라이언트와 서버를 한 저장소에서 영역을 나눠 협업한다.

서버는 저장소 루트의 독립 .NET 솔루션(`Server/`)으로 존재하며,
패킷 정의(`Server/MikaProtocol`)만 Unity(`Assets/Scripts_Server/Protocol`)로 단방향 미러링된다.

---

## ⚠️ 게임 작업 전 필독 — 게임기획코어.md

**게임 시스템·콘텐츠에 닿는 작업은 착수 전에
[`GameDesign/기획/게임기획코어.md`](GameDesign/기획/게임기획코어.md)를 반드시 먼저 읽는다.**

- 이 문서가 게임의 **단일 진입점**이다. 정체성·설계 원칙(P1~P4)·코어 루프·시스템 지도·
  **확정/미확정 현황**을 담는다.
- 게임기획코어를 읽은 뒤, 해당 영역의 **상세 기획안**(`GameDesign/기획/<시스템>/README.md`)을 이어서 읽는다.
- **게임기획코어 5장의 "미확정" 항목은 임의로 결정하지 않는다.** 필요하면 사용자에게 먼저 확인한다.
- 절차·예외·문서 갱신 규칙은 [`game-design-reference`](.claude/skills/common/game-design-reference/SKILL.md) 스킬 참조.

### 기획 문서를 고쳤으면 재귀적으로 전파한다

**기획 문서는 서로 촘촘히 물려 있다**(17개 문서 · 참조 113개).
한 문서만 고치고 끝내면 상위·형제 문서가 낡은 서술로 남는다 —
실제로 `Hunting` enum·아이템 종수·`ItemRarity` 개명이 이렇게 새어 나갔다(2026-08-02 확인).

1. 고친 문서 헤더의 **`> **바뀌면 갱신:**`** 블록에 적힌 문서를 **전부 열어 본다.**
2. 고친 문서가 생기면 **그 문서의 블록을 따라 또 퍼진다** (순환은 방문 표시로 멈춘다).
3. 전체 그래프·전파 규칙·엑셀/코드 대응표는
   [`GameDesign/기획/문서관계도.md`](GameDesign/기획/문서관계도.md)에 있다.
4. 커밋 전에 검사한다:

```powershell
powershell -File GameDesign/check-doc-graph.ps1 -Changed
```

> **엑셀만 앞서 나가면 "미구현"이 아니라 "오동작"이 된다.** 엑셀 구조를 바꿀 때는
> 그 시트를 읽는 서버 코드를 **같은 작업 단위로** 본다. 코드가 못 따라가면
> `일감/`에 등록하고 문서에 `❌ 미구현 (일감 T-0XX)`를 적는다.

대상 작업: 채취·퀘스트·특성·아이템·거래·성장곡선·위젯 UI 로직, `GameDesign/Excel` 데이터 변경,
게임 데이터 테이블/패킷/DB 스키마 설계, 기획 문서 수정.
(게임 규칙과 무관한 순수 인프라 작업은 제외)

---

## 환경

| 항목 | 내용 |
|------|------|
| Unity 버전 | 6000.3.10f1 |
| 렌더 파이프라인 | Built-in |
| 서버 런타임 | .NET 10 (WSGameServer) / MikaProtocol 멀티타깃 `net9.0`·`netstandard2.1` |
| 직렬화 | MemoryPack |
| 패킷 핸들러 생성 | Roslyn Source Generator (빌드 타임) |
| DB | SQLite (`Server/Shared/game.sqlite3`) |

---

## 폴더 구조

| 경로 | 담당 | 내용 |
|------|------|------|
| `일감/` | 공용 | **할 일의 단일 목록** — 일감 1개 = 파일 1개, `README.md`가 전체 현황 표. 담당(클라/서버/공용)·상태·우선순위·마감·관련 커밋 |
| `문서/` | 공용 | **개발 인프라 문서** — 서버·클라 양쪽에 걸치는 것. 현재 [`CI.md`](문서/CI.md) |
| `.github/workflows/` | 공용 | GitHub Actions — 서버 CI · 클라 CI · Unity 라이선스 활성화 |
| `Server/문서/` | 서버 | 서버 전용 문서 — [`테스트커버리지.md`](Server/문서/테스트커버리지.md) |
| `GameDesign/기획/게임기획코어.md` | 공용 | **게임 기획 최상위 문서** — 게임 작업 착수 전 필독. 정체성·설계 원칙·코어 루프·시스템 지도·확정/미확정 현황 |
| `GameDesign/기획/문서관계도.md` | 공용 | **기획 문서 의존 그래프** — 무엇을 함께 읽고 함께 고치는가. 전파 규칙 + 엑셀·코드 대응표 |
| `GameDesign/check-doc-graph.ps1` | 공용 | **문서 그래프 검사기.** 깨진 링크·헤더 블록 불일치·**갱신일 역전**(전파 누락)을 잡는다 |
| `GameDesign/기획/` | 공용 | **게임 기획 단일 진실** — 게임기획코어 + 시스템별 상세 기획안(`<시스템>/README.md`) + 1차 산업(`자원채취/<산업>/README.md`) + 설계 평가(`기획평가.md`) |
| `GameDesign/Excel/` | 공용 | **게임 데이터 단일 진실** — 기획 데이터 엑셀(`Enum.xlsx`·`Item.xlsx` …). 서버/클라 어느 쪽 폴더에도 속하지 않는 공용 입력 |
| `GameDesign/DataLog/` | 공용(생성) | 생성된 `.bytes`를 되읽어 덤프한 JSON. 엑셀 대조·diff 리뷰용 — **직접 수정 금지** |
| `GameDesign/generate-tables.ps1` | 공용 | **데이터 파이프라인 실행 스크립트.** 입력(엑셀) 옆에 두어 기획자가 그 자리에서 돌린다 |
| `Server/` | 서버 | **서버 단일 진실** — .NET 솔루션(MikaNetwork 모듈 + WSGameServer). 패킷 정의 원본 = `Server/MikaProtocol` |
| `Server/MikaNetwork.Lib/` | 서버 | **게임과 무관한 재사용 네트워크 프레임워크** — `MikaNetwork.Core`·`.Client`·`.Server`·`MikaUtils`·`MikaSourceGen` |
| `Server/GameData/` | 서버(생성) | 엑셀에서 생성된 테이블 정의(Row/Enum/GameTable/TableSet) — **직접 수정 금지** |
| `Server/Shared/Data/` | 서버(생성) | MemoryPack 바이너리 `*.bytes` |
| `Server/WSGameServer.Tests/` | 서버 | 서버 유닛 테스트 — **xUnit + Shouldly + Moq** |
| `Assets/Scripts_Server/Protocol/` | 서버(미러) | `Server/MikaProtocol`에서 자동 복사되는 사본 — **직접 수정 금지** |
| `Assets/Scripts_Server/GameData/` | 서버(미러) | `Server/GameData`에서 자동 복사되는 사본 — **직접 수정 금지** |
| `Assets/StreamingAssets/Data/` | 서버(미러) | `Server/Shared/Data`에서 복사되는 `*.bytes` |
| `Assets/Scripts_Server/` (`Network`·`Test`·`Utils`) | 서버 | Unity 측 네트워크/서버 연동 코드 |
| `Assets/Scripts_Client/` | 클라이언트 | 클라이언트 코드 |
| `Assets/Scripts_Client/Common/` | 클라이언트 | **Arca Unity Toolkit의 사본** — 게임을 모르는 범용 코드. 마스터는 `~/.claude/skills`(저장소)이고, 여기서 고쳤으면 `/unity-skill-sync`로 되돌린다. 특정 프로젝트 이름을 주석에 남기지 않는다 |
| `Assets/Scenes/` | 공용 | 씬 파일 |

> 패킷 정의는 `Server/MikaProtocol`에서 빌드되면 post-build로 `sync-protocol-to-unity.ps1`이
> 실행되어 `Assets/Scripts_Server/Protocol`로 단방향 미러링된다(소스 `MikaProtocol` → 대상 `Protocol`).

> **Roslyn 분석기(`MikaSourceGen`)도 빌드 시 `Assets/Plugins/Analyzers/`로 자동 복사된다.**
> Unity는 이 DLL을 `RoslynAnalyzer` 라벨로 로드해 핸들러 누락 경고(MIKA001)를 낸다.
> Unity 에디터가 켜져 있으면 파일이 잠겨 복사가 실패할 수 있다(빌드는 통과) — Unity를 닫고 다시 빌드한다.

**`MikaNetwork.Lib` 안팎의 경계는 "게임을 아는가"다.** 프레임워크는 게임 타입을 모른다 —
`MikaProtocol`(게임 패킷)·`GameData`(게임 테이블)를 Lib 안으로 넣지 않는다.
`MikaProtocol`·`GameData`·`ExcelGenerator`·`Shared`는 **미러링·파이프라인 경로가 위치에 묶여 있어**
옮기면 `ExcelGenerator/Program.cs`의 소스 상대경로가 조용히 어긋난다. 위치를 유지한다.

---

## 게임 데이터 파이프라인

엑셀 하나를 고치고 `GameDesign/generate-tables.ps1`을 돌리면 서버·Unity 양쪽 산출물이 한 번에 갱신된다.

```
GameDesign/Excel/*.xlsx            ← 사람이 편집하는 유일한 원본
        │  [1/2] ExcelGenerator (코드 생성 + 런타임 인메모리 컴파일로 .bytes까지)
        ├─ 정의(.cs)   → Server/GameData/        ─[2/2]→ Assets/Scripts_Server/GameData/
        ├─ 데이터(.bytes) → Server/Shared/Data/   ─[2/2]→ Assets/StreamingAssets/Data/
        └─ 리뷰(.json) → GameDesign/DataLog/
```

- 서버는 `GameData` 프로젝트를 참조하고 `.bytes`를 Content로 bin에 복사받는다.
- Unity는 미러된 `.cs` + StreamingAssets의 `.bytes`를 읽는다. 양쪽 MemoryPack 와이어 포맷이 동일하다.
- 엑셀을 Excel에서 열어 둔 채로 실행하면 파일 잠금으로 즉시 실패한다. 닫고 다시 실행한다.
- **경로는 전부 저장소 루트에서 유도한다.** 어디에 체크아웃하든 동작하도록 절대경로를 박지 않는다.
  ps1은 `$RepoRoot`/`$ServerRoot`/`$UnityRoot`에서, `ExcelGenerator`는 `Program.cs` 상단의
  루트 상대 상수(`ExcelDirRel` 등)에서 조합한다. **프로젝트 폴더 상대(`../GameData`)로 두지 않는다** —
  프로젝트를 옮기면 컴파일은 통과하면서 엉뚱한 위치에 파일을 쓴다.
- **시트를 지우면 그 테이블의 `.bytes`·`.json`도 자동 삭제된다**(Unity 미러까지 전파).
  생성물을 손으로 지울 필요가 없다.

### 엑셀 마커 행 (A열)

| 마커 | 의미 |
|------|------|
| `Type` | `int`·`long`·`float`·`string`·`bool`·`ID`·`eEnum` (+ `[]` 배열은 `,` 구분) |
| `Min`·`Max` | 값 범위 |
| `Default(Null)` | 빈 셀일 때 쓸 값. **비워 두면 "빈 셀 = 오류"**(fail-fast) |
| `Ref` | `대상시트.대상컬럼` — 값이 실재하는지 검사. `?`를 붙이면 빈 셀·`0` 허용 |

> `Default(Null)`에 **`""`(따옴표 두 개)** 를 적으면 빈 문자열이 기본값이 된다.
> 설명·비고처럼 비워 두는 게 정상인 string 컬럼에 쓴다. 이 표기가 없으면 빈 셀은 오류다.

### 예약 컬럼 — `Description`

**`Description` 컬럼은 게임 로직에 아무 영향을 주지 않는다.** 기획자가 시트에 남기는 메모다.

- 값을 바꿔도, 통째로 비워도 **동작이 달라지지 않는다.** 서버·클라 어느 쪽도 읽지 않는다.
- 그래서 `Default(Null)`에 `""`를 지정해 **빈 셀을 정상으로 둔다.**
- 생성된 Row 클래스에 `[기획 메모 — 로직에서 읽지 않는다]` 주석이 자동으로 붙는다.

> ⚠️ **로직에 쓰는 수치를 `Description`에 적지 않는다.** 확률·배수 같은 값을 메모로 적어 두면
> 실제 컬럼(`Weight` 등)과 따로 놀다가 조용히 어긋난다. 계산에 쓸 값은 반드시 자기 컬럼을 갖는다.

---

## 서버 테스트

`Server/WSGameServer.Tests`(솔루션 포함). **xUnit** + **Shouldly**(단언) + **Moq**(목).
세 네임스페이스는 csproj의 `<Using>`으로 전역 등록돼 있어 테스트 파일에 `using`을 적지 않는다.

```powershell
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
```

- 테스트 이름은 한글로 **동작을 서술**한다 (예: `만료된_티켓은_소모되지_않는다`).
- `SmokeTest.cs`는 프레임워크 연결 확인용이다. 실제 테스트는 새 파일로 나눈다.
- 작성 규칙·red-green 절차는 [`server-tdd`](Server/.claude/skills/server-tdd/SKILL.md) 스킬 참조.
- **실행 중인 `WSGameServer.exe`가 있으면 DLL 잠금(MSB3021)으로 빌드가 실패한다.** 종료하고 돌린다.
  (Unity 에디터는 분석기 DLL 복사만 막으므로 테스트에는 영향이 없다)

### 커버리지

```powershell
powershell -File Server/run-coverage.ps1          # 낮은 순 25개 + 전체 수치
powershell -File Server/run-coverage.ps1 -Top 0   # 전부
powershell -File Server/run-coverage.ps1 -Html    # HTML 리포트(reportgenerator 필요)
```

**필터 없이 `--collect`만 쓰면 숫자가 쓸모없다.** MemoryPack 생성물(`*.g.cs`)이 전체 라인의
절반을 넘어 손으로 쓴 코드가 그 안에 묻힌다(필터 전 17.5% → 후 45.4%).
제외 규칙은 `Server/coverlet.runsettings`에 있고, 측정 대상은 **`WSGameServer`뿐**이다.

⚠️ **커버리지를 목표로 삼지 않는다.** 단언 없는 테스트로도 숫자는 올라간다 —
빨강만 믿을 만하고 초록은 못 믿는다. 상세는
[`Server/문서/테스트커버리지.md`](Server/문서/테스트커버리지.md) 참조.

---

## 협업 규칙

- 각자 자기 담당 폴더(`Scripts_Client` / `Scripts_Server`·`Server`)만 수정한다. 상대 폴더 변경은 합의 후.
- 패킷 정의는 `Server/MikaProtocol`에서만 수정한다. `Assets/Scripts_Server/Protocol`은
  `sync-protocol-to-unity.ps1`이 덮어쓰는 사본이므로 직접 수정하지 않는다.
- 게임 시스템·콘텐츠 작업은 `GameDesign/기획/게임기획코어.md` → 해당 상세 기획안 순으로 먼저 읽는다.
  기획이 확정·변경되면 상세 기획안과 게임기획코어의 확정/미확정 현황을 함께 갱신하고,
  **`문서관계도.md`의 역참조를 따라 재귀적으로 전파한 뒤 `check-doc-graph.ps1 -Changed`로 검사한다.**
- 게임 데이터는 `GameDesign/Excel`의 엑셀에서만 수정한다. 여긴 **공용**이라 서버·클라 모두 편집해도 된다.
  생성물(`Server/GameData`, `Server/Shared/Data`, `GameDesign/DataLog`, Unity 미러)은 직접 고치지 않는다.
- 엑셀을 수정했으면 `GameDesign/generate-tables.ps1`을 돌려 **엑셀과 생성물을 같은 커밋에** 담는다.
  `GameDesign/DataLog/*.json`의 diff가 데이터 변경 내역 리뷰 수단이므로 함께 커밋한다.
- **서버 로그는 `Console.WriteLine`이 아니라 `ServerLog`(`Server/WSGameServer/Common/ServerLog.cs`)를 쓴다.**
  시각·레벨·스레드·분류가 함께 남아야 로그를 읽을 수 있다.
  `MikaNetwork.Lib`은 로그 정책을 갖지 않는다 — 훅(`MikaPacketManager.Dispatching`,
  `MikaSessionPacketExtensions.Sent`, `MikaServer.Connected`)만 뚫고 호스트가 채운다.
- 커밋은 `commit-convention` 규칙을 따른다.
- Unity에서 새 스크립트·에셋을 만들면 에디터를 갱신해 `.meta`를 생성한 뒤 원본과 함께 커밋한다.
  `.meta` 누락 시 GUID·참조 충돌이 발생할 수 있다.
- `.claude/settings.local.json`은 개인 설정이라 커밋하지 않는다(`.gitignore` 처리됨).
- 코드는 한글 주석을 사용한다.
- **문서(`.md`)와 문서 폴더는 항상 한글 이름으로 만든다.** 상세는 아래 "이름 규칙" 참조.
- CLAUDE.md·스킬 문서를 수정하면 문서 상단의 `최종 업데이트` 날짜를 그날 날짜로 갱신한다.
- **사용자가 특별한 요구를 하지 않는 한, 간단하고 명료하게 설명한다.**
  물은 것에 답하고 끝낸다. 배경·대안·파생 논점을 묻지 않았는데 늘어놓지 않는다.
  중요한 위험이나 결정 사항이 있으면 **한두 줄로 짚고** 넘어간다.

---

## 이름 규칙 (문서·폴더)

**새 `.md` 문서나 폴더를 만들 때는 한글 이름을 쓴다.** 영문으로 만들지 않는다.

| 대상 | 규칙 | 예 |
|------|------|-----|
| 기획·설계 문서 폴더 | **한글** | `GameDesign/기획/자원채취/농사/` |
| 기획·설계 `.md` 파일 | **한글** | `요일로테이션.md`, `밸런스표.md` |
| 문서 안의 링크·경로 | 실제 한글 경로 그대로 | `[자원채취](자원채취/README.md)` 형태 |

### 예외 — 영문을 유지하는 것

이름을 바꾸면 **동작이 깨지거나 관례를 벗어나는** 대상은 영문 그대로 둔다.

| 대상 | 이유 |
|------|------|
| `GameDesign/Excel/` · `GameDesign/DataLog/` | `Server/ExcelGenerator/Program.cs`가 경로를 문자열로 직접 참조 |
| 모든 코드 폴더·소스 파일 (`Assets/`, `Server/`, `.cs` 등) | 빌드·네임스페이스·Unity 규약 |
| `.claude/` 하위 (스킬명·`SKILL.md`·`Agent` 로그) | 스킬 이름은 kebab-case 영문, 로그 파일명은 `YYYY-MM-DD-<kebab-slug>.md` |
| 관례적 파일명 (`README.md`, `CLAUDE.md`) | 표준 관례 |
| 이미 영문으로 자리 잡은 기존 문서 | 임의로 바꾸지 않는다. 바꿀 땐 참조 경로를 전부 함께 고친다 |

> 폴더명을 바꾸면 **참조하는 모든 문서의 상대 링크가 깨진다.**
> 이름을 변경했으면 링크를 전수 확인하고, `CLAUDE.md`와 관련 스킬 문서의 경로도 함께 고친다.

---

## Skills 참조

스킬은 항상 적용하는 게 아니라, **작업 내용에 따라 필요한 경우에만** 참고한다.
작업하는 폴더에 맞춰 해당 그룹과 **공용** 스킬을 함께 본다.
(클라이언트 작업 = 공용 + 클라이언트 / 서버 작업 = 공용 + 서버)

### 공용 (`common/`) — 모든 작업

| 스킬 | 경로 | 내용 |
|------|------|------|
| `game-design-reference` | [`.claude/skills/common/game-design-reference/SKILL.md`](.claude/skills/common/game-design-reference/SKILL.md) | 게임 작업 **착수 전** `게임기획코어.md` + 상세 기획안 필독 |
| `excel-table-creator` | [`.claude/skills/common/excel-table-creator/SKILL.md`](.claude/skills/common/excel-table-creator/SKILL.md) | 게임 데이터 엑셀 시트·컬럼 작성 규칙 (TID 필수·마커 행·`Ref`) |
| `commit-convention` | [`.claude/skills/common/commit-convention/SKILL.md`](.claude/skills/common/commit-convention/SKILL.md) | Git 커밋 메시지 규칙 |
| `agent-log-reader` | [`.claude/skills/common/agent-log-reader/SKILL.md`](.claude/skills/common/agent-log-reader/SKILL.md) | 코드 작업 **착수 전** `.claude/Agent/` 로그 필독 |
| `agent-log-writer` | [`.claude/skills/common/agent-log-writer/SKILL.md`](.claude/skills/common/agent-log-writer/SKILL.md) | 코드 작업 **종료 후** `.claude/Agent/`에 로그 기록 |
| `task-reader` | [`.claude/skills/common/task-reader/SKILL.md`](.claude/skills/common/task-reader/SKILL.md) | `일감/`에서 현재 할 일·상태 확인 |
| `task-writer` | [`.claude/skills/common/task-writer/SKILL.md`](.claude/skills/common/task-writer/SKILL.md) | `일감/`에 일감 등록·상태 갱신 |

### 클라이언트 (`client/`) — `Assets/Scripts_Client` 작업 시

| 스킬 | 경로 | 내용 |
|------|------|------|
| `clean-code-style` | [`.claude/skills/client/clean-code-style/SKILL.md`](.claude/skills/client/clean-code-style/SKILL.md) | Unity/C# 클린 코드 스타일 규칙 |
| `feature-design` | [`.claude/skills/client/feature-design/SKILL.md`](.claude/skills/client/feature-design/SKILL.md) | OOP·SOLID·디자인 패턴 기반 기능 설계 |
| `optimization` | [`.claude/skills/client/optimization/SKILL.md`](.claude/skills/client/optimization/SKILL.md) | 성능 최적화 판단 및 적용 가이드 |
| `unity-handoff` | [`.claude/skills/client/unity-handoff/SKILL.md`](.claude/skills/client/unity-handoff/SKILL.md) | 유니티 에디터 작업 핸드오프 프롬프트 생성 |

### 서버 (`Server/.claude/skills/`) — `Server/` · `Assets/Scripts_Server` 작업 시

서버 스킬은 `.claude/skills/server/`가 아니라 **`Server/.claude/skills/`** 에 있다
(디렉터리 스코프 — `Server/` 아래 파일을 다룰 때 적용된다).

| 스킬 | 경로 | 내용 |
|------|------|------|
| `packet-creator` | [`Server/.claude/skills/packet-creator/SKILL.md`](Server/.claude/skills/packet-creator/SKILL.md) | MikaProtocol 패킷 추가 절차 (PacketId·MemoryPackable·핸들러) |
| `sqlite-sql-creator` | [`Server/.claude/skills/sqlite-sql-creator/SKILL.md`](Server/.claude/skills/sqlite-sql-creator/SKILL.md) | SQLite DDL·쿼리 규칙 (STRICT·주석 필수·인덱스 근거) |
| `server-tdd` | [`Server/.claude/skills/server-tdd/SKILL.md`](Server/.claude/skills/server-tdd/SKILL.md) | 서버 테스트 작성 규칙 + red-green 루프 (한글 테스트명·기대값 리터럴·목은 경계에서만) |
| `server-code-style` | [`Server/.claude/skills/server-code-style/SKILL.md`](Server/.claude/skills/server-code-style/SKILL.md) | 서버 코드 작성 스타일 (주석은 꼭 필요한 곳에만·한글 주석) |
