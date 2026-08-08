---
date: 2026-08-03
title: 가챠 갱신 규칙 통일(#8) + 하트비트 클라 파트(#10) + 클라 로그·예외 보강
tags: [client, log, heartbeat, gacha, inventory]
---

# 클라 로그 체계 정리 · 하트비트 · 가챠 갱신 통일

## 목적 / 배경

- 깃허브 이슈 **#8** — 서버가 `S_GachaDrawResponse`에 `ItemChangeInfos`를 실어 주기로 한
  후속(→ `.claude/Agent/2026-08-02-gacha-item-change-notify.md`)을 클라가 받는 작업.
- 깃허브 이슈 **#10** — 좀비 유저. 서버가 끊김을 감지 못하는 문제의 **클라 절반**만 처리했다.
- 사용자 요청 — 클라 전반의 예외/경고 로그 보강, 로그 태그를 방향이 보이게 한글로.

## 변경 내용

- `Assets/Scripts_Client/Log/ClientLog.cs` (신규) — 태그 상수 + `Info/Warn/Error`,
  `MikaSessionPacketExtensions.Sent` 훅 등록으로 **송신 로그 신설**.
- `Assets/Scripts_Client/Managers/HeartbeatManager.cs` (신규) — 5초 Ping / 15초 무응답 판정.
  씬의 `Session Manager` 오브젝트에 부착(에디터에서 붙이고 씬 저장 완료).
- `SessionManager` — 가챠를 `ApplyItemChanges`로 통일하고 `AddGachaRewards` 삭제,
  로그인 무응답 감시 코루틴, 요청 전 로그인 가드(`CanSend`), 실패 응답 경고.
- `ServerPacketHandler` — `S_PongResponse` 핸들러·`PongReceived` 이벤트 추가, 수신 로그 9곳 한글화.
- `Services.Get<T>` — 미등록 시 타입명·원인을 담은 예외 메시지.
- UI 4종(`InventoryPanelUI`·`WorkStationPanelUI`·`StatePanelUI`·`WorkStationTestButtonUI`) —
  `RequireRef` 참조 검증 + 로그 태그 정리. `WorkStationTestButtonUI`는 드롭다운 인덱스 가드 추가.

## 주요 결정 / 근거

- **로거를 `Common/`에 두지 않았다.** `Common/`은 프로젝트를 옮겨도 그대로 쓰는 토대인데
  이 로거는 `PacketId`와 게임의 태그 약속을 안다. `Scripts_Client/Log/`로 분리했다.
- **참조 검증은 새로 만들지 않고 기존 `MonoBehaviourExtensions.RequireRef`를 썼다.**
  처음엔 `ClientLog.HasReferences`를 만들었다가, `WindowPanelUI`가 이미 쓰던 유틸을 발견하고 되돌렸다.
  같은 일을 하는 장치를 둘로 두면 규칙이 갈린다.
- **Ping/Pong은 평시에 로그를 남기지 않는다**(`ClientLog.QuietPacketIds`). 5초마다 왕복이라
  그대로 찍으면 콘솔이 덮인다. `HeartbeatManager`가 **상태가 바뀔 때만**(끊김·복구) `[연결]`로 남긴다.
- **하트비트가 소켓을 끊지는 않는다.** 세션은 `MikaClient`(서버 소유 폴더)의 것이라 손대지 않았다.
  좀비 세션을 실제로 정리하는 것은 서버 몫이다.

## 후속 작업 / 주의사항

- **이슈 #10의 서버 파트가 남아 있다** — 무응답 세션 정리, pid 중복 kick
  (`UserManager.cs:52-57`이 아직 `// Error Send` 주석 스텁), `Disconnect()` `Interlocked` 보강.
  클라 판정값은 5초/15초이고, 서버가 같은 값을 쓰면 그대로 맞물린다. → `일감/T-001`
- **`characterId` 기본값을 1 → 1001로 고쳤다.** TID 1은 폐기(영구 결번)라 서버가
  `CharacterNotOwned`로 거절한다. **씬에 이미 저장된 값은 그대로이므로** 배치 테스트 버튼이
  실패하면 인스펙터의 `characterId`부터 볼 것.
- `MIKA001` 경고 1건 남음 — `S_CharacterListResponse`에 클라 핸들러가 없다(서버가 커밋 `a44cdc0`으로
  추가한 패킷). 캐릭터 선택 UI를 만들 때 함께 처리한다.
- 서버 소유 파일이라 손대지 않은 것 — `MikaClient.cs:54`의 `"$Disconnected"` 오타,
  `NetworkManager.cs`·`MikaClientSession.cs`·`EchoTestButton.cs`의 영어 로그.
- **플레이 모드 검증 완료(업데이트 참조)** — 배치·해제 왕복이 `[↑송신]`/`[↓수신]` 짝으로 찍히고
  캐릭터 이름까지 정상 표시되는 것을 콘솔에서 확인했다.

## 업데이트 (2026-08-03) — 배치 거절의 진짜 원인은 TID/개체 번호 혼동이었다

배치가 `미보유 캐릭터`로 거절되던 원인은 설정 실수가 아니라 **클라가 TID를 보내고 있었기** 때문이다.

- 서버 `_characters`의 키는 **개체 PK**(`t_character.character_id`)다. DB 확인 결과
  `Uid=5`의 캐릭터는 **개체 2 / TID 1001** — 클라가 보낸 1001은 종류 번호라 조회에 걸리지 않는다.
- 그 개체 번호를 알려 주는 패킷이 `S_CharacterListResponse`인데 **클라에 핸들러가 없었다**
  (앞서 남긴 `MIKA001` 경고가 정확히 이것이었다. 경고가 곧 버그였다).
- 조치 — `ServerPacketHandler`에 핸들러 추가, `SessionManager`가 보유 캐릭터를 캐시하고
  `FirstCharacterId`·`GetCharacterName(개체번호)` 제공, 테스트 UI 두 곳이 인스펙터 값 대신 이걸 쓴다.
  인스펙터의 `characterId` 필드는 **삭제했다** — 계정마다 다른 값이라 박아 두면 반드시 어긋난다.
- 같이 고친 것: `WorkStationSlotView`가 개체 번호를 `GameDataLoader.GetCharacterName`(TID를 받는다)에
  넣고 있어 이름이 `?#2`로 나오던 기존 버그. 이제 패널이 변환해 넘긴다.

> ⚠️ **`GameDataLoader.GetCharacterName`은 TID를 받는다.** 개체 번호를 쓰려면
> `SessionManager.GetCharacterName`을 거친다. 둘 다 이름이 같아 헷갈리기 쉽다.

> ⚠️ `SessionManager`는 `using CharacterInfo = MikaProtocol.CharacterInfo;` 별칭이 필요하다 —
> `UnityEngine.CharacterInfo`(폰트 글리프)와 이름이 겹친다.
