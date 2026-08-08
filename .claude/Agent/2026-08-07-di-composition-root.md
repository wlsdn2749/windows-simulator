---
date: 2026-08-07
title: 싱글턴을 걷어내고 GameServer를 조립 지점으로 세운다 (진행 중)
tags: [server, refactor, architecture, di, executor]
---

# 싱글턴 제거 · `GameServer` 조립 지점 — 아키텍처 리뷰 후보 4 + 후보 2의 꼬리

> ⚠️ 본문은 리팩터링 커밋(`6c6e6e8`) 시점의 기록이다. **테스트 복구는 아래 [업데이트](#업데이트-2026-08-07--테스트-복구)를 본다.**

## 목적 / 배경

- [2026-08-03 로그](2026-08-03-user-testability-seam.md)의 마지막 줄이 예고한 **"다음 판"** 이다.
  거기서 `IClientChannel`·`IDBQueue`로 `User`를 열었지만 `Login()` 전체 흐름은 여전히 테스트 불가였다 —
  `Entity.Create()`/`Destroy()`가 `LogicExecutor.Instance.Post`로 던지는 비동기라 `OnCreate`가 그 자리에서 돌지 않는다.
  **실행기와 매니저 싱글턴을 열지 않으면 그 위는 못 연다.**
- 아키텍처 리뷰([2026-08-03](../../GameDesign/아키텍처리뷰/2026-08-03-아키텍처리뷰.md) 후보 6 →
  [2026-08-01](../../GameDesign/아키텍처리뷰/2026-08-01-서버아키텍처리뷰.md) **후보 4**)가 지적한
  두 가지를 함께 친다.
  - `Entity.Post`가 `LogicExecutor.Instance`에 못 박혀 **어떤 Entity 생명주기도 동기 검증이 안 된다**.
  - "프레임워크는 게임을 모른다"는 경계 규칙(CLAUDE.md)을 `MikaExecutor.cs`가 스스로 어긴다.
- 지표: `*.Instance` 직접 참조가 08-01 15곳 → 08-03 **21곳**으로 늘고 있었다(리뷰 1장 "후퇴한 것").

## 변경 내용

### 신규 — `Server/WSGameServer/GameServer.cs`

전역 싱글턴 대신 **한 곳에서 생성하고 서로에게 주입하는 조립 지점(composition root)** 을 세웠다.

```
LogicExecutor ──┬─→ SessionWatchdog ──→ NetworkManager
                ├─→ GatheringScheduler
                └─→ DBManager ─────────→ UserManager.Initialize(...)
```

`Program.Main`은 20줄 초기화 블록을 통째로 넘기고 `new GameServer() → Initialize() → Run()` 3줄만 남았다.

### 프레임워크(`MikaNetwork.Lib`)에 seam 인터페이스

- `MikaExecutor.cs` — 빈 껍데기 `MikaExecutor` 클래스를 **`ILogicExecutor`**(`Start`/`Stop`/`Post`)로 대체.
  `LogicExecutor`가 `Singleton<LogicExecutor>` 상속을 떼고 이 인터페이스를 구현한다.
- `MikaServer.cs` — **`IServer`** 추가(`Connected`/`Disconnected`/`EndPoint`/`Listen`).

### 탈싱글턴 4종

`GatheringScheduler` · `SessionWatchdog` · `DBManager` · `NetworkManager` 전부
`Singleton<T>` 상속을 떼고 `ILogicExecutor`를 생성자로 받는다.
내부의 `LogicExecutor.Instance.Post(...)` → `_logicExecutor.Post(...)`.

- `SessionWatchdog` — `ISessionWatchdog` 인터페이스 추가, `Sweep`이 `private` → `public`(테스트 진입점).
- `UserManager`만 싱글턴을 **유지**하되 `Initialize(DBManager, ILogicExecutor)`로 의존성을 받아 둔다.

### `Entity` 폐기 → `User`로 인라인

`Common/Entity.cs`(55줄)를 삭제하고 `Create`/`Destroy`/`IsDestroyed`/`Key`/`Post`를 `User`가 직접 갖는다.
`OnCreate`/`OnDestroy`는 `protected override` → `public`.
`User` 생성자에 `ILogicExecutor`가 3번째 인자로 들어갔다.

## 주요 결정 / 근거

- **`Entity`는 상속으로 얻는 게 없었다.** 구현체가 `User` 하나뿐인데
  `Post`가 `LogicExecutor.Instance`에 묶여 있어서 **주입을 받으려면 base 생성자에 실행기를 뚫어야 했다.**
  그 시점에 base가 하는 일은 "필드 2개 + 가드 2개"뿐이라, 계층을 남기는 값보다 없애는 값이 컸다.
  멱등 가드(`Interlocked.Exchange`, 리뷰 버그 #4 대응)는 그대로 옮겼다.
- **리뷰 후보 4의 "Executor를 Lib에서 꺼낸다"는 아직 안 했다.** 물리적 이동 대신
  **인터페이스만 먼저 뽑았다** — 호출부가 전부 `ILogicExecutor`를 보게 되면 클래스가
  어느 프로젝트에 있든 다음에 옮기는 비용이 거의 0이 된다. 이번 변경 반경을 줄이려는 선택이다.
- **`UserManager`만 싱글턴으로 남겼다.** `SessionUserExtensions`·`ClientPacketHandler`·`User` 자신이
  정적으로 참조하고 있어 같이 열면 반경이 패킷 핸들러 전체로 번진다. 다음 판으로 미룬다.
- 결과: `*.Instance` **21곳 → 14곳**(잔여: `UserManager` 5 · `DropTableCatalog` 3 ·
  `DBExecutor` 2 · `GachaPoolCatalog` 2 · `GachaService` 1 · 조립 지점 1).

## 남은 것 / 주의사항

**🔴 지금 상태로는 테스트가 컴파일되지 않는다.** 커밋 전에 셋을 고쳐야 한다.

| 위치 | 문제 |
|---|---|
| `WSGameServer.Tests/User/UserManagerTest.cs:17` | `private sealed class Dummy : Entity` — `Entity`가 삭제돼 컴파일 실패. 멱등 가드 테스트라 `User`로 옮기거나 폐기 판단이 필요하다 |
| `WSGameServer.Tests/User/TestUserBuilder.cs:113`, `UserManagerTest.cs:52` | `new User(...)`에 `ILogicExecutor` 인자 누락. **인라인 실행기 가짜(Post = 즉시 실행)를 여기 넣으면 08-03 로그가 못 했던 `Login()` 전체 흐름 테스트가 열린다** — 이번 작업의 실제 목적지다 |
| `Program.cs:12` | `gameServer.Run()`이 `async Task`인데 await 없음(CS4014). `Task.Run` 안 예외가 조용히 삼켜지고, 초기화 완료 전에 "10050 포트에서 대기 중" 로그가 찍힐 수 있다. 그리고 **예외 삼킴은 리뷰 버그 #2와 같은 종류**다 — 여기서 다시 만들지 않는다 |

### 인터페이스를 뽑아 놓고 안 쓰는 자리

- `NetworkManager` 생성자가 `ISessionWatchdog`이 아니라 구체 `SessionWatchdog`을 받는다.
- `IServer`를 만들었지만 `NetworkManager._server`는 `new MikaServer(10050)`을 직접 들고 있다(포트도 하드코딩 그대로).
- `ISessionWatchdog.Start(MikaServer)`가 시그니처에 구체 클래스를 노출한다 — `IServer`로 받는 게 일관된다.

> **어댑터 1개 = 가설상의 seam, 2개 = 진짜 seam**(리뷰 용어표). 위 셋은 아직 어댑터가 1개다.
> 테스트 대역이 두 번째 어댑터로 붙어야 이번 인터페이스들이 값을 낸다.

### 그 외

- `GatheringScheduler.Start`는 여전히 `UserManager.Instance.All`을 직접 본다 — 전역 의존이 하나 남았다.
- `Assets/Plugins/Analyzers/MikaSourceGen.dll` 변경분은 빌드 산출물 재복사(크기 동일)다. 내용 변경 없음.

## 참고

- `GameDesign/아키텍처리뷰/2026-08-01-서버아키텍처리뷰.md` — 후보 4(Executor·`Entity.Post`), 버그 #2·#4
- `GameDesign/아키텍처리뷰/2026-08-03-아키텍처리뷰.md` — 1장 지표(`*.Instance` 21곳), 후보 6
- `.claude/Agent/2026-08-03-user-testability-seam.md` — 이 작업의 직전 판

---

## 업데이트 (2026-08-07) — 테스트 복구

**119건 전부 통과.** 실제로 깨진 건 **한 곳뿐이었다** — `Dummy : Entity`(CS0246).
본문에서 "인자 누락으로 컴파일 실패"로 적은 `new User(...)` 2곳은 오진이었다.
C# **비후행 명명 인수**(non-trailing named arguments) 규칙 덕에 `pid:` 이후가
전부 이름으로 붙어 있어 그대로 통과한다. 인자는 그래도 명시적으로 넣었다(가독성).

### 신규 — `WSGameServer.Tests/Common/FakeLogicExecutor.cs`

**모드가 둘인 게 핵심이다.** 어느 쪽을 쓸지는 *검증 대상이 예약이냐 결과냐* 로 갈린다.

| 모드 | 쓰는 곳 |
|---|---|
| **기록**(기본) — 큐에 쌓기만 | "몇 번 예약됐는가". **작업이 실제로 돌면 전역을 만지는 경우도 이쪽** |
| **즉시 실행** — `Post`가 그 자리에서 실행 | `Create()` 이후 흐름 전체 |

`Drain()`은 실행 중 새로 예약된 작업도 이어서 돈다 — 단일 스레드 실행기와 같은 동작.

### 🔴 밟은 지뢰 — `Destroy()`를 즉시 실행 모드로 돌리면 안 된다

`OnDestroy` → `Disconnect` → **`UserManager.Instance.LeaveUser`**(`User.cs:173`)가
프로세스 전역 싱글턴을 만진다. xUnit은 테스트 클래스를 병렬로 돌리므로 **다른 테스트로 샌다.**
그래서 멱등 가드 테스트는 기록 모드로 두고 **"큐에 한 번만 실렸는가"** 를 센다.
`TestUserBuilder.WithInlineExecutor()` XML 주석에 이 금지를 박아 뒀다.

### `Dummy : Entity` 2건 → 진짜 `User` 위로

되살리지 않고 올렸다. 예전 테스트는 `Destroy()`의 **반환값**만 봤는데,
실제 계약은 *"OnDestroy가 한 번만 큐에 실린다"* 다. 이제 그걸 직접 단언한다.

### red 확인 — 두 번 했다

1. 가드를 통째로 제거 → `Destroy().ShouldBeFalse()`에서 먼저 걸렸다.
   **새로 넣은 `Posted.Count` 줄이 값을 하는지 증명되지 않는다.**
2. 그래서 **반환값은 맞는데 예약만 새는 형태**로 다시 뚫었다(`Post`를 가드 앞으로).
   → `builder.Executor.Posted.Count`만 정확히 실패. 그 줄이 독립적으로 잡는다.

> 단언 한 줄을 추가할 때는 **그 줄만 잡는 실패**를 따로 만들어 본다. 안 그러면
> 앞 단언에 가려 죽은 줄인지 알 수 없다.

### 정정 — `dotnet`은 있다

본문의 "환경에 `dotnet`이 없다"는 틀렸다. **PATH에 없을 뿐 `~/.dotnet/dotnet`에 SDK 10.0.203**이 있다.

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$DOTNET_ROOT:$PATH
dotnet test Server/WSGameServer.Tests/WSGameServer.Tests.csproj
→ Passed!  Failed: 0, Passed: 119
```

빌드 중 `MikaProtocol.csproj`의 post-build가 `powershell` 부재로 MSB3073(경고)을 낸다 —
**macOS에서는 Unity 미러링이 안 돈다.** 빌드·테스트 자체는 영향 없다.

### 아직 안 한 것 (본문 "남은 것"에서 이월)

- **`Program.cs:12`의 `Run()` await 누락**(CS4014) — 예외가 조용히 삼켜진다.
  리뷰 버그 #2와 같은 종류를 새로 만드는 자리다.
- `Create()`/`Login()` 흐름 테스트와 `SessionWatchdog.Sweep` 테스트 → **일감 [T-020](../../일감/T-020-로그인생명주기테스트.md)** 으로 등록.
  즉시 실행 모드는 준비됐지만 `Login()`·`Destroy()`가 전역 `UserManager.Instance`를 만져
  **처리 방침을 먼저 골라야 한다** — 일감에 선택지 3개를 적어 뒀다.
- `ISessionWatchdog`·`IServer`는 어댑터가 운영 구현 1개뿐 — 아직 가설상의 seam.
