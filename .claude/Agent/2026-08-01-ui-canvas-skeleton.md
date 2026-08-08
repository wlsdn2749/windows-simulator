---
date: 2026-08-01
title: 게임 UI 캔버스 골격 확정 — 16:9 배율 창 · 3열 정렬 · 렌더 모드
tags: [client, ui, canvas, window, windowmanager]
---

# 게임 UI 캔버스 골격 확정

## 목적 / 배경

T-006(작업슬롯 UI)·T-007(인벤토리 UI) 착수 전, 확정된 UI 배치를 씬으로 옮기려다
구조가 기획과 어긋나 있는 것이 드러났다. 네 가지를 한 번에 정리했다.

1. 캔버스 이름이 `Left / Mid / Right` — **위치가 런타임에 바뀌는데 이름에 박혀 있었다**
2. 캔버스가 전부 root Screen Space - Overlay — **서로 정렬시킬 수단이 없고 스프라이트가 묻힌다**
3. 무대 연출(2D·파티클) 레이어 자리가 없음
4. `WindowManager`가 **세로 1:2 · 화면 세로의 1/3·1/2·1/1** 프리셋 — 확정된 16:9 배율과 다름

## 변경 내용

- `Assets/Scenes/DesktopWindow_Control.unity` — `UI Root`(Screen Space - Camera) 아래
  `Columns` → 세 열(각 `Slot Up` / 본체 / `Slot Dn`) 구조로 재구성. 옛 `Left/Mid/Right Canvas` 제거
- `Assets/Scripts_Client/UI/WidgetPositionLayout.cs` — **신규.** 위젯 6칸 → 3열 순서·위아래 슬롯 배치.
  `[ExecuteAlways]`라 재생 없이 인스펙터에서 확인된다
- `Assets/Scripts_Client/Managers/WindowManager.cs`
  - `enum WindowSize` → **`enum WindowScale { X1, X1_25, X1_5, X2 }`**
  - `GetSize()` — 화면 비례 1:2 세로 → **기준 `960×540`(상수 `BaseWidth`/`BaseHeight`)에 배율을 곱한 절대 픽셀**
  - `ClampToWorkArea()` 신설 — 넘칠 때 **16:9를 유지한 채** 축소
  - `GetPrimaryScreenHeight()` · `GetWorkAreaHeight()` 제거 → **`GetWorkAreaSize()`** 하나로 통합(가로도 필요해짐)
- `GameDesign/기획/게임UI/README.md` — 2장 재작성(창 축 3개 분리 · 캔버스 3개 · 3단 슬롯 정렬), 6장 Q13 신규

## 주요 결정 / 근거

**① 캔버스 이름은 역할로 — `Storage` / `Workstation` / `Market`.**
위젯 위치에 따라 작업슬롯 캔버스의 좌우가 바뀌므로 위치를 이름에 넣으면 이름이 거짓이 된다.
목업 배너 문구(`STORAGE`·`WORKSTATION`·`MARKET`)와 일치시켜 문서·목업·씬이 같은 단어를 쓰게 했다.

**② 캔버스 4개 → 3개. 위젯은 캔버스가 아니라 작업슬롯 열의 슬롯 패널이다.**
사용자 제안. 위젯 6칸이 **정렬 그룹 안 형제 순서 두 개**(가로 3 × 세로 2)로 표현되어
좌표 계산이 통째로 사라진다.

**③ ⚠️ 지뢰 — LayoutGroup은 root Canvas를 움직이지 못한다.**
root Canvas의 RectTransform은 캔버스 시스템이 매 프레임 덮어써서 부모 레이아웃이 무시된다.
그래서 세 캔버스를 **하나의 root Canvas 아래 nested Canvas**로 내렸다.
nested Canvas는 RectTransform을 따르면서 **리빌드 격리는 그대로** 유지된다.

**③-1 ⚠️ 지뢰 — nested Canvas는 자기 `GraphicRaycaster`가 있어야 입력을 받는다.**
처음엔 "root에 하나면 된다"고 봤는데 **에디터에서 실측해 보니 틀렸다.**
`Graphic`은 **가장 가까운** Canvas에 등록되므로(`Graphic.canvas` = `Storage Canvas`),
root의 `GraphicRaycaster`는 nested 밑의 그래픽을 **아예 보지 못한다**
(등록 수: root 0개 / Storage 1개 / Market 32개).
→ nested Canvas마다 `GraphicRaycaster`를 붙였다. 각 레이캐스터는 자기 캔버스 것만 보므로 중복이 아니다.
**클릭이 안 먹으면 여기부터 의심한다.**

**④ 렌더 모드는 Screen Space - Camera. World Space는 기각.**
World Space가 비싼 건 아니다 — 드로우 비용은 Overlay와 비슷하다. 기각 이유는 **관리 비용**:
월드 단위로 배치·스케일해야 해서 `CanvasScaler`의 해상도 대응이 사라지고,
창 배율(1x~2x)이 바뀔 때마다 직접 맞춰야 한다. 필요한 건 "UI 사이에 스프라이트를 끼우는 것"뿐이고
그건 Screen Space - Camera로 된다. Overlay는 항상 마지막에 그려져 스프라이트가 전부 묻힌다.

**⑤ ⚠️ 지뢰 — 스프라이트는 "캔버스 사이"에만 끼울 수 있다.**
한 캔버스 **내부** UI 사이에는 못 끼운다. 그래서 작업슬롯 본체를 **BG / Stage / FG 3층**으로 나눴다.
나중에 넣으려면 계층을 통째로 재구성해야 해서 지금 자리를 만들어 둔다.

**⑥ 창 크기는 절대 픽셀.** 모니터 비례로 두면 같은 배율이라도 실제 픽셀 수가 달라져
디자인 검증·버그 재현이 안 된다. 테스크바 히어로도 같은 방식이다.

**⑦ ⚠️ 지뢰 — 방치 시엔 `Canvas.enabled = false`를 쓰고 `SetActive(false)`는 쓰지 않는다.**
`SetActive`로 끄면 열이 사라져 정렬 그룹이 재정렬되고 **위젯이 제자리를 벗어난다.**
`enabled`만 끄면 레이아웃 자리는 남아 위젯 위치가 유지되면서 그리기·리빌드는 멈춘다.

## 업데이트 (2026-08-01) — 사용자가 씬을 다시 짜며 정정된 것

**① `LayoutElement`로 크기를 지정한 것은 과했다. 전부 제거하는 쪽이 맞다.**
내가 `HorizontalLayoutGroup`·`VerticalLayoutGroup`의 `childControl*`을 켜고 `LayoutElement`로
크기를 준 탓에, 인스펙터에서 **RectTransform 크기가 잠겨 손으로 못 만지게** 됐다
("Some values driven by Canvas/Layout"). 배치를 눈으로 잡아 보는 단계에서는 최악이다.
→ `childControlWidth/Height = false`로 두면 각 오브젝트가 **자기 sizeDelta를 유지**하고,
레이아웃 그룹은 **위치만** 잡아 준다. 그러면 `LayoutElement`가 존재 이유를 잃는다.
**슬롯 3개가 고정 크기인 이 구조에서는 이게 맞다.** LayoutElement는 자식 수·크기가
유동적일 때 쓴다.

**② 슬롯이 고정이면 열마다 `VerticalLayoutGroup`도 필요 없다.**
현재 씬은 `Workstation Column`에만 VLG가 있고 창고·거래 열은 수동 배치다.
`WidgetPositionLayout`은 **`SetSiblingIndex`만** 바꾸므로 이 구성에서 그대로 동작한다 —
순서가 바뀌는 것은 `Columns`(HLG)와 `Workstation Column`(VLG)뿐이기 때문이다.

**③ `TopBar Panel` → `State Panel`.** 위젯이 위로 가면 이 패널은 아래로 내려간다.
위치를 이름에 담으면 절반이 거짓이 되므로 **담는 것**(계정 레벨·골드·시스템 아이콘)으로 부른다.
1장에서 캔버스 이름을 역할 기준으로 바꾼 것과 **같은 원칙**인데, 내가 이 패널에는 적용하지 못했다.

## 겪은 함정 — 진단 도구가 원인이었다

**`Unity_Camera_Capture`(MCP)를 부르면 Main Camera 상태가 망가진다.**
호출 직후 콘솔에 `Releasing render texture that is set to be RenderTexture.active!`가 뜨고,
정리에 실패하면서 카메라가 **`orthographic = False`(fov 60 원근)** 로 뒤집히고
`targetTexture`가 해제된 RT를 가리킨 채 남았다. → **Game 뷰가 통째로 검게 나온다.**
씬 구조 변경 탓으로 오인하기 쉽다. **UI 캔버스 작업 중에는 이 도구를 쓰지 않는다.**

**`Camera.Render()` + RenderTexture로는 Screen Space 캔버스를 측정할 수 없다.**
"불투명 픽셀 0%"가 나와도 UI가 안 그려진다는 증거가 아니다.
대조 실험에서 캔버스를 **Overlay로 바꿔도 똑같이 0%** 였다(월드 스프라이트만 90% 잡힘).
Screen Space 캔버스는 디스플레이 렌더 경로에서 주입되므로 수동 `Render()`에 포함되지 않는다.
**UI 가시성은 Game 뷰를 사람이 눈으로 보고 판단해야 한다.**

## 후속 작업 / 주의사항

- **`setStartScale`은 재지정 완료(X2).** `setStartSize`(`WindowSize`)에서 이름·타입이 모두 바뀌어
  직렬화 값이 유실됐던 것을 스크립트로 다시 넣었다. 같은 종류의 변경을 또 하면 다시 유실된다.
- **`Market Canvas` 안에 디버그 UI(패킷 테스트·창 설정)가 그대로 들어 있다.**
  거래소 콘텐츠를 넣을 때 이 자리를 어떻게 할지 정해야 한다.
- **미결 — 무대 스프라이트와 패널 rect 정합.** `Main Camera`는 orthographic size 5(= 세로 10유닛),
  1유닛 ≈ 108px. `Stage Root`의 스프라이트·파티클은 `Default` 레이어 **order = 1**로 두면
  BG(0)와 FG(2) 사이에 낀다. PPU 결정은 T-006.
- **미결 — 무대 스프라이트와 패널 rect 정합.** `Main Camera`는 orthographic size 5(= 세로 10유닛)다.
  PPU를 정해야 패널 안에 스프라이트가 정확히 들어간다 → T-006.
- **창 제어는 빌드에서만 돈다** (`#if !UNITY_EDITOR`). 배율·클램프 검증은 `.exe`로 해야 한다.
- `Win32Native.GetSystemMetrics` · `SM_CYSCREEN`은 이제 호출부가 없다. 선언은 남겨 뒀다.
- ⚠️ **씬에서 세 열의 본체가 전부 `Storage Canvas`라는 같은 이름이다**(열을 복제한 흔적).
  `Workstation Canvas` · `Market Canvas`로 고쳐야 한다.
- ⚠️ **`Workstation Body`(BG / Stage Root / FG 3층)가 사라졌다.** 열 복제 과정에서 날아갔다.
  UI 사이에 스프라이트·파티클을 끼우려면 이 층이 필요하다 → T-006 전에 되살린다.

## 업데이트 (2026-08-01) — 서비스 로케이터 조회 시점 (⚠️ 지뢰)

로그인 테스트에서 `KeyNotFoundException: 'SessionManager'` → 버튼 클릭 시 `NullReferenceException`.
**두 오류는 하나의 버그다.**

**원인: `PacketTestPanelUI`가 `OnEnable`에서 `Services.Get<SessionManager>()`를 했다.**
`Unity는 씬을 열 때 오브젝트마다 Awake → OnEnable 을 이어서 부른다` — 모든 Awake가 먼저
끝나는 것이 아니다. 패널이 `SessionManager`보다 먼저 초기화되면 아직 `Register` 전이라 던진다.
로그 순서가 증거다: **예외가 `NetworkManager is Awaken` 보다 먼저 찍혔다.**
그리고 예외로 `_session`이 null로 남아, 로그인 버튼을 누를 때 NRE로 두 번째 증상이 나왔다.

`MonoService` 주석에 *"모든 Awake 등록 완료 후 Start에서 사용"* 이라는 규칙이 **이미 있었는데**
`PacketTestPanelUI`의 주석이 *"OnEnable이면 안전하다"* 고 **거짓을 적어 둔 채** 그걸 어겼다.
같은 파일의 `WindowPanelUI`는 `Start`에서 조회해 멀쩡했다 — 그래서 증상이 한쪽에만 나왔다.

**조치:** 조회·최초 구독을 `Start`로 옮기고, 껐다 켜는 경로만 `OnEnable`이 맡도록 분리했다.
`_isSubscribed`로 중복 구독을 막는다(`Start`와 `OnEnable`이 둘 다 구독을 시도하므로).
`MonoService` 주석도 "Awake·OnEnable 둘 다 안 된다"로 강화했다.

> **씬 오브젝트 순서에 기대지 않는다.** 매니저를 UI 위로 올리거나 Script Execution Order를
> 만지는 것은 근본 해결이 아니다 — 순서가 바뀌면 조용히 다시 깨진다.

## 업데이트 (2026-08-01) — 레거시 Input 제거

`activeInputHandler: 1`(Input System 전용)인데 `WindowManager`가 레거시 `UnityEngine.Input`을 써서
**재생하자마자 `InvalidOperationException`** 이 났다. 두 곳을 Input System으로 옮겼다.

| 옛 | 새 |
| --- | --- |
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` (null 가드 포함) |
| `Input.GetKeyDown(KeyCode.Escape)` | `Keyboard.current.escapeKey.wasPressedThisFrame` |

**Player Settings를 `Both`로 바꾸는 길도 있었지만 택하지 않았다** — 패키지가 이미 활성 핸들러이고,
`Both`는 백엔드를 둘 유지하며 레거시 쪽이 조용히 어긋나는 상태를 남긴다.
`asmdef`가 없어 `Assembly-CSharp`가 `Unity.InputSystem`을 자동 참조하므로 별도 배선은 필요 없다.
