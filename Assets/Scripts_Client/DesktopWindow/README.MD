# Desktop Window Control (Task Bar Hero 스타일) — Built-in 성공 버전

> ✅ **결론 먼저** : URP에서 투명 배경이 끝내 불가능함을 (재검증까지) 확인했고, **Built-in 렌더 파이프라인으로 이전하여 투명·보더리스·항상위·클릭스루·드래그·위치/크기 제어를 모두 성공**시켰습니다. 
> 이 문서는 그 이전 과정과 도중에 만난 함정·해결 방법을 정리한 문서입니다.

## 목차
- [📋 개요](#-개요)
- [🎮 참고 게임 : TBH Task Bar Hero](#-참고-게임--tbh-task-bar-hero)
- [🧩 구성 스크립트 & 씬](#-구성-스크립트--씬)
- [🔧 Win32 / DWM 호출 매핑](#-win32--dwm-호출-매핑)
- [❗ URP는 안 되고 Built-in은 되는 이유](#-urp는-안-되고-built-in은-되는-이유)
- [🚚 URP → Built-in 이전 과정](#-urp--built-in-이전-과정)
- [🧪 빌드 검은 화면 디버깅 타임라인](#-빌드-검은-화면-디버깅-타임라인)
- [🕳️ 발견한 함정들](#️-발견한-함정들)
- [⚙️ 최종 세팅 체크리스트](#️-최종-세팅-체크리스트)
- [🔗 참고 링크](#-참고-링크)
- [❓ 미해결 의문](#-미해결-의문)

---

## 📋 개요

*이 섹션 : 이 데모가 무엇이고, 무엇을 달성했는지.*

**기간 :** 2026.06.11 ~ 2026.06.12

**엔진 :** Unity 6.3 LTS (6000.3.10f1) 

**Built-in Render Pipeline (2D 템플릿 기반)**

바탕화면 위에서 **타이틀바 없는 투명 창**으로 동작하는 데스크톱 앱의 핵심 "창 기능"만 독립적으로 구현·검증하는 데모입니다.

**TBH : Task Bar Hero**가 창모드로 바탕화면 위에서 방치형 게임을 진행하는 컨셉이 인상 깊어, **게임 로직은 제외하고 창 관련 기능만** 떼어내 검증하려 했습니다. 처음엔 URP에서 진행했으나 **투명 배경이 URP의 구조적 한계로 불가능**했고, Built-in으로 이전하여 모든 기능을 성공시켰습니다.

| 기능 | 상태 | 핵심 API |
|------|:----:|------|
| 타이틀바 토글 (보더리스 On/Off) | ✅ | `SetWindowLong(WS_CAPTION ↔ WS_POPUP)` |
| 항상 위 (Always On Top) | ✅ | `SetWindowPos(HWND_TOPMOST)` |
| 투명 배경 (바탕화면 비침) | ✅ | `DwmExtendFrameIntoClientArea(-1)` + 카메라 알파 0 |
| 클릭 스루 (빈 영역 클릭 통과) | ✅ | `WS_EX_TRANSPARENT` + `GetCursorPos` 폴링 |
| 창 이동 | ✅ | OS 타이틀바를 켜고 그 타이틀바를 잡아 드래그 |
| 위치(9분할 앵커)·크기(프리셋) | ✅ | `SystemParametersInfo(SPI_GETWORKAREA)` + `SetWindowPos(x, y, w, h)` |

각 기능이 무엇을 하고 어떻게 구현하는지:
- **타이틀바 토글** — OS 타이틀바+테두리를 켜고 끈다. 켜면 `WS_CAPTION|WS_SYSMENU`(이 타이틀바로 창 이동, 단 리사이즈용 `WS_THICKFRAME`은 넣지 않아 크기 조절 불가), 끄면 캡션/프레임 비트를 제거하고 `WS_POPUP`을 더한 보더리스가 된다. 어느 쪽이든 프레임(`SWP_FRAMECHANGED`)을 갱신해 반영한다.
- **항상 위** — 다른 창들보다 항상 앞에 둔다. `SetWindowPos`의 Z순서 인자에 `HWND_TOPMOST`를 넘긴다.
- **투명 배경** — 바탕화면이 비치게 한다. `DwmExtendFrameIntoClientArea(MARGINS=-1)`로 DWM 유리 프레임을 창 전체로 확장하고, 카메라가 알파 0으로 그린 픽셀이 그대로 투명해진다.
- **클릭 스루** — 빈 영역 클릭을 뒤 창으로 통과시킨다. 확장 스타일에 `WS_EX_TRANSPARENT`를 켜고, 통과 상태에선 입력이 안 오므로 `GetCursorPos`로 커서를 직접 폴링해 UI 위인지 판정한다.
- **창 이동** — 커스텀 드래그 바 없이, 타이틀바 토글을 켜서 나타난 **OS 진짜 타이틀바**를 잡고 옮긴다(OS가 기본 처리). 타이틀바가 켜지면 그 바를 잡을 수 있도록 동적 클릭 스루를 잠시 해제한다.
- **위치·크기** — 위치는 9분할 앵커(좌상단~우하단) 드롭다운으로, 크기는 프리셋(작은 화면/큰 화면) 드롭다운으로 고른다. 크기는 고정 픽셀이 아니라 **주 모니터 세로 해상도 기준으로 런타임 계산**한다(작은=세로 1/3, 큰=세로 1/2, 1:2 비율 유지 → 세로 1/2 이하라 작업표시줄을 침범하지 않음). `SystemParametersInfo(SPI_GETWORKAREA)`로 작업표시줄을 제외한 작업 영역을 얻어, 창이 화면 밖으로 나가거나 작업표시줄에 가리지 않게 좌표를 계산한 뒤 `SetWindowPos`로 옮긴다. 크기를 바꾸면 현재 앵커 위치로 재정렬한다. 캔버스 기준 해상도는 360×720로 고정이라 창 크기에 맞춰 UI가 자동으로 비례 스케일된다.

---

## 🎮 참고 게임 : TBH Task Bar Hero

*이 섹션 : 어떤 게임에서 영감을 받았고, 그 게임에 대해 무엇이 확인되고 무엇이 미확정인지.*

| 항목 | 내용 |
|------|------|
| 출시 | 2026.05.27 |
| 장르 | 데스크톱 컴패니언 방치형 RPG |
| 엔진 | **공식 표기 없음 — 미확정.** 투명창 동작 방식으로 보아 네이티브 창 제어(Win32/DWM)를 사용한 것으로 추정 |
| 출처 | [Steam 공식 페이지](https://store.steampowered.com/app/3678970/TBH_Task_Bar_Hero/) |

관찰된 핵심 기능 : 타이틀바 없는 투명 창, 바탕화면 비침, 클릭 통과, 항상 위.

> ℹ️ 엔진을 단정할 1차 출처(개발사 공식 표기)는 확인되지 않았습니다. 사용 엔진을 확정하려면 [SteamDB](https://steamdb.info)에서 해당 앱의 빌드/디펜던시(예: UnityPlayer.dll 포함 여부)를 확인하는 방법이 있습니다.

---

## 🧩 구성 스크립트 & 씬

*이 섹션 : 어떤 파일들로 이뤄졌고, 씬이 어떻게 구성됐는지.*

`Assets/Scripts_Client/` (역할별 폴더로 분리)

| 파일 | 위치 | 역할 |
|------|------|------|
| `Win32Native.cs` | `DesktopWindow/` | Win32 / DWM API의 P/Invoke(네이티브 함수·상수·구조체) 선언 모음 |
| `WindowManager.cs` | `Managers/` | 창 제어 본체 (타이틀바·투명·항상위·클릭스루·9분할 위치·크기 프리셋), 싱글턴 |
| `WindowPanelUI.cs` | `UI/` | 창 기능 토글 4개(타이틀바·투명·항상위·동적클릭스루) + 위치·크기 드롭다운 2개를 WindowManager 설정과 동기화·배선하는 디버그 패널 |
| `DesktopWindow_Control.unity` | `Assets/Scenes/` | 데모 씬 |

> URP 시절의 `TransparentCameraOutput.cs`(RenderTexture→RawImage 우회용)는 Built-in에선 불필요하여 이전하지 않았습니다.

### 씬 구조
```
DesktopWindow_Control.unity
├─ Main Camera        (Clear Flags = Solid Color, 배경 알파 0)
├─ Directional Light
├─ EventSystem        (InputSystemUIInputModule, 프로젝트 InputSystem_Actions 연결)
├─ WindowManager      (WindowManager 부착, Raycast Camera 자동 확보)
└─ Canvas  (Screen Space - Overlay, CanvasScaler = Scale With Screen Size / Match Height)
   └─ DemoPanel Panel (WindowPanelUI)
      ├─ Toggle(타이틀바) / Toggle(투명) / Toggle(항상위) / Toggle(동적클릭스루)
      └─ Dropdown(위치 9분할) / Dropdown(크기 프리셋)
```

---

## 🔧 Win32 / DWM 호출 매핑

*이 섹션 : 각 기능이 실제로 어떤 네이티브 호출로 구현되는지.*

이 데모는 Unity가 제공하지 않는 OS 창 제어를 위해 **Windows 네이티브 라이브러리**를 직접 호출합니다. C#에서는 이를 **P/Invoke**(`[DllImport]`)로 호출합니다 — OS DLL의 C 함수 시그니처를 C#으로 그대로 옮겨 적으면 런타임이 호출을 연결해 줍니다.

- **`user32.dll`** — 창 스타일/위치/메시지/커서 등 일반 윈도우 제어 (`SetWindowLong`, `SetWindowPos`, `SendMessage`, `GetCursorPos` 등)
- **`dwmapi.dll`** — DWM(데스크톱 윈도우 매니저) 합성. 투명 유리 프레임 확장 (`DwmExtendFrameIntoClientArea`)
- **`kernel32` (System.Diagnostics.Process)** — 프로세스의 메인 창 핸들 획득 (`Process.MainWindowHandle`). 창 핸들을 안정적으로 얻는 데 사용

| 기능 | 핵심 호출 | DLL |
|------|-----------|-----|
| 창 핸들 확보 | `Process.MainWindowHandle` (폴백 `GetActiveWindow`) | kernel32 / user32 |
| 타이틀바 On | `GetWindowLong` → `WS_CAPTION\|WS_SYSMENU` (WS_THICKFRAME 제외) → `SetWindowLong` + `SetWindowPos(SWP_FRAMECHANGED)` | user32 |
| 타이틀바 Off(보더리스) | `GetWindowLong` → 테두리 비트 제거 + `WS_POPUP` → `SetWindowLong` + `SetWindowPos(SWP_FRAMECHANGED)` | user32 |
| 투명 배경 | `DwmExtendFrameIntoClientArea(MARGINS = -1)` + 카메라 알파 0 | dwmapi |
| 항상 위 | `SetWindowPos(HWND_TOPMOST, …)` | user32 |
| 클릭 스루 | `SetWindowLong(GWL_EXSTYLE, WS_EX_LAYERED \| WS_EX_TRANSPARENT)` | user32 |
| 창 이동 | OS 타이틀바(`WS_CAPTION`)를 켜서 그 바를 잡고 드래그(OS 기본 처리) | user32 |
| 위치/크기 | `SystemParametersInfo(SPI_GETWORKAREA)`로 작업 영역 조회 + `SetWindowPos(x, y, w, h)` | user32 |
| 커서 위치 (클릭스루 중 판정) | `GetCursorPos()` + `GetWindowRect()` | user32 |

---

## ❗ URP는 안 되고 Built-in은 되는 이유

*이 섹션 : 똑같은 코드인데 왜 파이프라인에 따라 투명이 갈리는지.*

**DWM의 투명은 창 백버퍼(최종 출력)의 알파 채널을 읽어** 투명도를 결정합니다. `DwmExtendFrameIntoClientArea(MARGINS=-1)`로 프레임을 창 전체로 확장하면, **백버퍼에서 알파가 0인 픽셀이 그대로 투명**해져 바탕화면이 비칩니다. 관건은 **"카메라가 그린 알파 0이 백버퍼까지 살아남는가"** 이고, 여기서 두 파이프라인이 갈립니다.

| 구분 | 🔹 Built-in (됨) | 🔹 URP (안 됨) |
|------|------------------|----------------|
| 카메라 출력 경로 | 백버퍼로 직행 | 여러 중간 렌더 타겟을 거친 뒤 FinalBlit |
| 백버퍼 알파 | 알파 0이 **그대로 기록됨** | FinalBlit에서 **알파를 1(불투명)로 덮어씀** |
| DWM 투명 결과 | 투명 (바탕화면 비침) | **배경 검정** |
| 알파 보존 옵션 | 불필요 | Alpha Processing(URP 17)도 후처리·RT 알파만 보존, **최종 스왑체인 미보장** |

- URP는 "화면은 불투명하다"는 전제로 설계되어 최종 출력에서 알파를 덮어씁니다. **백버퍼 알파를 보존하지 않는다**는 것이 핵심.
- 이번에 URP 쪽에서 UI 가시성 변수까지 통제하고 다시 테스트했지만, 여전히 **배경만 검정**으로 남아 **URP의 FinalBlit 알파 미보존**이 원인으로 확정됐습니다.

> 💡 결정적 단서 : URP에선 "UI는 보이고 배경만 검정"이 재현됩니다. UI(Canvas Overlay)는 알파가 보존되어 보이지만 **카메라(URP) 출력 경로만 알파를 1로 덮어쓰기** 때문입니다. Built-in엔 이 덮어쓰기 단계가 없습니다.

---

## 🚚 URP → Built-in 이전 과정

*이 섹션 : 실제로 무엇을 옮기고 무엇을 들어냈는지.*

1. **창 제어 스크립트 복사** (`Win32Native`, `WindowManager`, `WindowPanelUI`).
   - `.meta`까지 복사해 GUID를 보존 → 씬의 컴포넌트 참조가 그대로 연결됨.
   - URP 시절의 `NewAssembly.asmdef`는 가져오지 않음 → 스크립트가 **Assembly-CSharp**에 포함되어 UGUI/InputSystem을 자동 참조(컴파일 안전). UnityEvent는 GUID로 바인딩되므로 어셈블리명이 달라도 무관.
2. **씬 이전 + URP 잔재 제거.**
   - URP 전용 컴포넌트 `UniversalAdditionalCameraData` / `UniversalAdditionalLightData` 제거.
   - `TransparentCameraOutput` 컴포넌트 + 전체화면 `CameraOutput` RawImage 제거.
   - EventSystem의 InputSystem 액션 참조 GUID를 이 프로젝트의 `InputSystem_Actions`로 치환 (Unity 6 표준 템플릿이라 내부 fileID가 동일해 그대로 연결됨).
3. **Player Settings 정리** (아래 체크리스트 참조).
4. **빌드 → 검은 화면 디버깅** (다음 섹션).

---

## 🧪 빌드 검은 화면 디버깅 타임라인

*이 섹션 : 에디터에선 멀쩡한데 빌드만 검은 화면이던 원인들을 순서대로 격리한 기록.*

빌드에서 "유니티 로고 후 창이 사라지거나 검은 화면"이 나왔고, **디버그 단계 모드**(효과를 키로 하나씩 적용)로 원인을 분리했습니다.

| 증상 | 원인 | 해결 |
|------|------|------|
| 스플래시 후 창이 사라짐 | `Start()`의 `GetActiveWindow()`가 스플래시 동안 엉뚱한/빈 핸들을 잡음 | `Process.MainWindowHandle`이 유효해질 때까지 **코루틴 폴링** 후 적용 |
| 보더리스 적용 후 창 깨짐 | 스타일을 통째 교체 + 프레임 갱신 누락 | 테두리 **비트만 제거** + `SetWindowPos(SWP_FRAMECHANGED)`로 프레임 재계산 |
| 빌드에서 UI가 안 보임 | CanvasScaler 기준 해상도를 세로형(200×600)으로 직접 잡아, 1920 빌드 창에서 UI가 화면 밖으로 밀림 | CanvasScaler **Match = Height**로 변경 + 시작 창을 작은 세로 창으로 |

> 마지막 항목은 메이저 이슈가 아니라, "세로로 긴 창"을 의도해 기준 해상도를 직접 세로형으로 잡다 생긴 해프닝입니다. 기본값(1920×1080)으로 작업했다면 발생하지 않았을 문제입니다. (자세한 내용은 함정 #5)

---

## 🕳️ 발견한 함정들

*이 섹션 : 다시 만나면 헷갈릴 수 있는 비직관적 함정들.*

### 1. 클릭스루 ON 상태에선 Unity 입력이 죽는다
`WS_EX_TRANSPARENT`가 걸리면 **OS가 마우스 메시지를 창에 보내지 않습니다.** 따라서 `Input.mousePosition` / `EventSystem.IsPointerOverGameObject()`가 동작하지 않아 클릭스루가 영영 안 풀립니다.
→ **해결** : `GetCursorPos`로 전역 커서를 폴링하고, 그 좌표를 `EventSystem.RaycastAll`에 직접 넣어 UI 위인지 판정. (`WindowManager.GetCursorScreenPosition()` / `IsPointerOverContent()`)

### 2. WS_EX_LAYERED 는 단독으로 쓰면 안 된다
`WS_EX_LAYERED`는 `SetLayeredWindowAttributes`(균일 알파/색상키) 또는 `UpdateLayeredWindow`(per-pixel)와 **짝으로** 써야 합니다. 다만 본 데모는 DWM per-pixel 투명을 쓰므로 **`SetLayeredWindowAttributes`를 일부러 호출하지 않습니다.** 호출하면 DWM 투명이 균일 알파 모드로 덮여 창이 검게 변합니다. (그래서 `Win32Native`엔 선언만 있고 미사용)

### 3. 보더리스는 스타일을 통째로 덮어쓰지 말 것
`SetWindowLong(GWL_STYLE, WS_POPUP | WS_VISIBLE)`로 전부 교체하면 `WS_CLIPCHILDREN` 등 필수 비트가 사라져 창이 깨지거나 안 보일 수 있습니다.
→ **기존 스타일에서 `WS_CAPTION`/`WS_THICKFRAME`/`WS_MINIMIZEBOX`/`WS_MAXIMIZEBOX`/`WS_SYSMENU`만 제거**하고 `WS_POPUP`을 더한 뒤, `SWP_FRAMECHANGED`로 프레임을 갱신.

### 4. 창 핸들은 스플래시 이후에 잡을 것
`Start()`에서 바로 `GetActiveWindow()`를 부르면 유니티 스플래시 때문에 메인 창이 아직 활성이 아니라 빈/엉뚱한 핸들을 잡습니다.
→ `Process.GetCurrentProcess().MainWindowHandle`이 유효해질 때까지 코루틴으로 대기 후 적용.

### 5. CanvasScaler Match 기준 (작은 해프닝)
기준 해상도를 세로형(200×600)으로 직접 잡고 **Match = Width**로 두면, 빌드 창(1920×1080)에서 가로 기준으로 과확대되어 세로 단위가 줄고 UI가 화면 밖으로 밀립니다. 에디터 Game뷰를 같은 200×600으로 보고 있으면 눈치채기 어렵습니다.
→ **Match = Height**로 두면 세로 단위가 보장되어 해결. (기본 1920×1080 기준으로 작업했다면 애초에 없었을 문제)

### 6. 빌드 시작 씬
빌드는 Build Settings **씬 목록의 첫 번째(활성) 씬**을 실행합니다. 데모 씬을 첫 번째 enabled로 등록할 것.

---

## ⚙️ 최종 세팅 체크리스트

*이 섹션 : 새로 세팅할 때 그대로 따라 하면 되는 실용 체크리스트.*

**Player Settings**
- Fullscreen Mode = **Windowed**
- 그래픽 API (Windows) = **Direct3D11** (Auto for Windows 해제)
- **Use Flip Model Swapchain = 해제**
- Active Input Handling = **Both** (에디터 폴백 안전)
- Run In Background = **켜기** (포커스 없어도 클릭스루 폴링 동작)

**카메라**
- Clear Flags = **Solid Color**, 배경색 **알파 0**

**Canvas / UI**
- CanvasScaler = Scale With Screen Size, **Match = Height(1)**
- UI는 화면 일부만 차지하게 (전체화면 불투명 패널 금지)

**WindowManager (인스펙터)**
- Set Start Title Bar = 켜기(첫 실행에서 타이틀바로 창 이동 가능) / Start Transparent / Start Topmost = 켜기
- Set Start Dynamic Click Through = 켜기 권장(빈 영역 클릭 통과 자동 판정)
- Size Presets = 작은 화면(세로 1/3) / 큰 화면(세로 1/2) — 주 모니터 세로 기준 런타임 계산, Set Start Size = Small 권장
- Set Start Anchor = 원하는 9분할 위치(예: Lower Right)

**빌드**
- Build Settings에 데모 씬을 **첫 번째 활성 씬**으로 → Windows x64 빌드
- 확인 : ① 바탕화면 비침(투명) ② UI 표시 ③ 빈 영역 클릭 통과 / UI 위 클릭 먹힘 ④ 타이틀바 토글 켜고 타이틀바 드래그 → 창 이동 ⑤ 타이틀바 켠 상태에서 가장자리 당겨도 리사이즈 안 됨

**런타임 키**
- `ESC` 종료

---

## 🔗 참고 링크

*이 섹션 : 각 결론의 근거가 된 1차 자료. 옆 주석은 "그 글에서 무엇을 확인했는지".*

**Task Bar Hero**
- [Steam 공식 페이지](https://store.steampowered.com/app/3678970/TBH_Task_Bar_Hero/) — 출시일·장르 등 게임 정보 1차 출처 (엔진 표기는 없음)

**Win32 / DWM API (Microsoft Learn)**
- [DwmExtendFrameIntoClientArea](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea) — DWM 프레임을 클라이언트 영역으로 확장(투명)
- [SetWindowLong](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlonga) / [GetWindowLong](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlonga) — 창 스타일 변경
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) — 위치/크기/Z순서/프레임 갱신
- [Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles) / [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles) — `WS_*` / `WS_EX_*` 상수
- [SetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes) — 레이어드 창 알파/색상키 (본 데모 미사용)
- [WM_SYSCOMMAND](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand) — `SC_MOVE`(0xF010)+`HTCAPTION`(0x0002) 창 드래그
- [ReleaseCapture](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasecapture) / [GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos) / [GetWindowRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect)

**URP에서 투명이 안 되는 근거 (문제 상황)**
- [Unity Discussions — "Unity 6.1 and Transparent Applications"](https://discussions.unity.com/t/unity-6-1-and-transparent-applications/1653872) — URP 투명 빌드가 막혀 결국 Built-in/구버전으로 회귀했다는 사례
- [Unity Discussions — "URP Camera Solid Background Transparent"](https://discussions.unity.com/t/urp-camera-solid-background-transparent/879573) — URP 카메라 Solid Color 배경의 알파가 빌드에서 보존되지 않는 문제 논의
- Unity Issue Tracker — "[URP] Background's Transparency is lost on Render Texture when Post Processing is enabled" — 후처리 켜면 RenderTexture에서 배경 투명이 사라지는 공식 이슈 (issuetracker.unity3d.com에서 제목으로 검색)

**해결/우회 시도의 근거**
- [Unity Manual — "What's new in URP 17 (Unity 6)"](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/whats-new/urp-whats-new.html) — Alpha Processing 설정 추가. RenderTexture 알파는 보존하나 데스크톱 백버퍼는 미보장임을 확인
- [Unity Manual — "Universal Render Pipeline Asset reference"](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/universalrp-asset.html) — HDR/Grading 등 알파 보존에 관여하는 URP Asset 옵션 위치 확인
- [GitHub — mackysoft/UnityCapture-URP](https://github.com/mackysoft/UnityCapture-URP) — Allow HDR를 끄면 카메라 출력이 알파 보존 8bit 포맷이 된다는 우회 근거

**.NET / Unity 일반**
- [Process.MainWindowHandle](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.mainwindowhandle) — 프로세스 메인 창 핸들
- [Unity CanvasScaler](https://docs.unity3d.com/Manual/script-CanvasScaler.html) — UI 스케일 모드/매칭(Match Width/Height)
- [Unity 플랫폼 의존 컴파일 `UNITY_EDITOR`](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html)
- [P/Invoke (DllImport)](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

---

## ❓ 미해결 의문

*이 섹션 : 아직 확실치 않거나 추가 검증이 필요한 부분.*

1. **URP에서 정말 방법이 없나?** — 커스텀 `ScriptableRenderPass`로 FinalBlit 이후 백버퍼 알파를 직접 쓰는 길이 이론상 있으나, FinalBlit이 사용자 패스 이후 단계라 접근이 막혀 커뮤니티도 확실한 해법을 못 냈습니다. 이번 재검증에서도 실패. Unity 정식 지원을 기다리는 편이 현실적입니다.
2. **클릭스루 + DWM 투명 공존** — 현재 클릭스루는 `WS_EX_LAYERED|WS_EX_TRANSPARENT`만 쓰고 `SetLayeredWindowAttributes`는 호출하지 않아 DWM 투명과 공존합니다. 다양한 GPU/드라이버에서 추가 검증 여지.
3. **다중 모니터 좌표** — `GetCursorScreenPosition()`은 단일 모니터 기준. 음수 좌표 모니터 환경에서 재검증 필요.

---

*이 문서는 2026.06.11 URP 시행착오 → 2026.06.12 Built-in 이전 성공까지의 기록입니다.*
