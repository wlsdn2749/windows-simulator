---
date: 2026-08-05
title: 클라이언트 구조 재정비 — 송신을 UI로, 매니저는 수신 전담 / 창 설정 영속화
tags: [client, refactor, ui, manager, playerprefs]
---

# 클라이언트 구조 재정비 (Scripts_Client)

## 목적 / 배경

`Scripts_Client`는 서버 동작 확인용으로 급히 띄운 임시 UI였다. 서버가 붙었으니
본격적인 클라 작업 전에 구조를 다시 잡았다. **동작은 그대로, 구조만 바꾸는 작업**이다
(창 설정 저장·위젯 정렬 전환 두 가지만 사용자가 명시 요청한 신규 동작).

## 변경 내용

diff와 커밋 메시지가 파일 단위 변경을 이미 말하므로, 여기선 **구조의 축**만 남긴다.

- **송신/수신 축 분리** — `SessionManager` → `PlayerDataManager`(수신 전담).
  송신 3종은 각각 그 요청을 일으키는 UI로 이관(`LoginPanelUI` · `GachaTestButtonUI` ·
  `WorkStationTestButtonUI`). `PingManager`의 Ping만 예외로 매니저에 남았다.
- **UI 폴더를 패널별로 분할** — `UI/{Storage,WorkStation,Market,Widget,Layout,Login,Debug}`.
- 신규: `UIManager`(3열+위젯 참조 허브 · 여닫기 API) · `WindowSettings`(PlayerPrefs 래퍼) ·
  패널 껍데기 3종(`StoragePanelUI`·`MarketPanelUI`·`WidgetPanelUI`).

## 주요 결정 / 근거

- **왜 송신 전용 매니저를 새로 만들지 않았나** — 송신 3종은 4~6줄짜리 패킷 조립 껍데기였고,
  "어느 슬롯·어떤 산업·배치인지 해제인지"를 아는 쪽은 전부 UI였다. 중간 계층을 두면
  값을 아는 곳과 보내는 곳이 갈라진다. 기각한 대안: `ClientSend` 정적 게이트웨이
  (한 겹 더 감싸는 것뿐), 송신 매니저 분리(사용자 의도와 반대 방향).
- **`_lastRequestWasAssign`를 없앤 이유** — 실패 응답에 슬롯이 안 실려 와(`Slot=null`)
  배치/해제를 수신만으로 구분할 수 없어서 송신 시점에 기록하던 값이었다. 송신이 UI로 가면서
  그 UI가 스스로 알게 되어 불필요해졌다. 그래서 이벤트가
  `WorkStationAssignCompleted(bool success, bool wasAssign)` → **`(bool success)`** 로 줄었다.
- **`PlayerDataManager.SetLoginId`가 존재하는 이유** — 수신 전담 매니저는 "무엇으로
  로그인했는지"를 알 수 없는데 `StatePanelUI`가 그 Id를 표시에 쓴다(서버가 닉네임을 안 준다).
  보낸 쪽이 알려 주는 한 줄로 해결했다. **서버가 닉네임을 주기 시작하면
  `LoginId`와 함께 지울 것.**
- **매니저 초기화 규약을 `Start`로 통일** — `CacheReferences() → Subscribe() → Initialize()`
  + `_isReady` 가드 + `OnEnable` 재구독. 기존에 매니저 3개가 전부 다른 모양이었다.
- **창 설정 로드만 `Awake`인 이유** — `WindowPanelUI.Start()`가 `WindowManager`의 값을 읽어
  토글·드롭다운을 맞추는데 Unity는 **Start 순서를 보장하지 않는다.** Start에 두면 패널이
  먼저 돌 때 안 읽은 값을 가져간다. 다른 서비스를 안 건드리는 순수 값 로드라 Awake가 안전하다.

## 후속 작업 / 주의사항

- ⚠️ **씬 배선이 남아 있다.** 코드만 정리했고 컴포넌트 이동·신규 부착은 에디터 작업이다.
  작업 시점에 씬이 `isDirty=True`(사용자 편집 중)라 스크립트로 건드리지 않았다.
  - **지금 로그인·가챠 버튼 3개가 죽어 있다.** `Market Canvas/Common Packet Panel`의
    `Login/GachaSingle/GachaTen Send Button`이 사라진 `PacketTestPanelUI.SendXxx`를
    인스펙터 OnClick으로 가리킨다. `LoginPanelUI`·`GachaTestButtonUI`를 붙이고
    **인스펙터 OnClick 항목은 지운다**(새 UI는 코드로 `AddListener` 한다).
  - 슬롯 버튼 8개의 OnClick에는 `<NULL 대상>` 빈 항목이 남아 있다(원래 그랬다. 무해하지만 정리 권장).
- **이름 변경·파일 이동은 씬을 안 건드려도 된다** — `.meta`를 함께 옮겨 GUID가 보존됐고,
  Unity에서 `Missing Script 0개`를 확인했다. 앞으로도 `.cs`만 옮기고 `.meta`를 두면 씬이 깨진다.
- `WorkStationPanelUI.body`, `WidgetPositionLayout.alignmentTargets`는 **선택 참조**다.
  비워 두면 각각 여닫기가 무동작 / 정렬 전환 없음이며, 나머지 동작은 그대로다.
- `Common/`은 사용자 지시로 손대지 않았다. `MonoService.cs` 주석의 예시가 아직
  `SessionManager`를 든다 — 동작에는 영향 없으나 다음에 손댈 때 함께 고칠 것.
- 씬 실측 구조가 예상과 달랐다: `WidgetPositionLayout`은 `UI Root`가 아니라
  `Workstation Column/Widget Canvas`에 있고, 작업슬롯 테스트 버튼 8개는 **Market Column** 아래에 있다.
- 문서 그래프 검사에서 갱신일 역전 경고 3건(`게임기획코어`·`퀘스트`·`산업레벨`)이 뜨지만
  **전파 불필요로 판단했다** — 이번 변경은 구현 경로와 창 설정 저장뿐이고, 그 논점은
  `게임UI/README.md`에만 존재한다(다른 문서에 코드 경로 참조가 없음을 grep으로 확인).

---

## 업데이트 (2026-08-06)

여닫기 기능을 실제로 붙이면서 드러난 문제를 고치고, 이름 규칙을 정리했다.

### 지뢰 — 이걸 모르면 또 밟는다

- 🔴 **중첩 Canvas에 `overrideSorting=true`를 켜면 자기 `GraphicRaycaster`가 반드시 필요하다.**
  자기만의 정렬 그룹이 되어 부모 Raycaster가 그 안을 훑지 않는다. `Widget Canvas`·`State Canvas`에
  없어서 **버튼이 hover 반응조차 없었다.** 코드를 아무리 봐도 안 나오는 종류의 버그다.
- 🔴 **여닫을 때 Column을 끄면 안 된다. 그 안의 Canvas만 끈다.**
  Column을 끄면 남은 열이 `Horizental Columns`(MiddleCenter)에서 재배치돼 **위젯이 중앙 기준으로
  밀린다.** Canvas만 끄면 Column 3개와 `(Layout)` 스페이서가 남아 폭이 그대로다.
  → 이 결정 때문에 가로 정렬 보정(`columnsGroup`)은 넣었다가 다시 뺐다.
- 🟠 **`[ExecuteAlways]` 컴포넌트에서 `PlayerPrefs`를 무조건 읽으면 에디터 미리보기가 망가진다.**
  `WidgetPositionLayout`이 그렇다 — 인스펙터로 6칸을 바꿔 보는 순간 저장값이 덮어쓴다.
  `Application.isPlaying`일 때만 읽고 쓴다. 에디터에선 인스펙터가 진실, 빌드에선 저장값이 진실.
- 🟠 **"여는 것만" 하는 버튼은 고장 난 것처럼 보인다.** 시작이 "전부 열림"이라 `ShowStorage(true)`를
  눌러도 변화가 없었다. 여닫기 버튼은 토글이어야 한다.

### 이름 규칙

- 오브젝트가 Canvas면 클래스도 `XxxCanvasUI`. `Storage`·`Market`·`WorkStation`·`State`·`Widget` 5개.
  `LoginPanelUI`·`GachaTestButtonUI`는 캔버스가 아니라 그대로 뒀다.
- `ClientLog`→`ClientLogger`, `PacketLogUI`→`Log/PlayerDataLogger`.
  **후자는 UI가 아니다**(직렬화 필드 0·화면 출력 0) — `UI/`가 아니라 `Log/`에 있고
  씬에서도 `Log Manager` 루트에 붙는다. 패널을 닫아도 로그는 나와야 하기 때문이다.
- `WindowPanelUI` → `UI/Settings/SettingsPanelUI`. 창 제어(Win32)와 일반 설정(위젯 6칸)을
  한 패널에서 받되 헤더로 나눴다. **`WidgetPositionLayout`을 여기 흡수하지 않았다** —
  `[ExecuteAlways]`가 죽고, 설정 창을 닫으면 레이아웃 계산도 같이 멈춘다.

### 후속

- `PlayerDataLogger` 제거 조건은 `C:\Users\ASUS\Desktop\먼저 할일.MD`에 정리했다
  (가챠 결과 팝업 · 실패 토스트 · 위젯 수확 표시).
- Arca 툴킷은 `Common/` 2건 + README를 반영해 커밋했다(`0419958`). 프로젝트에만 있는 스킬 6개 중
  범용 4개(`agent-log-*`·`task-*`)의 마스터 이관은 **사용자 판단으로 보류**.
