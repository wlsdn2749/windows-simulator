---
date: 2026-07-27
title: ExcelGenerator 생성 코드를 C# 9(블록 네임스페이스)로 수정 — Unity CS8773 해소
tags: [server, excelgenerator, codegen, unity, langversion]
---

# ExcelGenerator 생성 코드를 C# 9로 수정 (Unity CS8773 해소)

## 목적 / 배경

GitHub 이슈 **#6** 대응. `ExcelGenerator`가 `namespace GameData;`(file-scoped namespace, C# 10)를
방출해, Unity(기본 C# 9)로 미러링된 4개 파일이 **CS8773**으로 컴파일에 실패했다.

`GameData.csproj`가 `LangVersion 11`이라 **서버 빌드는 이 위반을 통과시킨다.**
검증 지점이 Unity 에디터뿐이어서 클라이언트 측에서 뒤늦게 발견됐다.

이슈의 두 방안 중 **방법 1(생성기 출력 문법 수정)** 을 선택했다.

## 변경 내용

### 1. 생성 코드를 블록 네임스페이스로 (근본 수정)

- **`Server/ExcelGenerator/CodeGenUtil.cs` (신규)**
  `BuildFile(ns, usings, body)` — using 목록 + 블록 네임스페이스로 감싸고 본문을 한 단계 들여쓴다.
  네임스페이스 생성을 **한 곳으로 모아** 생성기마다 규약을 놓치지 않게 했다.
- **`Server/ExcelGenerator/EnumGenerator.cs`** — `namespace GameData;` 직접 출력 제거, `BuildFile` 사용.
- **`Server/ExcelGenerator/TableCodeGenerator.cs`** — 7개 지점 전부 `BuildFile` 경유로 변경.
  - `BuildRowClass` / `BuildPacker` / `BuildRegistry` / `BuildGameTable`: 본문만 만들고 반환 시 감싼다.
  - `TableSetSource` → `TableSetUsings` + `TableSetBody`로 분리.
  - `PackerUtilSource` → `PackerUtilUsings` + `PackerUtilBody`로 분리.
    (고정 소스의 using·namespace를 떼어내 `BuildFile`이 붙이게 했다 — 문자열을 손으로 들여쓰지 않아도 된다)

### 2. 파이프라인에 C# 9 규약 검사 추가

- **`Server/generate-tables.ps1`** — 2단계로 `Assert-CSharp9` 신설 (기존 2단계 → 3단계로 재번호).
  미러링 **전에** `Server/GameData/**/*.cs`를 스캔해 아래 문법을 차단한다.

  | 검출 패턴 | 도입 버전 |
  | --- | --- |
  | file-scoped namespace | C# 10 |
  | global using | C# 10 |
  | record struct | C# 10 |
  | required 멤버 | C# 11 |
  | 컬렉션 표현식 `= [` | C# 12 |

  위반 시 파일·라인·해당 소스를 출력하고 파이프라인을 중단한다.

### 3. `Assets/Scripts/` 삭제

`Protocol.meta` 하나만 있던 빈 껍데기였다(실제 코드는 `Assets/Scripts_Server/`).
`Assets/Scripts.meta`와 함께 제거.

### 4. `.meta` 9개는 이미 로컬에 생성돼 있었음

이슈 6-1의 누락 건은 에디터를 열어 이미 생성된 상태(untracked)였다. 커밋만 하면 닫힌다.

## 주요 결정 / 근거

- **`LangVersion`을 9로 낮추는 방안은 불가능하다.** 이슈 3-4에서 제안됐고 실제로 시도했으나,
  `[MemoryPackable]` 때문에 **MemoryPack 소스 생성기가 `static abstract` 구현과 `scoped` 수정자(C# 11)를
  방출**한다. C# 9로 낮추면 `CS8703`/`CS8987`이 발생한다.
  - `Server/GameData` → 오류 5건
  - `Server/MikaProtocol` → 오류 78건
  - 따라서 **두 프로젝트 모두 `LangVersion 11`을 유지**하고, csproj 주석에 이유를 남겼다.
    (obj/의 생성기 산출물은 Unity로 미러링되지 않는다. Unity는 자체 MemoryPack 패키지로 다시 생성한다.)
- **그래서 서버 빌드는 규약을 강제할 수 없다.** LangVersion 대신
  `generate-tables.ps1`의 검사 단계가 그 역할을 대신한다 — Unity를 열지 않고 위반을 잡는 유일한 지점.
- **검사 대상에서 `obj/`·`bin/`을 제외**해야 한다. `EmitCompilerGeneratedFiles=true`라
  MemoryPack `.g.cs`가 obj/에 쌓이고 이들이 file-scoped namespace를 쓴다.
  제외하지 않으면 오탐으로 파이프라인이 멈춘다(실제로 걸려서 수정함).
- **`Packer/`도 블록 네임스페이스로 통일했다.** 미러 대상이 아니라 유지해도 됐지만,
  생성기 안에서 "이건 미러되고 저건 안 됨"을 구분하면 실수가 난다.
  블록 네임스페이스는 C# 9~13 전부 유효하므로 통일해도 손해가 없다.

## 검증

| 항목 | 결과 |
| --- | --- |
| `generate-tables.ps1` 전체 실행 | ✅ 3단계 모두 통과 (`ok : C# 9 범위 준수`) |
| 미러본 4개 namespace 형태 | ✅ 전부 `namespace GameData` (블록) |
| file-scoped namespace 잔존 | ✅ 없음 (미러본 + `Server/GameData` 소스) |
| 서버 솔루션 전체 빌드 | ✅ 오류 0 · 경고 0 |
| 검사기 동작 | ✅ obj/의 실제 file-scoped namespace를 검출함을 확인 |

**Unity 에디터에서의 최종 확인은 아직 하지 않았다.** 이슈 6-3 절차대로 에디터를 열어
콘솔 오류가 사라졌는지 확인이 필요하다.

## 후속 작업 / 주의사항

- ⚠️ **Unity 에디터를 열어 CS8773이 사라졌는지 확인할 것.** 이슈 6-3 절차.
- ⚠️ **`.meta` 9개가 untracked 상태다.** 원본과 함께 커밋해야 한다 (GUID는 먼저 커밋한 쪽이 기준).
- ⚠️ **`MikaProtocol`은 `LangVersion 11`이라 손으로 쓴 패킷 정의의 C# 9 위반을 컴파일러가 잡지 못한다.**
  현재 `Assert-CSharp9`는 `Server/GameData`만 검사한다.
  패킷 정의에도 같은 문제가 생기면 검사 대상에 `Server/MikaProtocol`을 추가하는 것을 검토할 것
  (단 손으로 쓴 .cs만 대상이고 obj/ 제외는 이미 처리돼 있다).
- 이슈 #6은 `wontfix` 라벨이 붙어 있다. 실제로는 방법 1로 해결했으므로 라벨 정리·클로즈가 필요하다.
- 엑셀 자체는 변경하지 않았다. 생성물 diff는 네임스페이스 감싸기 + 들여쓰기 변화가 전부이며
  `GameDesign/DataLog/ItemTable.json`은 내용이 동일하다(데이터 변경 없음).
