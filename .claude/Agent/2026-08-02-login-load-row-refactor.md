---
date: 2026-08-02
title: 로그인 Load 경로를 Row 기반으로 리팩토링
tags: [server, repository, login, refactor]
---

# 로그인 Load 경로를 Row 기반으로 리팩토링

선행 문서: [아키텍처 리뷰](../../GameDesign/아키텍처리뷰/2026-08-01-서버아키텍처리뷰.md) 후보 2의 **Load 부분만** 실행.
사용자가 namespace 평탄화·`DbConnection` 래퍼·`RepositoryContracts.cs`를 먼저 만들어 두었고, 그 WIP를 이어받아 완성했다.

## 변경 내용

**계약** — `Repository/RepositoryContracts.cs`
- 공개 Row 4종: `InventoryRow` · `CurrencyRow` · `CharacterRow(long CharacterId, int CharacterTid, ...)` · `WorkStationSlotRow(..., long CharacterId)`
- `PlayerLoginData` — 리포지토리가 로직 스레드로 넘기는 Row 묶음. **Row는 User의 partial까지만 들어간다.**

**DB** — `DB/DbConnection.cs` (Dapper 래퍼) · `DB/DBManager.cs`
- 래퍼 메서드 4개: `QueryAsync`(List 반환) / `QueryFirstOrDefaultAsync` / `ExecuteAsync` / `ExecuteScalarAsync`
- `IRepository.ExecuteAsync(DbConnection)` — `IDbConnection`은 DBManager만 안다
- `DBManager.Initialize(Func<SqliteConnection>)` 오버로드 추가 — 테스트가 `:memory:`를 넣는 seam. 파일명 오버로드는 유지(경로 탐색 동일)

**로그인 흐름**
- `LoginRepository.ExecuteAsync` = **Row 수집 + 신규 지급 INSERT까지만** (동작 보존). 도메인 변환·`startedAt` 결정이 DB 스레드에서 빠졌다
- `Apply()` → `User.OnLoginDataLoaded(PlayerLoginData)` 호출만
- `User.OnLoginDataLoaded`(구 `LoadDB` 대체): `startedAt = UtcNow`를 **로직 스레드가** 결정 → `LoadInventory` / `LoadCurrencies` / `LoadCharacters` / `LoadWorkStation` → `RefreshWorkStationSpeed` → `Login()`
- 영역별 Load는 각 partial에 산다: `User.Inventory.cs` `User.Currency.cs` `User.Character.cs`(GameTable 스킵 정책 포함) `User.WorkStation.cs`
- `Inventory.Load`가 `ItemInfo`(Protocol DTO) 대신 `Item`(도메인)을 받는다 — DTO는 `Snapshot()`(패킷 조립)에서만 등장

## 주요 결정 / 근거

- **Row는 프로퍼티 record + 프로퍼티 이름 = DB 컬럼명(snake_case).** (2026-08-02 사용자 결정으로 최종 확정)
  - 처음엔 위치 기반 record + SQL `AS` 별칭이었으나 **두 번 뒤집었다**:
    ① Dapper의 `MatchNamesWithUnderscores`는 생성자 매핑에 적용되지 않고, **SQLite는 INTEGER를
    전부 long으로 돌려줘** 위치 기반 record의 int 파라미터가 타입 불일치로 거부된다(런타임 예외).
    → 프로퍼티 record로 전환. ② 이후 옵션 자체를 끄고 프로퍼티를 컬럼명 그대로 맞췄다 —
    Row 타입에 한해 PascalCase 관례의 예외. `AccountRepository`의 private record도 함께 전환.
  - `DefaultTypeMap.MatchNamesWithUnderscores`는 **꺼져 있다**(Program.cs에서 제거). 새 Row를 만들 때
    컬럼명과 한 글자라도 다르면 매핑이 조용히 빈다(기본값 0/null) — RepositoryContracts.cs 상단 주석 참조.
- **신규 지급(기본 캐릭터·기본 슬롯)은 아직 `LoginRepository.ExecuteAsync`에 남겼다.** 로직 스레드로 옮기면 쓰기 왕복이 1회 늘어나는 구조 변경이라 "Load만" 범위를 벗어난다. 후보 2 후속에서 옮긴다.
- **Row는 순수 코어에 넣지 않는다.** `WorkStation.Load`·`Inventory.Load`는 도메인 객체를 받는다 — 코어가 Repository 타입을 참조하면 의존 방향이 뒤집히고 기존 테스트(342줄)가 오염된다.
- 사용자 WIP의 namespace 평탄화 잔재(`User.User`·`Character.Character`·`Slot` 별칭·`GlobalUsing`)를 함께 정리했다. 서버 스킬 문서의 예시 코드가 옛 namespace를 쓰고 있는지는 확인하지 않았다.

## 검증

- `dotnet build WSGameServer` 오류 0
- `dotnet test` **83/83 통과** (기존 테스트 변경 없음 — 순수 코어 시그니처가 안 바뀌었다는 증거)
- 런타임 로그인 경로는 실서버로 검증하지 않았다 — 더미 클라 로그인 확인 권장

## 업데이트 (2026-08-02) — 신규 유저 지급을 로직 스레드로 이동

사용자 결정: **"Login은 지급 후에 보낸다."** `LoginRepository`가 완전한 읽기 전용이 됐다.

- `GrantCharacterRepository` 신설 — INSERT + `RETURNING character_id`, `Apply`가
  `User.OnDefaultCharacterGranted(id)`로 개체 PK를 돌려준다.
- `OnLoginDataLoaded`: 캐릭터 0이면 지급을 Post하고 **return** — 지급 완료 콜백이
  `FinishLogin()`(속도 확정 + `Login()`)을 이어간다. 같은 세션 키 파티션이라 순서가 보장된다.
- 기본 슬롯은 대기 없이 처리 — `WorkStation.Unlock`(메모리) + `SaveWorkStationSlots`(비동기 저장).
  프로덕션에서 `Unlock`을 쓰는 첫 호출자가 생겼다.
- `DefaultSlotCount`가 `LoginRepository`에서 `User.WorkStation.cs`(public const)로 이동 —
  신규 유저 정책 상수 2개가 전부 User에 모였다.
- 빌드 오류 0 · 테스트 83/83. 신규 유저 실제 로그인은 더미 클라 검증 필요(아래).

## 후속 작업 / 주의사항

- **신규 유저 로그인 경로를 더미 클라로 검증할 것** — DB를 비우거나 새 pid로 접속해
  기본 캐릭터·슬롯 지급 + S_LoginResponse 순서를 확인한다.
- 지급 INSERT가 실패하면 `Apply` 미도달로 로그인이 영영 안 끝난다 — 기존 DB 침묵 실패
  문제(아키텍처 리뷰 부수 발견 #2)와 같은 뿌리. Executor `OnError` 훅과 함께 풀어야 한다.
- `AddItemRepository.Key`가 여전히 0 (파티션 붕괴 버그) — 이번 범위에서 제외했다. **한 줄 수정**(`public long Key => User.SessionId;`)이니 다음 작업에서 반드시.
- 밴/삭제 응답 미전송, `Apply()`의 침묵 실패 등 아키텍처 리뷰 부수 발견 목록은 그대로 남아 있다.
- `DbConnection`이라는 이름이 `System.Data.Common.DbConnection`과 겹친다. WSGameServer namespace 안에서는 우리 타입이 이기지만, `System.Data.Common`을 using하는 파일에서는 혼동 여지가 있다.
