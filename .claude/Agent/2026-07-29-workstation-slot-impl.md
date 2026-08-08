---
date: 2026-07-29
title: 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시
tags: [server, workstation, gathering, packet, db, test]
---

# 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시

선행 문서: [작업슬롯 구조 전환](2026-07-29-workstation-slot-design.md) · [WeightedPicker](2026-07-29-weighted-picker.md)

## 목적 / 배경

기획 전환(슬롯 구조)을 서버 코드로 옮겼다. 사용자 확정 사항:

- 슬롯당 캐릭터 1명 · 실시간 푸시 필요 · 요일 폐지
- **회당 산출 1개 고정 · 효율배수 신경 쓰지 않음** (캐릭터 시스템 미작성이므로)

## 변경 내용

**DB** — `t_workstation_slot` 생성 (STRICT, `game.sqlite3`에 적용 완료)

```sql
CREATE TABLE t_workstation_slot (
    user_id      INTEGER NOT NULL,                            -- 소유 유저 (t_user.user_id 참조)
    slot_index   INTEGER NOT NULL,                            -- 슬롯 번호 (0부터). 해금된 슬롯만 행으로 존재한다
    industry     INTEGER NOT NULL DEFAULT 0,                  -- 지정 산업 = GameData.ItemType (0=미지정)
    character_id INTEGER NOT NULL DEFAULT 0,                  -- 배치된 캐릭터 (0=비어 있음 → 채취하지 않음)
    last_tick_at TEXT    NOT NULL DEFAULT (datetime('now')),  -- 마지막 정산 시각 (UTC). 진행도의 단일 원본
    PRIMARY KEY (user_id, slot_index)                         -- (유저, 슬롯) 유일 = UPSERT 타깃
) STRICT;
```

**인덱스는 추가하지 않았다.** 모든 조회가 `user_id`로 시작하는데 복합 PK의 선두 컬럼이라
이미 인덱스가 걸려 있다. 별도로 만들면 중복이다.

**패킷** (`MikaProtocol`, PacketId 12~15)

| 패킷 | 방향 | 용도 |
| --- | --- | --- |
| `C_WorkStationAssignRequest` | C→S | 슬롯에 산업·캐릭터 배치 |
| `S_WorkStationAssignResponse` | S→C | 배치 결과 |
| `S_WorkStationSlotsResponse` | S→C | 로그인 시 슬롯 전체 |
| `S_GatherResultResponse` | S→C | **채취 결과 푸시**(요청 없이 서버가 밀어 줌) |

`WorkStationSlotInfo`는 `PacketInfo.cs`에 뒀다. 더미 클라이언트에 수신 핸들러 3개도 추가.

**모델·로직** (`WSGameServer`)

- `User/WorkStation/WorkStationSlot.cs` — 슬롯 한 칸. `ConsumeJudgeCount(now)`가 핵심.
- `User/WorkStation/WorkStation.cs` — 슬롯 컬렉션. `Settle(now, catalog?)`.
- `User/User.WorkStation.cs` — `SettleWorkStation` / `AssignWorkStation` / 저장.
- `Repository/WorkStationRepository.cs` — 슬롯 UPSERT.
- `Repository/LoginRepository.cs` — 슬롯 로드 추가. **신규 유저에게 기본 슬롯 1개**를 연다.
- `Common/GatheringScheduler.cs` — 30초 주기 타이머.
- `Network/ClientPacketHandler.cs` · `User/UserManager.cs`(`All`) · `Program.cs` 연결.

## 주요 결정 / 근거

- **진행도를 `LastTickAt` 하나로만 표현한다.** 방치형 진행은 시각의 함수라
  이 값 하나면 **온라인·오프라인이 같은 계산 경로**를 탄다. 별도 카운터를 두면
  경로가 둘로 갈리고, 어긋나는 순간 재화가 새거나 사라진다.
- **자투리 시간은 이월한다.** `LastTickAt += 판정수 × 30초`로 전진시켜
  29초 시점에 정산해도 손해가 없게 했다. `= now`로 덮으면 푸시가 잦을수록 손해가 난다.
- **비활성 슬롯은 `LastTickAt`이 현재를 따라간다.** 그냥 두면 캐릭터를 꽂는 순간
  비어 있던 기간만큼이 한꺼번에 터진다.
- **배치를 바꾸면 진행 중이던 조각을 버린다.** 이월하면 산업을 계속 갈아타며
  29초 조각을 모으는 악용이 가능하다. 대신 **바꾸기 전에 먼저 정산**한다.
- **드롭 테이블이 없는 산업은 조용히 건너뛴다.** 예외를 던지면 그 유저의
  다른 슬롯 정산까지 함께 죽는다. 아직 시트가 낚시 하나뿐이라 실제로 발생하는 상황이다.
- **`Settle`에 카탈로그를 주입할 수 있게 했다.** 싱글턴에 직접 붙으면
  테스트가 전역 상태를 오염시킨다.
- **타이머는 30초 주기 하나이고, 로직 스레드로 넘겨 실행한다.**
  30fps 루프를 쓰지 않은 근거는 [선행 로그](2026-07-29-workstation-slot-design.md) 참조.
  **이 타이머가 늦게 돌아도 결과는 같다** — 정산량은 주기가 아니라 경과 시각이 정한다.

## 검증

- 솔루션 빌드 **오류 0** (경고는 SQLite 패키지의 기존 `NETSDK1206`뿐).
- 테스트 **53건 통과** (기존 35 + 작업슬롯 18).
  자투리 이월, 나눠 정산해도 총량 동일, 비활성 슬롯 시간 누적 방지,
  시각 역행, 24시간 = 2,880회, 배치 변경 시 조각 폐기 등.
- Unity 미러링 정상(`Assets/Scripts_Server/Protocol`).

## 업데이트 (2026-07-29) — 재접속 유지 검증 및 user_id 버그 수정

사용자 질문 **"재접속해도 그 상태가 유지돼?"** 에 답하려고 실제 서버로 검증했고,
그 과정에서 **버그 하나를 발견해 고쳤다.**

### 🐛 슬롯 저장/로드가 서로 다른 user_id를 쓰고 있었다

`t_account`와 `t_user`는 **각자 PK를 발급하는 별개 테이블**이다.

| 경로 | 쓰던 값 |
| --- | --- |
| 슬롯 저장 (`SaveWorkStationSlotRepository`) | `User.Uid` = `t_account.user_id` |
| 슬롯 로드 (`LoginRepository`) | `_userId` = **`t_user.user_id`** ❌ |
| 인벤토리 저장·로드 (기존 코드) | `User.Uid` (일관됨) |

현재 유저가 1명뿐이라 두 값이 우연히 `1`로 같아 증상이 안 나타난다.
**유저가 늘고 가입 순서가 갈리는 순간 재접속 시 슬롯을 못 찾는다.**
인벤토리와 같은 기준(`User.Uid`)으로 통일했다.

- `DateTime.Parse` → `ParseExact` + `InvariantCulture`로 교체.
  서버 로케일에 따라 `"yyyy-MM-dd HH:mm:ss"` 파싱이 깨질 수 있었다.
  포맷 문자열과 파서를 `LoginRepository.SqliteDateTimeFormat`/`ParseUtc`로 모아 저장·로드가 어긋나지 않게 했다.

### 실제 서버 검증 결과 (모두 정상)

`MikaDummyClient`에 슬롯 배치 메뉴를 추가하고 stdin 자동 입력으로 확인했다.

1. **신규 로그인** → 기본 슬롯 1개 자동 생성, `Slot=0, Industry=0, Character=0`
2. **슬롯 배치** → DB에 `(1, 0, 2, 100, '2026-07-28 17:18:26')` 저장 확인
3. **접속 종료 후 재접속** → `Slot=0, Industry=2, Character=100` **그대로 로드됨**
4. **밀린 구간 정산** → `[Client] Recv 채취: Slot=0, 판정=4회` (오프라인 경과분)
5. **30초 주기 푸시** → 대기 중 `판정=1회` 추가 수신
6. **인벤토리 반영** → `item 1001`이 26 → 29로 증가

**로그아웃 시 정산을 따로 하지 않아도 손실이 없다.** `last_tick_at`이 DB에 남아 있어
재접속 시 그 시점부터 다시 계산되기 때문이다 — 오히려 이쪽이 정확하다.

## 후속 작업 / 주의사항
- ⚠️ **`.meta` 미생성** — `Assets/Scripts_Server/Protocol`의 변경분은 기존 파일 수정이라
  새 `.meta`는 필요 없지만, Unity를 한 번 열어 확인할 것.
- **효율배수가 빠져 있다.** `WorkStation.Settle`의 `rollCount` 계산 자리에
  캐릭터 스탯이 들어가야 한다. 지금은 `judgeCount × YieldPerJudge(=1)`뿐이다.
- ~~오프라인 감쇠 미구현~~ → **기획에서 감쇠 자체가 폐지됐다(2026-07-29).**
  현재 코드(경과 시간 전체 100%)가 곧 확정 사양이다. 구간 분할은 필요 없다.
  ⚠️ 대신 **무한 누적**이므로 총 누적 상한 도입 여부를 경제 설계와 함께 볼 것.
- **기본 슬롯 수가 코드 상수(`LoginRepository.DefaultSlotCount = 1`)다.**
  시작 슬롯 수·증가 곡선이 확정되면 테이블로 옮긴다.
- **슬롯 해금 경로가 없다.** `WorkStation.Unlock`은 있지만 이를 호출하는
  레벨업·구매 로직이 없다.
- DB 스키마 파일이 저장소에 없다(테이블을 직접 실행해 만든다). 위 DDL이 유일한 기록이다.
