---
date: 2026-08-02
title: 가챠 응답에 인벤토리 변경분(누적 총량) 포함 — 이슈 #8
tags: [server, protocol, gacha, inventory]
---

# 가챠 응답에 인벤토리 변경분 포함 (깃허브 이슈 #8)

## 목적 / 배경

- 아이템 지급 3경로 중 가챠만 인벤토리 갱신 통지가 없어, 클라가 연출용
  `GachaRewardInfo`(델타)를 로컬 가산해 쓰고 있었다 — 상세는 깃허브 이슈 #8.
- 클라 규칙을 "총량 덮어쓰기" 하나로 통일할 수 있도록 서버가 총량을 내려 준다.

## 변경 내용

- `Server/MikaProtocol/MikaPacket.cs` — `S_GachaDrawResponse`에
  `ItemChangeInfos`(갱신 후 누적 총량) 추가. Rewards는 연출 전용으로 남는다.
- `Server/MikaProtocol/PacketInfo.cs` — `ItemChangeInfo` 주석 정정
  (`변경(델타) 전용` → 누적 총량). 값은 원래 총량이었고 주석만 반대였다.
- `Server/WSGameServer/Gacha/GachaService.cs` — `GainItem` 반환값을 모아 응답에 포함.
- `Server/MikaDummyClient/Network/ServerPacketHandler.cs` — 가챠 수신 시 변경분도 출력.
- `Server/WSGameServer.Tests/Inventory/InventoryTest.cs` (신규) —
  `Inventory.AddItem` 반환 Count가 델타가 아니라 누적 총량임을 고정하는 회귀 테스트 3개.

## 주요 결정 / 근거

- 이슈 원문은 별도 `S_UpdateItemResponse` 전송을 요청했지만, **응답 패킷에 필드로 싣는
  방식**을 택했다(사용자 지시). 채취 `S_GatherResultResponse`가 이미 같은 패턴이고,
  패킷 2개의 도착 순서 문제도 없다.
- `GachaService.Draw`는 `User`(구체 클래스)·`GachaTable`(static)에 묶여 유닛 테스트가
  어렵다. 대신 패킷 값의 규칙을 만드는 이음새인 `Inventory.AddItem`에 테스트를 걸었다.

## 후속 작업 / 주의사항

- **클라(`SessionManager`) 정리는 이슈 작성자(클라 담당) 몫** — `AddGachaRewards`를 지우고
  `OnGachaDrawResponse`에서 `ApplyItemChanges(res.ItemChangeInfos)`로 교체하면 된다.
  교체 전까지 클라는 여전히 델타를 더하는데, 별도 통지를 안 보내므로 이중 반영은 없다
  (오늘 기준 로컬 가산 = 서버 총량이라 동작 동일).
- `SessionManager.cs:228` 주석이 `PacketInfo` 주석을 "총량"의 근거로 인용하는데,
  이번 정정으로 이제 참조 방향이 맞아졌다.
- MemoryPack은 필드 순서 기반이라 **서버·클라 빌드를 같이 갱신**해야 한다.
  미러(`Assets/Scripts_Server/Protocol`)는 빌드 시 자동 동기화됐고 같은 커밋에 담았다.
