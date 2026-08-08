---
date: 2026-07-29
title: 데이터 파이프라인 스크립트를 GameDesign으로 이전하고 서버 테스트 프로젝트 신설
tags: [server, pipeline, powershell, test, xunit]
---

# 데이터 파이프라인 스크립트를 GameDesign으로 이전하고 서버 테스트 프로젝트 신설

## 목적 / 배경

- 사용자 요청 두 가지: **① 테이블 생성 ps1을 `GameDesign`으로 옮기고 경로를 고정할 것,
  ② `WSGameServer`에 유닛 테스트 프로젝트(xUnit·Shouldly·Moq)를 만들 것.**
- 파이프라인의 입력(`GameDesign/Excel/*.xlsx`)은 공용인데 실행 스크립트만 `Server/`에 있어
  기획 데이터를 고치는 사람이 서버 폴더로 건너가야 했다.
- 두 ps1 모두 `C:\Users\wlsdn\...` **절대경로가 박혀 있어** 다른 체크아웃에서 깨졌다.

## 변경 내용

**파이프라인 이전 · 경로 고정**

- `Server/generate-tables.ps1` → **`GameDesign/generate-tables.ps1`** (`git mv`).
- 경로를 스크립트 위치에서 유도하도록 교체.
  `$RepoRoot = Split-Path -Parent $PSScriptRoot` → `$ServerRoot` / `$UnityRoot`.
  기존 `$UnityRoot` 절대경로 상수 제거.
- `sync-protocol-to-unity.ps1` 호출 시 `-SourceRoot`·`-DestRoot`를 **명시적으로 전달**한다.
  스크립트가 더 이상 `Server/`에 없어 상대 위치 가정이 성립하지 않는다.
- `Server/sync-protocol-to-unity.ps1` — `$DestRoot` 기본값의 절대경로를 제거하고
  `SourceRoot`(=`Server/`)의 부모에서 `Assets\Scripts_Server`를 유도하도록 변경.
  MSBuild post-build가 인자 없이 호출하는 경로도 그대로 동작한다.

**서버 테스트 프로젝트**

- `Server/WSGameServer.Tests/` 신설 (net10.0). `WSGameServer2.slnx`에 등록.
  xUnit 2.9.3 / Shouldly 4.3.0 / Moq 4.20.72 / Microsoft.NET.Test.Sdk 17.14.1.
- 세 네임스페이스를 csproj `<Using>`으로 **전역 등록**해 테스트 파일에서 `using`을 없앴다.
- `SmokeTest.cs` — 프레임워크 연결 확인용 4개(Fact 3 + Theory 1 = 실행 6건).
  서버 로직을 검증하지 않는다. **실제 테스트는 새 파일로 나눈다.**

**문서**

- `CLAUDE.md` — 폴더 구조에 `GameDesign/generate-tables.ps1`·`Server/WSGameServer.Tests/` 추가,
  **"서버 테스트"** 절 신설, 파이프라인 절에 절대경로 금지 규칙 추가.
- 스크립트 경로 표기를 `Server/…` → `GameDesign/…`으로 일괄 갱신:
  `게임기획코어.md`, `아이템/README.md`, `자원채취/README.md`,
  `자원채취/낚시/README.md`, `자원채취/사냥/README.md`,
  `game-design-reference/SKILL.md`, `GameData.csproj` 주석.

## 주요 결정 / 근거

- **스크립트를 입력 옆(`GameDesign/`)에 뒀다.** 실행 주체가 기획 데이터를 고치는 사람이고,
  산출물은 서버·Unity 양쪽으로 나가므로 `Server/`는 소유자로서 부정확한 위치였다.
- **경로를 파라미터가 아니라 `$PSScriptRoot` 유도로 고정했다.** 파라미터화하면 호출부마다
  값을 넘겨야 하고 빠뜨리면 조용히 잘못된 위치에 쓴다. 스크립트 위치는 저장소 구조상 불변이다.
- **`sync-protocol-to-unity.ps1`은 `Server/`에 남겼다.** `MikaProtocol` post-build가
  `$(SolutionDir)` 기준으로 호출하므로 옮기면 빌드가 깨진다. 기본값의 절대경로만 제거했다.
- **Moq 대상은 테스트 파일 안의 더미 인터페이스로 뒀다.** 서버 인터페이스를 끌어오면
  스모크 테스트가 그 타입의 변경에 끌려다닌다.

## 후속 작업 / 주의사항

- ⚠️ **파이프라인 전체(2·3단계)를 아직 통과시키지 못했다.** 1단계에서
  `[ItemTable.Desciption] 셀이 비었는데 Default(Null) 값이 없습니다`로 중단된다.
  `Item.xlsx`에 새로 추가된 어종 `1001~1006`의 `Desciption`이 비어 있다 — **데이터 문제이며
  이번 이동과 무관하다.** 경로 해석은 6개 전부 검증했다(엑셀 읽기·`Shared\Data` 쓰기·
  `DataLog` 쓰기 절대경로가 로그로 확인됨, 나머지는 `Test-Path`로 확인).
- ⚠️ **제너레이터가 "빈 문자열 기본값"을 표현할 수 없다.**
  `ExcelGenerator.cs:245`가 `Default(Null)` 셀을 `Length > 0`일 때만 인정하므로,
  `Desciption` 같은 **선택 항목 string 컬럼의 빈 셀을 허용할 방법이 없다.**
  자리표시자(`-`)를 넣거나 제너레이터를 고치는 판단이 필요하다.
- `Server/.claude/settings.local.json`에 옛 경로(`generate-tables.ps1`을 `Server/` 기준으로
  실행)를 허용하는 항목이 남아 있다. 개인 설정이라 **건드리지 않았다** — 필요하면 직접 갱신.
- `WSGameServer/Program.cs:17`의 `GameTable.LoadAll` 빌드 오류는 **해소됐다**(사용자 수정 확인).
- 테스트 실행: `dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj` → 6건 통과.

---

## 업데이트 (2026-07-29) — 경로 하드코딩 제거 및 생성물 정리

위 "후속 작업"의 두 ⚠️ 항목을 해소하고, 그 과정에서 발견한 결함 하나를 함께 고쳤다.

### 변경 내용

- **`ExcelGenerator/Program.cs`** — 프로젝트 폴더 상대(`ResolvePath("../../GameDesign/Excel")`)를
  **저장소 루트 기준**으로 교체. 루트는 ① 첫 인자 ② 소스 위치에서 위로 올라가며
  `.git`·`GameDesign` 표식 탐색 순으로 정한다. 경로는 상단 상수 4개(`ExcelDirRel` 등)로 모았다.
  `ResolvePath`는 프로젝트와 함께 움직여야 하는 `packerDir`에만 남겼다.
  입력 폴더 부재 시 `AssertExists`로 즉시 중단한다(없으면 "테이블 0개"로 조용히 성공한다).
- **`generate-tables.ps1`** — `dotnet run … -- $RepoRoot`로 루트를 명시 전달.
- **`TableCodeGenerator.BuildStringDefaultLiteral`** — 신규.
  `Default(Null)`에 **`""`** 를 적으면 빈 문자열이 기본값이 된다.
  기존에는 `Default(Null)` 셀이 비면 "기본값 없음"으로 읽혀 **빈 문자열을 기본값으로 두려는 의도를
  표현할 방법이 아예 없었다.** "빈 셀 = 오류" fail-fast는 그대로 유지된다.
- **`ExcelGenerator.RemoveOrphans`** — 신규. 생성 후 현재 테이블 목록에 없는
  `.bytes`·`.json`을 삭제한다.
- **`Item.xlsx`** — `Desciption` 컬럼 `Default(Null)`(G7)에 `""` 지정.
  **어종 설명 텍스트는 기획 영역이라 채우지 않았다.** 선택 항목이라는 스키마 선언만 넣었다.
- **`Drop.xlsx`** — `FishingBasicTable.Description`(E7)에도 같은 지정.
  사용자 확인: **설명 컬럼은 비어 있어도 되는 것이 정상이다.**

**`Description` = 예약 메모 컬럼 (사용자 지시로 명시)**

- `TableCodeGenerator.MemoColumnName` — `Description` 컬럼에 생성 주석
  `[기획 메모 — 로직에서 읽지 않는다]`를 자동으로 붙인다.
  **이 컬럼은 게임 로직에 아무 영향을 주지 않는다.** 값을 바꾸거나 비워도 동작이 같다.
- `Item.xlsx` — 컬럼명 오타 **`Desciption` → `Description`** 교정.
  생성물 외에 참조하는 코드가 **한 곳도 없어** 지금이 고칠 수 있는 마지막 시점이었다.
- 두 엑셀의 `//` 헤더 행을 `설명 (메모 — 로직 미사용)`으로 바꿔 시트에서도 바로 보이게 했다.
- `CLAUDE.md` — "예약 컬럼 — `Description`" 절 신설.
  **로직에 쓰는 수치를 메모에 적지 말 것**을 함께 못박았다
  (`Weight=6590`과 메모 `0.659`처럼 두 곳에 적으면 조용히 어긋난다).
- `CLAUDE.md` — 엑셀 마커 행 표(`Type`/`Min`/`Max`/`Default(Null)`/`Ref`) 신설, 경로 규칙 보강.

### 주요 결정 / 근거

- **루트 탐색과 인자 전달을 둘 다 뒀다.** 파이프라인은 이미 루트를 알므로 넘기는 게 확실하고,
  툴을 직접 실행할 때는 인자 없이도 동작해야 한다.
- **`""` 표기를 택했다.** string 컬럼 전체를 옵셔널로 바꾸면 오타로 비운 셀까지 통과한다.
  의도를 엑셀에 적게 해서 fail-fast를 지켰다.
- **고아 정리는 "먼저 비우기"가 아니라 "생성 후 삭제"다.** 생성이 중간에 실패해도
  기존 산출물이 남아 있어야 한다.

### 발견한 결함

`FishingSpecialTable` 시트가 삭제됐는데 `.bytes`·`.json`이 소스에 남아 **Unity StreamingAssets까지
계속 복사되고 있었다.** `.cs`는 `TableCodeGenerator`가 폴더를 비워 해결되지만 데이터는 아니었다.
미러링 스크립트는 "소스에 있는 파일"을 옮길 뿐이라 소스의 고아를 걸러내지 못한다.
`RemoveOrphans` 추가 후 재실행에서 소스·Unity 양쪽 모두 정리됐다.

### 검증

- 파이프라인 **3단계 전부 통과, 종료 코드 0.** 경로 5개가 실제 I/O로 확인됨
  (루트 탐색 → 엑셀 읽기 → `GameData` 쓰기 → `Shared/Data` 쓰기 → `DataLog` 쓰기).
- 고아 정리 동작 확인: `[정리] … FishingSpecialTable.bytes` → 미러에서도 `del` 로그.
- 빈 셀 → 빈 문자열 저장 확인: `ItemTable.json`의 어종 `1001~1006` 전부 `Desciption = ""`.
- 솔루션 빌드 **경고 0·오류 0**, 테스트 6건 통과.

### 주의 — `FishingSpecialTable`은 임시로 빠져 있다

사용자가 시트를 **임시로 지운 것이며 나중에 다시 추가한다.**
[낚시 기획서](../../GameDesign/기획/자원채취/낚시/README.md) 5장의 스키마
(`DropTID`·`SpotTID`·`ItemTID`·`Weight`)는 **유효하므로 문서를 고치지 않았다.**
시트를 되살리면 `RemoveOrphans`가 지운 `.bytes`·`.json`도 자동으로 다시 생성된다.
