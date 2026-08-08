---
date: 2026-07-29
title: 서버 프레임워크 프로젝트를 MikaNetwork.Lib로 묶어 폴더 구조 정리
tags: [server, refactor, structure, msbuild]
---

# 서버 프레임워크 프로젝트를 MikaNetwork.Lib로 묶어 폴더 구조 정리

## 목적 / 배경

- `Server/` 아래에 12개 프로젝트 폴더가 **평평하게** 늘어서 있어,
  네트워크 프레임워크·게임 계약·게임 앱·툴이 이름만으로 구분됐다.
- 사용자 요청: **"MikaNetwork 이거는 MikaNetwork.Lib 이렇게 뺄 수 있지 않나"**
- 범위는 확인 후 **"프레임워크 전체"** 로 결정(= 게임을 모르는 재사용 코드 5개).

## 변경 내용

**이동 (`git mv` — 전부 rename으로 추적되어 이력 보존)**

```
Server/MikaNetwork.Lib/
    MikaNetwork.Core/     MikaNetwork.Client/     MikaNetwork.Server/
    MikaUtils/            MikaSourceGen/          ← 이중 폴더 평탄화
```

- `MikaSourceGen/MikaSourceGen/` → `MikaNetwork.Lib/MikaSourceGen/`.
  상위 폴더에 다른 항목이 없어 중첩이 무의미했다.

**참조 수정**

- `MikaProtocol` · `MikaDummyClient` · `WSGameServer` — Lib 안으로 들어간 프로젝트 참조를
  `..\MikaNetwork.Lib\…`로 변경.
- `MikaNetwork.Lib/MikaNetwork.Client` — `MikaProtocol`이 Lib 밖에 남았으므로 `..\..\`로 한 단계 더.
- `WSGameServer2.slnx` — `<Folder Name="/MikaNetwork.Lib/">` 솔루션 폴더로 5개를 묶었다.
- `MikaNetwork.Lib` **내부끼리의 참조는 손대지 않았다**(같은 레벨이라 `..\`가 그대로 유효).

**문서**

- `CLAUDE.md` — 폴더 구조에 `Server/MikaNetwork.Lib/` 추가,
  **Lib 안팎의 경계 기준("게임을 아는가")** 과 이동 금지 대상을 명시.

## 주요 결정 / 근거

- **경계는 "게임을 아는가"로 그었다.** 프레임워크는 게임 타입을 모른다.
  그래서 `MikaProtocol`(게임 패킷)·`GameData`(게임 테이블)는 Lib에 넣지 않았다.
- **`MikaProtocol`·`GameData`·`ExcelGenerator`·`Shared`는 의도적으로 두었다.**
  옮기면 깨지는 지점의 성격이 다르기 때문이다:

  | 깨지는 곳 | 성격 |
  | --- | --- |
  | `ProjectReference`·`.slnx` | 빌드 즉시 실패 — **안전** |
  | `MikaProtocol` post-build `..\sync-protocol-to-unity.ps1` | 스크립트 못 찾음 — 알아채기 쉬움 |
  | `WSGameServer.csproj`의 `..\Shared\Data\*.bytes` | **조용히 실패** — 런타임에야 드러남 |
  | `ExcelGenerator/Program.cs:12-22`의 `ResolvePath("../GameData")` | **가장 위험** — 컴파일되고 엉뚱한 위치에 쓴다 |

  이번에 옮긴 5개는 위 두 위험과 **무관**하고 Unity 미러링 대상도 아니다.
  구조를 더 정리하려면 `ExcelGenerator/Program.cs`의 소스 상대경로부터 걷어내야 한다.
- **솔루션 폴더(`<Folder>`)를 물리 폴더와 같은 이름으로 맞췄다.** IDE 트리와 디스크가
  어긋나면 "어디에 있는 파일인가"를 두 번 생각하게 된다.

## 후속 작업 / 주의사항

- 검증 완료: 솔루션 전체 빌드 **오류 0**(11개 프로젝트), 테스트 6건 통과,
  `MikaProtocol` post-build 미러링 정상(`[sync-protocol] done. unchanged=5`).
  남은 경고 2건은 SQLite 패키지의 기존 `NETSDK1206`으로 이번 변경과 무관하다.
- **`Server/**/bin`·`obj`를 이동 전에 지웠다.** 옛 경로의 산출물이 남으면 참조가
  낡은 dll로 해석될 수 있다. 다른 사람이 pull한 뒤에도 한 번 정리하는 편이 안전하다.
- Unity 쪽은 영향 없다. 미러 대상(`MikaProtocol`·`GameData`)의 위치가 그대로다.
- 같은 날 선행 작업 → [파이프라인 이전 및 테스트 프로젝트 신설](2026-07-29-pipeline-move-and-tests.md)
