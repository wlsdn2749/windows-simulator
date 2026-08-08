---
date: 2026-08-02
title: 가챠 풀 엑셀 이관 + ItemRarity → GlobalRarity 개명 — 이슈 #9
tags: [server, gacha, data, protocol, excel]
---

# 가챠 풀 엑셀 이관 + GlobalRarity 개명 (깃허브 이슈 #9)

## 목적 / 배경

- 이슈 #9: 가챠 풀이 코드 하드코딩이라 존재하지 않는 아이템(1001~4001)을 주고,
  희귀도가 코드(`GachaEntry.Rarity`)와 엑셀(`ItemTable`) 두 곳에 따로 있었다.
- 가챠 풀을 엑셀로 옮겨 `Ref` 검사를 태우고, 희귀도 enum을 전역 이름으로 개명했다.

## 변경 내용

- `GameDesign/Excel/Enum.xlsx` — 시트 `ItemRarity` → `GlobalRarity` 개명.
- `GameDesign/Excel/Item.xlsx` — 타입 `eGlobalRarity`·컬럼명 `GlobalRarity`로 변경,
  가챠 전용 아이템 6종 추가(`100001~100006` 구슬 시리즈, `Special`, 등급 6단계 — 테스트값).
- `GameDesign/Excel/Gacha.xlsx` (신규) — `GachaTable` 시트.
  컬럼 `GachaTID / GachaId / ItemTID / Count / Weight / Description`,
  `ItemTID`에 `Ref: ItemTable.ItemTID`. 풀 1개(GachaId=1), 가중치 합 1000.
- `Server/MikaProtocol/PacketEnum.cs` — `EItemRarity` → `EGlobalRarity`,
  값을 GameData와 1:1로 정렬(Uncommon=2·Mythic=6 추가).
- `Server/WSGameServer/Gacha/GachaTable.cs` **삭제** →
  `GachaPoolCatalog.cs` 신규(엑셀 로드, `GachaId`별 `WeightedPicker`).
- `GachaService.cs` — 카탈로그 추첨으로 교체, 등급은 `GameTable.ItemTable`에서 조회.
- `Server/WSGameServer.Tests/Gacha/GachaPoolCatalogTest.cs` — 정규화·풀 분리 테스트 4건.
- 기획 문서 갱신: `게임기획코어.md` 4장 희귀도·식별자 표, `아이템/README.md` 1·4장.

## 주요 결정 / 근거

- **등급을 가챠 시트에 두지 않았다.** 풀은 `(ItemTID, Count, Weight)`만 갖고 등급은
  `ItemTable`에서 읽는다 — 이슈 #9 2-3(등급 이중 소유)의 재발 차단. 연출 등급은
  `RarityOf()`가 조회해 패킷에 싣는다.
- **프로토콜 `EGlobalRarity` 값을 GameData `GlobalRarity`와 1:1로 정렬했다.**
  ⚠️ 구 `EItemRarity`는 `Rare=2`였는데 신 enum은 `Uncommon=2`다 — **와이어 값 의미가
  바뀌었으므로 서버·클라를 반드시 같이 빌드**해야 한다(연출용 일회성 값이라 DB 영향 없음).
  서버는 `(EGlobalRarity)(byte)row.GlobalRarity` 캐스팅으로 실어 보낸다 — 두 enum이
  어긋나면 조용히 틀리므로, enum을 고칠 땐 양쪽을 함께 고친다.
- `GachaPoolCatalog`는 `DropTableCatalog`와 같은 패턴(시작 시 1회 로드·불변·Singleton,
  테스트는 `new` 인스턴스). 추첨은 기존 `WeightedPicker.GroupBy` 재사용 — 신규 추첨 코드 없음.
- 가챠 아이템은 `100000` 대역(사용자 지시). 산업 대역(10000단위)과 겹치지 않는다.

## 후속 작업 / 주의사항

- **클라(`PacketTestPanelUI` 등)는 재빌드만 하면 된다** — `Rarity` 필드명은 그대로,
  타입만 `EGlobalRarity`로 미러됐다.
- 가챠 아이템 이름·가중치·풀 구성은 **테스트값**이다. 가챠 결과물의 정체(캐릭터?)는
  여전히 기획 미정 → `아이템/README.md` 4장.
- 이슈 #9 잔여: 캐릭터 테이블 확충(4-2), `DefaultSlotCount`·`BaseCycleSeconds` 등
  상수 선 긋기(4-1)는 이번 작업에 포함하지 않았다.
