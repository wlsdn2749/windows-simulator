---
date: 2026-07-30
title: 작업속도 계산을 가산/승산 분류 합성으로 재구성
tags: [server, workstation, balance, design]
---

# 작업속도 계산을 가산/승산 분류 합성으로 재구성

## 목적 / 배경

- `ResolveSlotSpeed`가 `적성속도 × 전역배수` 곱 하나뿐이라, 앞으로 붙을
  특성 패시브·액티브 부스트·장비를 **어떻게 합성할지가 정해져 있지 않았다.**
- 그대로 두면 항이 추가될 때마다 곱셈이 쌓여 보정원의 **개수**가 밸런스를 흔든다
  (`+25%` 여덟 개 = 곱셈 ×5.96 vs 가산 ×3.00).
- 기획 결정: **보정을 add/mul로 먼저 분류해 모아 두고 마지막에 한 번 적용한다.**
  기본은 가산, 승산은 "총 작업속도 증가"류 예외.

```
속도 = 적성기본값 × (1 + Σ가산) × Π승산
```

## 변경 내용

- `Server/WSGameServer/User/WorkStation/WorkSpeed.cs` — **신규.** 보정 누산기(readonly struct).
  `From(기본값)` → `.Add(천분율)` / `.Multiply(배수|천분율)` → `.Resolve()`.
- `Server/WSGameServer/User/User.WorkStation.cs` — `ResolveSlotSpeed`가 `WorkSpeed`를 쓴다.
  전역 배수는 `.Multiply()`(승산)로 분류. 특성·부스트·장비가 붙을 자리를 주석으로 표시.
- `Server/WSGameServer.Tests/WorkStation/WorkSpeedTest.cs` — **신규 13개.** 전체 83개 통과.
- `GameDesign/기획/작업슬롯/README.md` — 1장 확정 표에 항목 10 추가, 3장 속도 행 갱신,
  **3.4 속도 보정 합성** 절 신규(곱셈 대비 표 포함).
- `GameDesign/기획/게임기획코어.md` — 5장 확정 현황에 2줄 추가.
- `일감/T-002-장비슬롯.md` · `일감/README.md` — `EquipSlot` 부위 목록 확정으로 **보류 해제**.

## 주요 결정 / 근거

- **가산이 기본, 승산이 예외.** 가산은 "적힌 숫자 = 그 보정의 몫"이 보정원 개수와 무관하게
  유지된다. 곱셈은 항을 하나 넣을 때마다 기존 보정 전체의 가치가 함께 올라 재밸런싱을 부른다.
- **모으는 것과 적용하는 것을 분리.** 실수 연산이 `Resolve()` 한 곳에서만 일어나고 `int`로
  끝나므로, 진행도 누적(`ConsumeJudgeCount`)은 끝까지 정수로 남는다.
  `ResolveSlotSpeed`가 여전히 유일한 호출 경로라 **"정산 → ApplySpeed" 소급 방지가 그대로 유지**된다.
- **가산 factor는 먼저 곱하고 나중에 나눈다** (`base × (1000+add) / 1000`). 순서를 뒤집으면
  1 미만이 잘려 나간다.
- **승산 곱은 `_mulRate == 0`을 "승산 없음"으로 쓴다.** `Multiply`가 0 이하를 거부하므로
  0이 실제 배수로 들어올 수 없어 모호하지 않다. `struct` 기본값 footgun을 피하기 위한 선택.
- **승산 0을 금지**했다. 속도 0은 "정지"가 아니라 배치를 비우는 것으로 표현한다는 기존 방침
  (`MinWorkSpeed`)과 같은 이유 — 배수 0을 허용하면 보정 하나가 슬롯을 조용히 멈춰 세운다.
- **적성 0은 보정으로 뚫리지 않는다.** 기본값 0에 가산은 0의 비율이라 늘어날 것이 없다.
  배치 제한(`CanWork`)이 속도 경로에서도 유지되는지 테스트로 못박았다.
- **장비 효과도 가산.** T-002가 `ResolveSlotSpeed`에 `.Add()`로 붙는다.

## 업데이트 (2026-07-30) — 속도 이름을 `CurrentWorkSpeed`로 정리

`SpeedPermille` 하나가 **적성 기본값과 최종 확정값 두 곳에 같은 이름으로** 쓰이고 있어 바꿨다.
클라이언트가 아직 아무것도 참조하지 않는 시점(T-006 미착수)이라 지금이 가장 싼 타이밍이었다.

| 이전 | 이후 | 무엇 |
| --- | --- | --- |
| `WorkSpeedTable.SpeedPermille` (엑셀 컬럼) | **`BaseWorkSpeedPermille`** | 적성별 기본값 — 계산의 **입력** |
| `WorkStationSlot.SpeedPermille` | **`CurrentWorkSpeed`** | 보정 전부 적용된 **결과** |
| `WorkStationSlotInfo.SpeedPermille` (패킷) | **`CurrentWorkSpeed`** | 위와 동일. 클라는 이것만 본다 |
| `WorkStationSlot.BaseSpeedPermille` | **`DefaultWorkSpeed`** | 1.0배 기준 상수 (엑셀 `Base~`와 헷갈리지 않게) |
| `WorkStationSlot.MinSpeedPermille` | **`MinWorkSpeed`** | 하한 |
| `WorkStationSlot.SpeedScale` | **`WorkSpeedScale`** | 천분율 단위 (1000) |
| `WorkStationSlot.ApplySpeed()` | **`ApplyWorkSpeed()`** | |
| `Character.GetWorkSpeedPermille()` | **`GetBaseWorkSpeed()`** | 반환값이 기본값이므로 이름을 맞췄다 |

- **누산기 `WorkSpeed` struct는 이름을 유지했다.** 프로퍼티가 `CurrentWorkSpeed`라 충돌하지 않는다.
- 엑셀 컬럼을 바꿨으므로 `Character.xlsx` + 생성물(`GameData`·`.bytes`·`DataLog`·Unity 미러)을
  **같은 커밋에 담아야 한다.** `generate-tables.ps1` 실행 완료.
- `WorkSpeedTID`는 그대로다 — `CharacterTable`의 `Ref`가 이 이름을 가리킨다.
- 테스트 83개 통과 (실패 0).

## 후속 작업 / 주의사항

- **`WorkSpeedTable`은 적성 0~10의 이산 룩업이다.** 레벨·강화치 같은 연속값을 기본값에
  섞으려면 여기가 병목 — T-003(캐릭터 성장)에서 다시 봐야 한다.
- 가산 항이 늘어나면 **속도 상한이 사라진다.** 현재 상한은 `WorkSpeedTable` 최댓값(4배)뿐이고
  하드 상한은 없다. `JudgeCost`가 3천만이라 오버플로 여유는 충분하나 밸런스 상한은 별도 결정.
- **산출량 축이 없다.** `YieldPerJudge = 1` 고정이라 모든 성장이 "빨라진다" 한 방향뿐 → T-009.
- 총량 = 접속시간 × 슬롯수 × 속도 로 **세 항이 곱셈**이라는 게임기획코어 경고는 그대로다.
- 보석이 독립 부위인지 무기 소켓인지 미확인 → `일감/T-002-장비슬롯.md` "남은 질문".
