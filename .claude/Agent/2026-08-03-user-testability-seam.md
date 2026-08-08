---
date: 2026-08-03
title: User를 채널·DB 큐 주입과 시각 인자로 테스트 가능하게 분리
tags: [server, refactor, test, workstation, architecture]
---

# `User` 테스트 seam 열기 — 아키텍처 리뷰 후보 2

## 목적 / 배경

- `User` partial 합계 574줄(파일 전체 692줄)인데 **테스트 0건.** 저장소 전체 91건이
  전부 이미 열려 있던 순수 코어(`WorkStationSlot`·`WorkSpeed`·`Inventory`·`GachaPoolCatalog`)에만
  붙어 있었고, **재화가 실제로 생성되는 경로(정산 → 지급 → 저장 → 푸시)는 커버리지 0%**였다.
- 2026-08-03 아키텍처 리뷰의 신규 버그 A·B·C가 전부 이 경로에 있고, 셋 다
  **응답은 `Ok`로 나가면서 상태만 어긋나는** 형태라 로그로 안 잡힌다.
- `User`는 2026-08-01에 441줄 → 08-03에 574줄. 테스트 0건인 채로 가장 작은 시점이 지금이었다.

## 방침 — `IClock`을 넣지 않기로 했다

리뷰 초안은 `IClock` 주입을 제안했으나 **철회했다.** 순수 코어에 `DateTime.UtcNow`가
**0건**이고 전부 인자로 받는다(`WorkStationSlot.Assign(.., now)`·`ConsumeJudgeCount(now)`·
`WorkStation.Settle(now, catalog)`). `WorkStationSlotTest` 20여 건이 목 없이 도는 이유가 이것이다.
**시계를 필드로 들면 같은 문제에 두 번째 관례가 생긴다.**

`UtcNow` 6곳 중 5곳은 인자로 뚫었고, 인자를 못 받는 `OnDestroy`(base 시그니처 고정)만
`Disconnect(DateTime now)`로 한 겹 갈라 배선만 남겼다.

## 실제로 막고 있던 것 3가지

| # | 막는 것 | 해법 |
|---|---------|------|
| 1 | `internal User(...)` + `InternalsVisibleTo` 부재 | csproj에 `InternalsVisibleTo` |
| 2 | `SendPacket`이 **확장 메서드(static)** 라 `Mock<ISession>`이 못 잡음 | `IClientChannel` |
| 3 | `DBManager.Instance` 싱글턴 + 스레드 2개 비동기 왕복 | `IDBQueue` |

**#2가 이번에 새로 확인한 지점이다.** 확장 메서드는 객체를 거치지 않아 Moq 프록시가
가로챌 자리가 없다. 목이 볼 수 있는 건 `Send(ReadOnlyMemory<byte>)`로 오는 프레임된
바이트뿐인데, `MikaGenerated.GeneratedPacketIds`는 **`Get<T>()`(타입→id) 단방향만** 생성한다
— id→타입 역매핑이 없다. 그래서 `Login()`이 보내는 5개 패킷의 **순서**
("캐릭터를 슬롯보다 먼저" — `User.cs`에 명시된 규칙)를 검증하려면 테스트가 id→타입 표를
손으로 유지해야 하고, 그 표가 곧 새 드리프트 원천이 된다.

## 변경 내용

### 신규

- `Server/WSGameServer/Common/IClientChannel.cs` — `SessionId`·`IsConnected`·`Send<T>` 셋만.
  `User`가 `ISession`에서 실제로 쓰던 멤버가 정확히 이 셋이었다.
  운영 구현 `SessionClientChannel`이 `ISession`을 감싸고, **직렬화·프레이밍은 여기 안에서만** 일어난다.
- `Server/WSGameServer/DB/IDBQueue.cs` — `DBManager`가 구현.

### 수정

- `User/User.cs` — 생성자가 `(IClientChannel, IDBQueue, pid, nickname, loggedInAt, DropTableCatalog?)`.
  `Session` 프로퍼티 제거, `Send`/`PostDBTask`가 주입분을 쓴다.
  `OnDestroy` → `Disconnect(DateTime now)` 위임.
- `User/User.WorkStation.cs` — `AssignWorkStation(.., DateTime now)` ·
  `RefreshWorkStationSpeed(DateTime now, bool notify)` · `SettleWorkStation`이 `_dropTables`를 넘김.
- `User/User.DB.cs` — `OnLoginDataLoaded(data, now)` · `FinishLogin(now)` ·
  `OnDefaultCharacterGranted(id, now)`.
- `User/UserManager.cs` — 조립 지점. `user.Session.SessionId` → 기존 `user.SessionId` 프로퍼티.
- `Common/GatheringScheduler.cs` — `private static Tick()` → `public static Tick(now, users)`.
  시각·대상 목록은 타이머 콜백이 만든다.
- `Repository/{Login,GrantCharacter}Repository.cs` — `Apply()`에서 `UtcNow` 생성.
- `Network/ClientPacketHandler.cs` — `AssignWorkStation(.., DateTime.UtcNow)`.
- `WSGameServer.csproj` — `InternalsVisibleTo Include="WSGameServer.Tests"`.

### 테스트 (신규 17건, 91 → 108)

- `WSGameServer.Tests/User/TestUserBuilder.cs` — `FakeClientChannel`(패킷을 객체 그대로 기록) ·
  `FakeDBQueue`(Repository를 실행 않고 기록) · `GameTableFixture`(프로세스당 `LoadAll` 1회).
- `WSGameServer.Tests/User/UserWorkStationTest.cs` — 배치 거절 3종(코드 구분·저장 안 함·상태 불변),
  배치 성공(Ok·스냅샷·저장), 정산(지급·1회 저장·미달 시 무동작·슬롯별 분리),
  `notify:false`(지급하되 푸시 없음), `Disconnect`, `GatheringScheduler.Tick`.

## 주요 결정 / 근거

- **`IClock` 대신 인자 관통.** 위 방침 참조. 인터페이스 하나를 아꼈다기보다,
  이미 있는 관례를 두 개로 쪼개지 않은 것이 요점이다.
- **`ISession` 전체가 아니라 셋만 잘랐다.** 실제 사용처를 세어 보고 정한 경계라
  발명한 추상화가 아니다. 부수 효과로 `User`가 전송 계층을 모르게 됐다.
- **`IDBQueue`는 커넥션이 아니라 큐를 자른다.** `DBManager.Initialize(Func<SqliteConnection>)`가
  이미 있지만 그건 DB **안쪽**을 갈아끼우는 층이다. SQL 정합성은 Repository 테스트가
  `:memory:`로 보고, "저장 요청이 나갔는가"는 이쪽에서 본다.
- **드롭 카탈로그도 생성자로 주입(기본값 = 전역).** `WorkStation.Settle(now, catalog?)`가
  이미 같은 규약이라 맞췄다. 덕분에 정산 테스트가 후보 1종짜리 테이블로 돌아
  판정 수와 획득 수를 그대로 대조한다.
- **`GrantCharacterRepository.Apply`는 로그인 시각을 끌고 가지 않고 새로 만든다.**
  DB 왕복을 건너므로 캡처한 값은 낡는다.
- **버그 A(`(ItemType)200`이 `CharacterId == 0` 경로로 DB에 영속화)는 고치지 않았다.**
  리팩터링과 동작 변경을 한 커밋에 섞지 않는다. seam이 열렸으니 다음 작업에서
  실패 테스트로 재현 후 고친다.

## 검증

```
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
→ 통과!  실패: 0, 통과: 108
```

**red 확인을 했다.** `AssignWorkStation`의 정산/`ApplyWorkSpeed` 순서를 일부러 뒤집어
돌렸더니 `배치를_바꾸기_전에_먼저_정산한다` 1건만 정확히 실패했고, 되돌린 뒤 108건 통과.
이 테스트는 양방향 실패를 다 잡는다 — 정산이 `Assign` 뒤로 가면 완성된 판정이
**통째로 사라지고**(0회), `ApplyWorkSpeed`가 정산 앞으로 가면 **재화가 샌다**(20회 초과).

## 남은 것 / 다음

- **`Login()` 전체 흐름 테스트는 못 한다.** `Entity.Create()`/`Destroy()`가
  `LogicExecutor.Instance.Post`로 던지는 비동기라 `OnCreate`가 그 자리에서 돌지 않는다.
  실행기와 `UserManager.Instance`까지 열어야 가능하다 — 다음 판.
- 후보 1(드롭 테이블 산업 레벨 축, 현재 드롭의 81.1%가 미해금 레벨)이 이 seam 위에서
  테스트로 고정하며 짜기 좋아졌다. 리뷰가 권한 순서가 이것이었다.

## 참고

- `GameDesign/아키텍처리뷰/2026-08-03-아키텍처리뷰.md` 후보 2 · 신규 발견 A·B·C
- 빌드 잠금: 실행 중인 `WSGameServer.exe`가 `MikaUtils.dll` 복사를 막아 MSB3021이 났다.
  프로세스를 종료해야 `dotnet test`가 시작된다(Unity 에디터는 분석기 DLL만 막는다).
