---
date: 2026-07-29
title: 작업슬롯 패킷 4종 Unity 클라 연동 및 MonoService null 등록 방지
tags: [client, packet, workstation, service-locator]
---

# 작업슬롯 패킷 4종 Unity 클라 연동

선행 로그: [작업슬롯 서버 구현](2026-07-29-workstation-slot-impl.md)

## 목적 / 배경

서버·더미 클라는 작업슬롯 패킷(PacketId 12~15) 대응이 끝났는데 **Unity 클라만 미구현**이라
MIKA001 경고가 떠 있었다. 이번 범위는 **패킷 왕복 확인까지**다(UI 레이아웃·연출 제외).

## 변경 내용

기존 3계층(`ServerPacketHandler` → `SessionManager` → UI)을 그대로 따랐다.

- `Scripts_Server/.../ServerPacketHandler.cs` — `[PacketHandler]` 3종 + static 이벤트 추가
- `Scripts_Client/Managers/SessionManager.cs` — `AssignWorkStation()` 요청 API,
  슬롯 캐시(`WorkStationSlots`), 가공 이벤트 3종
- `Scripts_Client/UI/PacketTestPanelUI.cs` — 배치/해제 버튼 2개 + 수신 로그
- `Scripts_Client/Common/Service/MonoService.cs` — null 등록 방지 (아래 참조)
- `Scripts_Client/NETWORK_NOTES.md` — 낡은 경로(`Scripts/` → `Scripts_Server/`) 수정,
  로그인 수신 세트·작업슬롯 절 추가

## 주요 결정 / 근거

- **배치와 해제를 한 API로 처리한다.** 서버가 같은 패킷을 쓰고 `Industry=0, CharacterId=0`이 곧
  해제다. 클라에서 굳이 둘로 나눌 이유가 없다.
- **채취 결과 로그에 수신 시각을 찍는다.** 30초 주기가 실제로 도는지 확인하는 게 이번 작업의
  목적인데, 로그 순서만으로는 간격을 알 수 없다.
- **로그인 시 자동 수신되는 패킷들에 `★` 주석을 달았다.** 인벤토리·슬롯은 조회 요청 패킷이
  아예 없어서, 모르면 "왜 요청도 안 했는데 오지?" 또는 반대로 조회 API를 찾게 된다.
- **`MonoService<T>`는 CRTP 제약(B안) 대신 런타임 검사(A안)를 택했다.**
  `where T : MonoService<T>`로 묶으면 컴파일 타임에 막을 수 있지만 T가 항상 자기 자신이어야 해
  **역할 인터페이스 등록이 불가능해진다**(`class Person : MonoService<IWalk>`).
  교체 가능성을 위해 만든 클래스라 그 쪽을 살렸다. 판단 근거는 파일 주석에 남겼다.

## 후속 작업 / 주의사항

- **인벤토리 캐시에 채취분이 반영되지 않는다.** `SessionManager.OnGatherResultReceived`에
  TODO로 남겼다. 이때 `ItemChanges.Count`는 **델타가 아니라 갱신 후 누적 총량**이라
  더하면 수량이 두 배가 된다(`PacketInfo.cs`의 `// 변경(델타) 전용` 주석과 실제가 어긋남 —
  서버 담당과 정리 필요).
- **로그인 시 인벤토리 스냅샷은 오프라인 정산 "전" 값**이다. 이어 오는 채취 결과로 보정해야 한다.
- MIKA001 경고 2건(`S_PongResponse`·`S_UpdateItemResponse`)은 **서버가 보내지 않는 미사용 패킷**이라
  의도적으로 남겼다. 새로 뜨는 경고는 진짜 누락이다.
- 실동작으로 확인 가능한 범위: **슬롯 0번 / 낚시(Fishing)뿐**. 슬롯 해금 경로가 없고
  드롭 테이블도 낚시만 등록돼 있다. `CharacterId`는 더미(서버가 `!= 0`만 검사).
- `MonoService` 변경은 **Arca_Unity_Toolkit** 템플릿에도 동일 반영했다(별도 커밋).
