---
date: 2026-08-04
title: 하트비트 무응답 세션 정리 · 중복 로그인 kick — 이슈 #10 / T-001
tags: [server, network, heartbeat, session, test, bugfix]
---

# 좀비 유저 정리 (이슈 #10 · 일감 T-001)

## 목적 / 배경

FIN/RST 없이 사라지는 종료(Unity 에디터 플레이 중지·절전·랜선 뽑기)에서는 `ReceiveAsync`가
영원히 대기하고 `IsConnected`가 참으로 남는다. `GatheringScheduler`가 매초 그 User를 정산해
**폐지한 오프라인 적립이 되살아난다.**

클라이언트는 이미 5초마다 `C_PingRequest`를 보내고 있었다. **서버가 받아서 끊기만 하면 됐다.**

## 확정한 값 (기획 — 작업슬롯 3.3)

| 항목 | 값 | 근거 |
| --- | --- | --- |
| 하트비트 주기 | **5초** | 클라 선행 구현 |
| 무응답 판정 | **15초** | **채취 기준 주기 30초보다 짧다** — 부당 적립 구간이 판정 1회를 못 채운다 |
| 재접속 유예 | **없음** | 작업슬롯 3.1과 같은 규칙(끊기면 조각 폐기) |

15초를 고른 이유가 "적당해서"가 아니라 **30초보다 짧아서**라는 게 요점이다. 아이템이
한 개도 새지 않는 상한이 여기서 나온다. 이 관계를 테스트로 잠갔다
(`판정_시간은_채취_한_주기보다_짧다`).

## 변경 내용

### 신규

- `MikaNetwork.Server/IHeartbeatSession.cs` — `SessionId`·`IsConnected`·`LastReceivedAt`·`Disconnect()`.
- `WSGameServer/Common/SessionWatchdog.cs` — 5초 타이머 → 로직 스레드에서 스윕.

### 수정

- `MikaServerSession` — `LastReceivedAt`(수신 시 갱신) · `Disconnect()`를 `Interlocked.Exchange`로 멱등화.
- `MikaServer` — `SweepIdle(now, timeout)` + 본체 `static DisconnectIdle(sessions, now, timeout)`.
- `Entity.Destroy()` — 멱등 가드. `bool Destroy()`로 바꿔 실제 예약 여부를 돌려준다. `IsDestroyed` 추가.
- `UserManager.CreateUser` — pid 중복 시 kick. `LeaveUser` — 값 일치 제거.
- `IClientChannel.Disconnect()` + `User.CloseChannel()` — `Destroy`는 게임 상태만 정리한다.
- `MikaProtocol/PacketEnum.cs` — `EResultCode.AlreadyLoggedIn = 2`.
- `Global.SessionIdleTimeout = 15초`.
- 중복 `using` 정리(`DBManager`·`ClientPacketHandler`·`NetworkManager`) — CS0105 경고 0으로.

### 테스트 (신규 11건, 108 → 119)

- `Tests/Network/SessionIdleSweepTest.cs` 6건 — 경계(14초/15초)·이미 끊긴 세션·선별·빈 목록·
  판정시간 < 채취주기.
- `Tests/User/UserManagerTest.cs` 5건 — `Destroy` 멱등 2건, 매핑 정합성 3건.

## 주요 결정 / 근거

- **`ISession`에 `LastReceivedAt`을 얹지 않았다.** `Assets/Scripts_Server/Network/MikaNetwork.Core`에
  **수동 사본**이 있다(`sync-protocol-to-unity.ps1`은 `MikaProtocol`만 미러한다 — Core는 주석으로
  "추가하면 된다"만 적혀 있다). 손대면 두 벌이 갈라진다. 게다가 유휴 판정은 서버만의 관심사다.
  `IHeartbeatSession`을 `MikaNetwork.Server`에 두어 Unity와 무관하게 했다.
- **판정 로직이 소켓을 요구하지 않게 잘랐다.** `DisconnectIdle`이 `IEnumerable<IHeartbeatSession>`을
  받으므로 `Mock<IHeartbeatSession>`으로 전부 검증된다. 실제 연결 없이 경계값을 본다.
- **주기·임계값을 Lib이 정하지 않는다.** "얼마나 빨리 끊어야 하는가"는 게임 규칙(부당 적립 구간)이
  정하므로 호스트가 넘긴다. `MikaNetwork.Lib`이 로그 정책을 훅으로만 뚫어 두는 것과 같은 이유다.
- **수신만 본다.** `Send`는 큐 적재(1024)라 소켓이 죽어도 성공하고, `Sent` 로그 훅도 적재 직후
  찍힌다 — 송신은 살아 있음의 증거가 못 된다. 받은 내용도 보지 않는다(하트비트든 일반 요청이든 동일).
- **`SessionWatchdog`를 `GatheringScheduler`에 합치지 않았다.** 관심사가 다르고 주기도 다르다(5초 vs 1초).
  "채취 스케줄러가 세션을 끊는다"는 읽히지 않는다.
- **중복 로그인은 새 접속을 신뢰한다.** 기존을 남기고 새 접속을 거절하는 선택도 가능하지만,
  "끊긴 걸 서버가 아직 모르는" 경우가 압도적이다. 조용히 return하던 기존 동작은
  정상 재접속이 응답조차 없이 막히는 것이라(이슈 #10 증상 3) 가장 나쁜 선택이었다.

## 덤으로 잡은 것 — 아키텍처 리뷰 신규 발견 C

`_pids` 오염 경합을 함께 고쳤다. pid 중복 검사가 보는 `_pids`는 DB 왕복 2회 뒤에야 채워지므로,
좀비의 `LeaveUser`가 새 유저의 `JoinUser`보다 늦게 돌면 **살아 있는 세션의 매핑을 지운다.**

두 방향으로 막았다.

1. `CreateUser`가 좀비를 발견하면 **그 자리에서 동기적으로** pid 자리를 비운다
   (`Destroy`는 큐에 넣을 뿐이라 늦다).
2. `LeaveUser`가 **값까지 일치할 때만** 제거한다(`TryRemove(KeyValuePair)`).

## 검증

```
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
→ 통과!  실패: 0, 통과: 119
```

**red 확인.** `LeaveUser`를 키만 보고 지우도록 되돌렸더니
`좀비가_늦게_나가도_새_유저를_밀어내지_않는다` 1건만 정확히 실패했고, 되돌린 뒤 119건 통과.

문서 그래프: `check-doc-graph.ps1 -Changed` → 그래프 정합성 OK.
갱신일 역전 경고는 하트비트를 서술하지 않는 문서들이라 전파 대상이 아니다
(하트비트 언급 문서는 게임기획코어·작업슬롯·자원채취·리서치 넷뿐이며, 앞 셋을 갱신했다).

## 남은 것 / 다음

- **실기 재현 확인이 남았다.** Unity 플레이 중지 후 15초 안에 세션이 정리되고
  그 뒤 송신 로그가 멎는지. ⚠️ `taskkill`은 OS가 RST를 보내 재현되지 않는다.
- 이슈 #10의 보강 4번(죽은 세션 송신을 로그에서 구분)은 하지 않았다.
  세션이 15초 안에 정리되므로 증상이 사라진다 — 필요해지면 그때 한다.
- `Login()` 전체 흐름 테스트는 여전히 불가(`Entity.Create()`가 `LogicExecutor`로 던지는 비동기).
  중복 로그인 kick의 **통합** 검증이 여기에 걸려 있어, 지금은 `UserManager` 매핑 수준까지만 잠갔다.
