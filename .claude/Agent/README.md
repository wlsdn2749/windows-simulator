# Agent 작업 로그 (.claude/Agent)

이 폴더는 **코드 작업의 결정·맥락을 다음 작업(사람/Agent)이 참고**하도록 남기는 로그 저장소다.

- 새 코드 작업을 **시작하기 전** → `agent-log-reader` 스킬에 따라 관련 로그를 먼저 읽는다.
- 코드 작업을 **마친 뒤** → `agent-log-writer` 스킬에 따라 이 폴더에 로그(.md)를 남긴다.

## 파일 규칙

- 파일명: `YYYY-MM-DD-<kebab-slug>.md` (예: `2026-07-25-excelgenerator-bytes.md`)
- 하나의 작업 단위 = 하나의 로그 파일
- 아래 `INDEX`에 한 줄씩 최신순으로 추가한다.

## INDEX

<!-- 최신 작업이 위로. 형식: - [YYYY-MM-DD 제목](파일명.md) — 한 줄 요약 -->

- [2026-08-07 싱글턴 제거 · `GameServer` 조립 지점 — 리뷰 후보 4](2026-08-07-di-composition-root.md) — `ILogicExecutor`/`IServer` seam 신설 · `GatheringScheduler`·`SessionWatchdog`·`DBManager`·`NetworkManager` 탈싱글턴(생성자 주입) · `Entity` 폐기 후 `User`로 인라인 · `*.Instance` 21→14곳 · `FakeLogicExecutor`(기록/즉시 두 모드)로 테스트 복구 **119건 통과** · ⚠️ `Destroy()`를 즉시 실행 모드로 돌리면 `UserManager.Instance`가 오염된다 · **`Program.cs`의 `Run()` await 누락은 미해결**
- [2026-08-05 클라이언트 구조 재정비 — 송신을 UI로, 매니저는 수신 전담](2026-08-05-client-structure-refactor.md) — `SessionManager`→`PlayerDataManager`(수신 전담) · 송신 3종을 각 UI로 이관 · `HeartbeatManager`→`PingManager` · `UI/`를 패널별 폴더로 · 창 설정 `PlayerPrefs` 영속화 · ⚠️ **씬 배선 미완 — 로그인·가챠 버튼 3개가 죽어 있다**
- [2026-08-04 하트비트 무응답 세션 정리 · 중복 로그인 kick — 이슈 #10 / T-001](2026-08-04-heartbeat-idle-session.md) — 무응답 세션을 15초에 정리 · 중복 로그인은 기존 세션을 끊는다
- [2026-08-03 클라 로그 체계 정리 · 하트비트 · 가챠 갱신 통일 — 이슈 #8·#10](2026-08-03-client-log-and-heartbeat.md) — `ClientLog` 신설(`[↑송신]`/`[↓수신]` 태그 + 송신 훅) · `HeartbeatManager` 5초/15초 · 가챠를 `ApplyItemChanges` 하나로 통일 · ⚠️ **#10 서버 파트(세션 정리·pid kick)는 미착수** · `characterId` 기본값 1→1001(TID 1 폐기)
- [2026-08-02 기획 문서·엑셀·코드 불일치 전수 정리](2026-08-02-doc-data-code-sync.md) — 아이템 30→156종 · `Hunting` enum · `ItemRarity` 개명 · 낚시의 오프라인/요일 잔존 정정 (문서 12개) · 🔴 **드롭 롤이 레벨을 무시해 Lv1에서 Lv5가 나온다**(T-017 최우선) · 코드는 미수정
- [2026-08-02 기획 문서 의존 그래프 도입 — 재귀 전파 규칙과 검사기](2026-08-02-doc-graph-propagation.md) — `문서관계도.md`(그래프 단일 원본) + `check-doc-graph.ps1`(깨진 링크·**갱신일 역전**) · 문서 17개 헤더에 `바뀌면 갱신` 블록 · ⚠️ 블록은 그래프 계산에서 제외해야 한다(안 하면 간선이 양방향으로 번진다)
- [2026-08-02 가챠 풀 엑셀 이관 + ItemRarity → GlobalRarity 개명 — 이슈 #9](2026-08-02-gacha-excel-migration.md) — `Gacha.xlsx → GachaTable`(Ref 검사) · 가챠 전용 아이템 6종(100001~) · 하드코딩 `GachaTable.cs` 폐기 → `GachaPoolCatalog` · ⚠️ `EGlobalRarity` 와이어 값 재정렬(서버·클라 동시 빌드 필수)
- [2026-08-02 가챠 응답에 인벤토리 변경분(누적 총량) 포함 — 이슈 #8](2026-08-02-gacha-item-change-notify.md) — `S_GachaDrawResponse.ItemChangeInfos` 추가 · `ItemChangeInfo` 주석 정정(값은 원래 총량) · 클라 `AddGachaRewards` 제거는 클라 담당 몫 · Inventory 회귀 테스트 3건
- [2026-08-02 로그인 Load 경로를 Row 기반으로 리팩토링](2026-08-02-login-load-row-refactor.md) — Row 계약(`PlayerLoginData`) + 영역별 `Load*` 분리 · Dapper 래퍼 `DbConnection` · **Row는 프로퍼티 record + 컬럼명 그대로(snake_case), 매핑 옵션 OFF** · 신규 지급은 로직 스레드로 이동(`Login`은 지급 후)
- [2026-08-01 게임 UI 캔버스 골격 확정 — 16:9 배율 창 · 3열 정렬 · 렌더 모드](2026-08-01-ui-canvas-skeleton.md) — 캔버스 4→3개(위젯은 패널) · **LayoutGroup은 root Canvas를 못 움직인다 → nested Canvas** · Overlay→Screen Space-Camera · `WindowManager` 1:2 세로 → 16:9 절대 배율
- [2026-08-01 mattpocock-skills 각색 — 프로젝트 스킬 갱신 2건 + server-tdd 신설](2026-08-01-skill-updates-from-mattpocock.md) — task-writer(일감 쪼개기 절차) · agent-log-writer(압축 기준) · **server-tdd 신설** · CLAUDE.md 서버 스킬 표 정정
- [2026-08-01 산업 레벨 시스템 기획 — 배치가 (산업, 레벨, 캐릭터) 세 칸이 된다](2026-08-01-industry-level.md) — 경험치 없이 조건 해금 · 효과는 드롭 테이블 확장 · **낚시터·사냥터·수종이 `IndustryLevel`로 통합** · 해금 조건은 미정(T-016)
- [2026-07-30 보상 구조에서 특별보상 층(9:1) 폐지](2026-07-30-special-reward-removal.md) — 판정 1회 = 희귀도 롤 한 번 · 문서 9개 갱신 · **보스 티켓 경로와 낚시 차별화가 미정으로 남음**
- [2026-07-30 작업속도 계산을 가산/승산 분류 합성으로 재구성](2026-07-30-workspeed-add-mul.md) — `적성 × (1 + Σ가산) × Π승산` · `WorkSpeed` 누산기 신설 · **EquipSlot 부위 확정(무기1·장신구2·보석1)**
- [2026-07-29 작업슬롯 패킷 4종 Unity 클라 연동](2026-07-29-workstation-packet-client.md) — 배치/해제 송신 + 30초 채취 푸시 수신 확인 · `MonoService` null 등록 방지(CRTP 대신 런타임 검사)
- [2026-07-29 MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정](2026-07-29-mika001-unity-visibility.md) — 진단의 `Location.None`이 원인(Unity는 위치 없는 경고를 파싱 못 함) · 분석기 DLL 자동 동기화 추가
- [2026-07-29 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시](2026-07-29-workstation-slot-impl.md) — `t_workstation_slot` · 패킷 4종 · `LastTickAt` 정산 · 스케줄러 · 테스트 18건
- [2026-07-29 작업슬롯 구조 전환 — 산업 택1·요일 로테이션 폐지](2026-07-29-workstation-slot-design.md) — 슬롯당 캐릭터 1명 · 서버 권위 + 30초 푸시 · **30fps 루프 반대 근거** · P3 근거 상실
- [2026-07-29 가중치 추첨기 WeightedPicker 도입](2026-07-29-weighted-picker.md) — 드롭·희귀도·가챠 공용 추첨기(누적합 + 이진 탐색) · 그룹 인덱스 헬퍼 · 경계 검증 테스트 19건
- [2026-07-29 서버 프레임워크 프로젝트를 MikaNetwork.Lib로 묶어 폴더 구조 정리](2026-07-29-server-folder-restructure.md) — 프레임워크 5개 이동 + MikaSourceGen 이중 폴더 해소 · 게임 코드는 위치 유지(경로 하드코딩)
- [2026-07-29 데이터 파이프라인 스크립트를 GameDesign으로 이전하고 서버 테스트 프로젝트 신설](2026-07-29-pipeline-move-and-tests.md) — `generate-tables.ps1` 이동 + 절대경로 제거 · `WSGameServer.Tests`(xUnit·Shouldly·Moq) 신설
- [2026-07-28 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입](2026-07-28-drop-table-ref-check.md) — Drop.xlsx(낚시 Basic/Special) + `Ref` 마커 기반 참조 검사 · 드롭 시트는 `DropTID(ID)` 선두 규칙
- [2026-07-27 ExcelGenerator 생성 코드를 C# 9로 수정](2026-07-27-csharp9-codegen-fix.md) — 이슈 #6 대응 · 블록 네임스페이스 + 파이프라인 C# 9 규약 검사 추가
- [2026-07-27 낚시 기획 확정 및 요일 로테이션 구조 전환](2026-07-27-fishing-design-lock.md) — 낚시 논점 확정 + 요일 효율 보너스 전환 + **채취주기 30초 통일·개체 변량 폐기**(2차 업데이트)
- [2026-07-26 GameDesignCore 도입 및 시스템별 상세 기획안 구축](2026-07-26-game-design-core.md) — 게임 기획 단일 진입점 + 상세안 7종 + 평가 문서 + 강제 참조 스킬
