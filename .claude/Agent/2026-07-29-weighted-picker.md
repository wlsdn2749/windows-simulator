---
date: 2026-07-29
title: 가중치 추첨기 WeightedPicker 도입
tags: [server, gathering, drop, gacha, test]
---

# 가중치 추첨기 WeightedPicker 도입

## 목적 / 배경

- 사용자 요청: **"앞으로 모든 DropTable에 사용할 (아마 가챠에서도 쓸) 확률 계산 클래스를
  기존 Gacha와는 별개로 만들어 달라."**
- 채취 보상은 3층 구조(→ [자원채취](../../GameDesign/기획/자원채취/README.md) 2.4)라
  판정 1회에 롤이 **3번** 돈다(기본/특별 분기 → 희귀도 or 특별 → 공통보상).
  여기에 산업 5종·가챠까지 같은 계산을 각자 구현하면 확률 버그가 흩어진다.
- 기존 `Gacha/GachaTable.cs`는 풀이 **코드에 하드코딩**돼 있고
  `Draw()`가 **호출마다 가중치 합을 다시 더한다.** 재사용할 수 있는 형태가 아니다.

## 변경 내용

- **`WSGameServer/Common/WeightedPicker.cs`** — 신규.
  - `WeightedPicker<T>` — 불변 추첨기. 생성 시 누적 가중치를 쌓고 추첨은 이진 탐색 O(log n).
    `Pick` / `PickMany`(리스트) / `PickMany`(콜백) / `ProbabilityOf` / `TotalWeight`.
  - `WeightedPicker.From(...)` — 제네릭 인자를 적지 않도록 타입 추론되는 진입점.
  - `WeightedPicker.GroupBy(...)` — `(그룹 키 → 추첨기)` 사전. 드롭 시트가
    `SpotTID`·`FieldTID`를 **키가 아닌 일반 컬럼**으로 두는 구조라, 로드 후 서버가 만들어야 하는
    그룹 인덱스가 바로 이것이다(자원채취 3장 제약).
- **`WSGameServer.Tests/Common/WeightedPickerTest.cs`** — 신규, 19건.

## 주요 결정 / 근거

- **누적합을 생성 시점에 한 번만 만든다.** 오프라인 정산은 접속 한 번에 판정을
  최대 2,880회 몰아서 돌린다(24시간). 추첨마다 합을 다시 더하면 그게 그대로 비용이 된다.
- **`Random`을 인자로 받는다(기본값 `Random.Shared`).** 시드를 고정할 수 있어야
  정산을 재현하고 테스트를 결정적으로 짤 수 있다. `Random.Shared`는 스레드 안전이다.
- **가중치 0은 후보에서 빼고, 음수는 예외.** 0은 "당분간 안 나오게 막아 둔다"는 흔한 기획
  의도라 정상 입력으로 받고, 음수는 어떤 의도로도 해석되지 않아 데이터 오류로 본다.
  0을 남겨 두면 누적 구간의 길이가 0이 되어 이진 탐색 경계에서 잘못 잡힐 수 있다.
- **항목 타입을 모른다(`T` 제네릭 + `Func<T,int>`).** `ItemTID`를 안다고 가정하면
  희귀도 롤·가챠·미래 테이블에 못 쓴다. 아이템 해석은 호출부 몫이다.
- **기존 `Gacha`는 건드리지 않았다.** 사용자가 "그거랑 다른 걸로"라고 명시했다.
  `GachaTable.Draw`를 이 클래스로 옮기는 것은 가능하지만 별도 판단이 필요하다.

## 테스트에서 신경 쓴 것

- **구간 경계를 난수 목(Moq)으로 고정해 결정적으로 검증한다.**
  `Random.Next(int)`가 virtual이라 목으로 정확한 roll 값을 주입할 수 있다.
  누적 `[790, 940, 990, 1000]`에서 `789/790`, `939/940`처럼 **경계 양쪽을 겨냥**해
  이진 탐색의 off-by-one을 잡는다. 난수를 그냥 돌려 보는 방식으로는 이게 안 걸린다.
- `Next(1000)`으로 요청하는지도 검증한다 — 범위를 `TotalWeight`로 넘기면
  마지막 항목의 확률이 조용히 어긋난다.
- 분포 수렴은 별도로 20만 회 표본에서 본다(시드 고정).

## 드롭 테이블이 여러 개로 늘어날 때 (2차 추가)

사용자 지적: **"드롭 테이블이 여러 개 생겼을 때도 대응해야 한다."**
호출부가 각자 `WeightedPicker.From`을 부르면 캐시 위치가 흩어지고
"판정마다 새로 만드는" 실수가 섞여 든다. 만드는 곳을 한 군데로 모았다.

- **`WSGameServer/Common/DropTable.cs`** — 신규.
  `DropEntry(ItemTID, Weight)`로 정규화해 `WeightedPicker`를 감싼다.
  Row 타입이 산업마다 다르므로(`FishingBasicTableRow`·`MiningBasicTableRow` …)
  **셀렉터 두 개만 받아 공통 형태로 흡수**한다. 결과는 `ItemTID` 하나로 좁혔다.
  `RollMany`/`RollManyInto`는 정산용으로 `Dictionary<ItemTID, 개수>` 집계까지 해 준다.
- **`WSGameServer/Common/DropTableCatalog.cs`** — 신규(Singleton).
  `ItemType`(산업) → `DropTable`. **`LoadAll()`이 유일한 등록 지점**이고,
  시트를 추가하면 여기에 한 줄을 더한다.
- **`WSGameServer/Program.cs`** — `GameTable.LoadAll` 직후 `DropTableCatalog.Instance.LoadAll()` 호출.

### 범위를 좁힌 이유 (중요)

처음에는 기획서(자원채취 2.4)대로 `DropLayer { Basic, Special, Common }` 3층 키로 만들었으나,
**사용자 확인 결과 지금은 그 단계가 아니다:**

> "공통보상은 없어. 지금은 희귀도를 나누지 않고, 나중에 할 생각이고.
>  지금 당장은 일반보상만 뽑으려고 기능 만들 겸. 그래서 엑셀에도 1개만 넣었잖아"

실제 `FishingBasicTable`에 **희귀도 컬럼이 없다**(`DropTID`/`ItemTID`/`Weight`/`Description`).
그래서 **키를 산업 하나로 줄였다.** 층이 생기면 키에 축을 더하고
`Get(industry)`가 기본 층을 가리키게 두면 기존 호출부는 그대로 간다.

> ⚠️ **기획서와 현재 데이터가 다르다.** 기획서는 3층 + 희귀도 롤을 그리고 있으나
> 실제 시트는 단일 층·희귀도 없음이다. **기획이 앞서 있는 상태이며 데이터가 정상**이다.
> 문서를 고치지 않았다 — 나중에 그쪽으로 갈 계획이기 때문이다.

## 후속 작업 / 주의사항

- **아직 드롭을 굴리는 게임 로직은 없다.** 카탈로그에 등록까지만 돼 있다.
  채취 정산(판정 횟수 × 산출량)은 기획 미확정 항목이 많아
  (→ [자원채취](../../GameDesign/기획/자원채취/README.md) 5장) 만들지 않았다.
- 사용 예:
  ```csharp
  // 서버 시작 시 한 번 (Program.cs)
  DropTableCatalog.Instance.LoadAll();

  // 판정마다
  var itemTid = DropTableCatalog.Instance.Get(ItemType.Fishing).Roll();

  // 오프라인 정산 — 판정 수천 회를 한 번에
  var gained = DropTableCatalog.Instance.Get(industry).RollMany(judgeCount);
  ```
- **추첨기는 로드 시 한 번 만들어 재사용한다.** 판정마다 `From`을 부르면
  누적합을 미리 쌓는 이점이 사라진다.
- 그룹 축이 있는 시트(낚시터·사냥터·깊이)는 `WeightedPicker.GroupBy`로 나눈다.
  아직 그런 시트가 없어 `DropTable`에는 그룹 개념을 넣지 않았다.
- `Gacha/GachaTable.cs`가 같은 계산을 따로 갖고 있다. 통합하면 확률 로직이 한 곳으로 모이지만,
  가챠 풀을 엑셀로 옮기는 작업과 함께 판단하는 편이 낫다.
