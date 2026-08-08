---
date: 2026-07-28
title: 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입
tags: [server, excelgenerator, data, gathering, fishing]
---

# 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입

## 목적 / 배경

- 채취 보상 3층 구조(→ [자원채취](../../GameDesign/기획/자원채취/README.md) 2.4)를
  실제 데이터로 옮기는 첫 작업. 사용자 요청은 **"드롭 테이블마다 엑셀 시트를 만들고,
  `ItemTID` reference check를 하고 싶다"** 였다.
- 기존 파이프라인에는 참조 무결성 개념이 없었다. 드롭 시트의 `ItemTID`는 오타이거나
  삭제된 아이템이어도 타입(int)이 맞아 `.bytes`까지 생성되고,
  **실제 드랍이 일어나는 런타임에야** `TableSet` 조회에서 터진다.

## 변경 내용

**ExcelGenerator (참조 검사 도입)**

- `Server/ExcelGenerator/ReferenceValidator.cs` — 신규.
  `Ref` 마커("대상시트.대상컬럼")를 따라 셀 값이 대상 테이블에 실재하는지 검사한다.
  위반을 **전부 모아 한 번에** 보고하고 `InvalidDataException`으로 중단한다.
  배열 컬럼은 `,`로 나눠 원소마다 검사하고, `?` 접미사(`ItemTable.ItemTID?`)면
  빈 셀·`0`을 "참조 없음"으로 허용한다.
- `Server/ExcelGenerator/ExcelGenerator.cs` — `ColumnInfo`에 `Ref` 필드 추가,
  `ParseSheet`가 A열 `Ref` 마커 행을 읽도록 수정.
- `Server/ExcelGenerator/Program.cs` — `LoadExcel` 직후, **`GenerateCode` 이전**에
  `ReferenceValidator.Validate` 호출.

**데이터**

- `GameDesign/Excel/Drop.xlsx` — 신규. 시트 2개.
  - `FishingBasicTable` — `Rarity`(키), `ItemTID`. 6행.
  - `FishingSpecialTable` — `DropTID`(ID·키), `SpotTID`, `ItemTID`, `Weight`. 3행.
  - **행은 전부 구조 검증용 더미다.** `ItemTID`는 현재 `Item.xlsx`에 있는 테스트
    더미(TID 1~6)를 가리킨다. 실제 어종·가중치가 아니다.

**기획 문서**

- `자원채취/낚시/README.md` 5장 — 시트 존재 사실, `DropTID` 선두 규칙, `Ref` 마커 표 추가.
- `자원채취/README.md` 3장 — 제너레이터 단일키 제약 절 신설, `<산업>SpecialTable`에 `DropTID` 반영.

## 주요 결정 / 근거

- **가중치 롤 시트는 첫 컬럼에 고유 `DropTID(ID)`를 둔다.**
  `TableCodeGenerator.PickKey()`가 `ID` 컬럼(없으면 첫 비배열 컬럼)을 자동으로 키로 잡고
  `TableSet.From()`이 중복 키에 예외를 던진다 — **복합키가 없다.**
  기획서 원안 `FishingSpecialTable(SpotTID, ItemTID, Weight)`를 그대로 만들면
  `SpotTID`가 키가 되어 대물이 2종 이상인 순간 로드가 깨진다.
  그룹 축(`SpotTID`·`FieldTID`·깊이)은 일반 컬럼으로 두고 서버가 로드 후 인덱스를 만든다.
- **`BasicTable`만 `Rarity`를 키로 남겼다.** `(희귀도 → 아이템)` 1:1 매핑이라
  `GameTable.FishingBasicTable[rarity].ItemTID` 한 줄로 끝난다.
  단 채굴처럼 축이 하나 더 붙는 산업은 Basic도 `DropTID` 방식이 필요하다.
- **검사를 코드 생성 전에 뒀다.** 생성 후에 검사하면 끊어진 TID로 만든 `.bytes`가
  이미 디스크에 남고 Unity 미러링까지 흘러갈 수 있다.
- **위반을 하나씩 던지지 않고 모아서 보고한다.** 엑셀 편집은 대량 입력이라
  한 건씩 고치고 다시 돌리는 왕복이 비싸다.
- **표기를 `시트.컬럼`으로 잡았다.** 대상 키를 추론하지 않으므로
  참조 대상이 그 테이블의 PK가 아니어도 검사할 수 있다.

## 후속 작업 / 주의사항

- ⚠️ **`Server/WSGameServer/Program.cs:17`이 빌드 실패 상태다 — 이번 작업과 무관한
  기존 미커밋 변경이다.** `GameTable.LoadAll()`에 read 델리게이트 인자가 빠져 있다.
  `.bytes`는 `WSGameServer.csproj`의 Content 설정으로 실행 폴더 `Data/`에 복사되므로
  `GameTable.LoadAll(name => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", name)))`
  형태가 맞다. **건드리지 않았다.**
- **Unity `.meta` 미생성.** `Assets/Scripts_Server/GameData/Tables/`에
  `FishingBasicTable.cs`·`FishingSpecialTable.cs`가, `StreamingAssets/Data/`에
  `.bytes` 2개가 새로 미러링됐다. Unity 에디터를 한 번 열어 `.meta`를 만든 뒤 함께 커밋할 것.
- **아직 만들지 않은 시트** — `RarityWeightTable`·`CommonRewardTable`(전 산업 공통),
  `FishingSpotTable`, 그리고 농사·벌목·채굴·사냥의 Basic/Special.
  나머지 산업의 `SpecialTable`은 **고유 컬럼(파종 작물·잔여 자원량·깊이 구간)이 기획 미결**이라
  의도적으로 보류했다. → [자원채취](../../GameDesign/기획/자원채취/README.md) 5장
- `ItemType.Hunting` 미추가 건은 그대로다. 사냥 시트는 `Enum.xlsx` 수정이 선행돼야 한다.
- **확률·가중치 수치는 전부 미확정이다.** 게임기획코어 5장에서 "희귀도 분포 확률값"이
  최우선 미작성 항목이며, 임의로 채우지 않았다.
- `Item.xlsx`의 아이템이 여전히 테스트 더미(`A`~`F`/`Military1`)라
  드롭 시트가 참조할 실제 어종이 없다. 실데이터 교체가 선행돼야 드롭 값이 의미를 갖는다.
