# UI 스크립트 규칙

> 최종 업데이트: 2026-08-08 · 대상: `Assets/Scripts_Client/UI/`

이 폴더에 스크립트를 새로 만들기 전에 읽는다. **이름을 뭐라고 붙일지 · 어느 오브젝트에 붙일지 ·
어느 폴더에 넣을지**를 여기서 정한다.

---

## 1. 세 층으로 나눈다

```
┌─ Manager 층 ─────────────────  Assets/Scripts_Client/Managers/
│   UIManager           무엇을 열고 닫을지 결정한다
│   WindowManager       Win32 창을 실제로 조작하고 저장한다
│   PlayerDataManager   서버 데이터를 보관하고 이벤트를 쏜다
└──────────────────────────────────────────────────────────
        ▲ 일을 시킨다(호출)              │ 이벤트(구독)
        │                                ▼
┌─ UI 층 ──────────────────────  Assets/Scripts_Client/UI/   ← 이 폴더
│   입력을 받아 매니저에 넘기고, 이벤트를 받아 그린다.
│   ※ 로직·상태·저장을 갖지 않는다.
└──────────────────────────────────────────────────────────
```

**UI는 일을 하는 곳이 아니라 넘기는 곳이다.** 이 한 줄이 아래 규칙 전부의 근거다.

```csharp
// 좋다 — 넘기기만 한다. 위젯이 20개가 되어도 20줄이다.
BindToggle(topmostToggle, window.Topmost, window.SetTopmost);

// 나쁘다 — 로직이 UI로 새어 들어왔다. 위젯 수 × 로직 줄 수로 폭발한다.
topmostToggle.onValueChanged.AddListener(on => {
    var hwnd = Win32Native.GetActiveWindow();
    Win32Native.SetWindowPos(hwnd, on ? -1 : -2, ...);
    PlayerPrefs.SetInt("Topmost", on ? 1 : 0);
});
```

> **UI 클래스가 두꺼워지면 쪼갤 신호가 아니라, 로직을 매니저로 밀어낼 신호다.**

---

## 2. 이름 규칙 — 접미사는 **붙는 오브젝트**를 따른다

`<대상><오브젝트 종류>`

| 붙는 오브젝트 | 접미사 | 예 |
|---|---|---|
| `Canvas` 컴포넌트가 있는 오브젝트 | `...CanvasUI` | `StorageCanvasUI` · `MarketCanvasUI` · `LoginCanvasUI` |
| 캔버스 안의 패널 오브젝트 | `...PanelUI` | `LoginPanelUI` · `InventoryPanelUI` · `GachaPanelUI` |
| **반복되는 한 칸** (보통 프리팹) | `...View` | `InventorySlotView` · `WorkStationSlotView` · `WorkStationAssignView` |
| 배치를 계산하는 컴포넌트 | `...Layout` / `...LayoutGroup` | `WidgetPositionLayout` · `FlexibleGridLayoutGroup` |

**`View`만 `UI`가 안 붙는다.** 나머지는 "화면 한 덩어리"지만 `View`는 "데이터 하나를 그리는 것"이라
성격이 달라서다. 프리팹이나 반복되는 칸에 붙고, N개가 복제되며, 자기 몫의 데이터만 본다.

### 오브젝트 이름이 먼저 맞아야 한다

스크립트 이름은 오브젝트에서 나온다. **캔버스 바로 아래 자식은 `Title` 말고 전부 `... Panel`로 끝난다.**

```
Storage Canvas                      ← StorageCanvasUI
  ├ Title
  ├ Select Panel
  ├ Inventory Scroll View Panel     ← InventoryPanelUI
  │   └ Viewport / Content
  │        └ Slot (1..N)            ← InventorySlotView   (프리팹)
  └ Information Panel
```

---

## 3. 어디에 붙이나

**오브젝트 1개 = 그 종류의 스크립트 1개.** 겹쳐 붙이지 않는다.

| | 규칙 |
|---|---|
| Canvas 스크립트 | `Show(bool)`을 갖는다. `UIManager`가 이걸 부른다. **자기 안의 패널을 갈아 끼우는 것도 여기서** |
| Panel 스크립트 | 그 화면의 위젯을 `[SerializeField]`로 받아 `Start()`에서 배선한다 |
| View 스크립트 | 반복되는 칸(프리팹 루트)에 붙는다. 자기 인덱스·데이터만 본다 |
| Layout 컴포넌트 | 배치를 계산하는 오브젝트에. 다른 스크립트와 **같은 오브젝트에 공존해도 된다** |

Canvas 오브젝트에 Panel 스크립트를 얹지 않는다. 캔버스가 담는 건 패널이고, 내용은 패널이 그린다.

### 패널끼리 갈아 끼울 때 — 패널은 서로를 모른다

열 폭이 좁아 두 화면을 나란히 못 두면 **같은 자리를 갈아 끼운다.** 이때 패널이 다음 패널을
직접 켜지 않는다. **이벤트를 쏘고, 캔버스가 정한다.**

```csharp
// 목록 패널 — 눌렸다고 알리기만 한다. 어디로 갈지는 모른다.
public event Action<int>? SlotClicked;

// 캔버스 — 무엇을 열지 정하는 유일한 곳
slotListPanel.SlotClicked += ShowSelect;
selectPanel.Closed        += ShowSlotList;
```

패널이 서로를 참조하면 화면이 3개, 4개로 늘 때 참조가 그물이 된다. `UIManager`가 3열에 대해
하는 일을 캔버스가 자기 패널들에 대해 하는 것이다 — **같은 규칙이 한 층 아래에 반복된다.**

> 꺼져 있는 패널을 열 때는 **인자를 먼저 넣고 켠다**(`Open(slotIndex)` 안에서 `SetActive(true)`).
> 꺼진 오브젝트는 `Start()`가 아직 안 돌았을 수 있어, 켠 직후 값을 넣으면 초기화가 덮어쓴다.

---

## 4. 버튼을 추가할 때 — 판단 흐름

```
버튼(또는 토글/드롭다운)을 하나 더 붙이려 한다
        │
        ├─ 같은 것이 N개 반복되나? (파라미터만 다른가)
        │     예 → View 컴포넌트 1개 만들고 N번 복제한다
        │           (WorkStationAssignView 가 slotIndex 만 다른 것처럼)
        │
        ├─ 그 위젯이 자기만의 상태를 구독해서 자기 모습을 바꾸나?
        │     예 → View
        │
        ├─ 화면(탭)이 통째로 하나 더 생기나?
        │     예 → Panel 을 새로 만든다  (로그인 / 회원가입)
        │
        └─ 그냥 기능 위젯 하나 더인가?
              예 → 기존 Panel 의 Start() 에 1줄 추가. 끝.
```

**"버튼 하나 = 스크립트 하나"는 하지 않는다.** 로직은 어차피 매니저에 있으므로,
버튼마다 클래스를 만들면 `Start()`·`RequireRef`·`Services.Get()` 보일러플레이트만 N배가 되고
얻는 게 없다.

> ⚠️ `button.onClick.AddListener(...)`는 **이미 옵저버 패턴이다.**
> "옵저버로 바꿀까"라는 선택지는 없다. 정할 건 **구독자를 몇 개 둘 것인가**뿐이다.

---

## 5. 폴더 규칙 — 캔버스 폴더 / 패널 폴더

**`캔버스/패널/` 두 겹이다.** 캔버스 스크립트는 캔버스 폴더 바로 아래,
패널 스크립트와 그 패널이 쓰는 View는 패널 폴더 안에 함께 둔다.

```
UI/
├─ Login/
│   ├─ LoginCanvasUI.cs
│   └─ LoginPanel/
│       └─ LoginPanelUI.cs
├─ Storage/
│   ├─ StorageCanvasUI.cs
│   └─ InventoryScrollViewPanel/
│       ├─ InventoryPanelUI.cs
│       └─ InventorySlotView.cs
├─ WorkStation/
│   ├─ WorkStationCanvasUI.cs
│   ├─ WorkStationScrollViewPanel/
│   │   ├─ WorkStationScrollViewPanelUI.cs
│   │   └─ WorkStationSlotView.cs
│   └─ SelectPanel/
│       ├─ WorkStationSelectPanelUI.cs
│       └─ CharacterStateRowView.cs
├─ Market/
│   ├─ MarketCanvasUI.cs
│   └─ GachaPanel/
│       └─ GachaPanelUI.cs
├─ State/            ← 독립 캔버스 (작업슬롯 열 안에 있지만 자기 Canvas 다)
│   └─ StateCanvasUI.cs
├─ Widget/
│   └─ WidgetCanvasUI.cs
├─ Settings/         ← 예외. 아래 설명
│   └─ SettingsPanelUI.cs
└─ Layout/           ← 예외. 화면이 아니라 배치 계산
    ├─ FlexibleGridLayoutGroup.cs
    ├─ SquareLayoutElement.cs
    ├─ WidgetPositionLayout.cs
    └─ Editor/
        └─ FlexibleGridLayoutGroupEditor.cs
```

- **폴더 이름 = 오브젝트 이름에서 공백을 뺀 것.** `Inventory Scroll View Panel` → `InventoryScrollViewPanel/`
- **빈 폴더는 만들지 않는다.** 패널 스크립트가 생길 때 그 폴더를 만든다.
  (`Select Panel`·`Information Panel`·`Menu Panel`은 아직 스크립트가 없어 폴더도 없다)
- 에디터 전용 스크립트는 반드시 `Editor/` 하위에 둔다. 안 그러면 빌드에 포함돼 컴파일이 깨진다.
- 매니저는 이 폴더에 두지 않는다 → `Assets/Scripts_Client/Managers/`

### 예외 두 개

| 폴더 | 왜 예외인가 |
|---|---|
| `Layout/` | 화면이 아니라 **배치 계산**이다. 어느 캔버스에도 속하지 않고 여러 곳에서 쓴다 |
| `Settings/` | 설정은 특정 캔버스의 기능이 아니다. **어느 캔버스에 얹혀도 되는 독립 화면**이고, 실제로 거래 열 → 작업슬롯 열로 옮겨 갈 예정이라 캔버스 폴더에 묶지 않는다 |

### `Debug/` 폴더는 없다

임시 UI라도 **제자리에 둔다.** 가챠 버튼은 `Market/GachaPanel/`, 작업슬롯 배치 버튼은
`WorkStation/WorkStationScrollViewPanel/`이다. 임시라는 건 **클래스 주석에 적는다** —
폴더로 나누면 정식이 될 때 폴더를 옮기는 일이 한 번 더 생긴다.

---

## 6. 공통 작성 규약

모든 UI 스크립트가 같은 뼈대를 쓴다.

```csharp
public class XxxPanelUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("... OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button someButton = null!;

    private PlayerDataManager _data = null!;
    private bool _isSubscribed;
    private bool _isReady;   // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다
    private void Start()
    {
        this.RequireRef(someButton, nameof(someButton));   // 미연결이면 즉시 예외 (fail-fast)

        _data = Services.Get<PlayerDataManager>();         // ※ 반드시 Start

        Subscribe();
        someButton.onClick.AddListener(OnClicked);
        Refresh();                                          // 이미 데이터가 와 있을 수 있다

        _isReady = true;
    }

    private void OnEnable()  { if (_isReady) { Subscribe(); Refresh(); } }
    private void OnDisable() { Unsubscribe(); }
}
```

지켜야 하는 것:

| 규칙 | 이유 |
|---|---|
| **`onClick`은 코드로 연결한다.** 인스펙터에서 연결하지 않는다 | 씬 파일에 묻혀 검색이 안 되고, 메서드 이름을 바꾸면 조용히 끊긴다 |
| **`Services.Get<T>()`는 반드시 `Start()`** 에서 | `Awake`·`OnEnable`은 등록 순서가 보장되지 않는다 |
| **필수 참조는 `= null!` + `RequireRef`** | `?`로 두면 미연결이 조용히 무시돼 "왜 안 되지"가 된다. 선택 참조만 `?` |
| **`OnEnable`에서 재구독하고 다시 그린다** | 꺼져 있는 동안 도착한 이벤트를 놓쳤다. 재구독만으론 화면이 낡은 채 남는다 |
| **`OnDisable`에서 반드시 구독 해제** | 안 하면 꺼진 UI가 계속 반응한다 |
| **매 프레임 도는 건 한 곳에만** | 슬롯마다 `Update`를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다. 계산은 부모가 하고 View엔 결과만 넘긴다 |
| **주석은 한글로** | 프로젝트 공통 |

---

## 7. Canvas를 다룰 때의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **Sorting Order를 올려도 계속 뒤에 그려진다** | 중첩 Canvas는 `Override Sorting`을 켜야 `Sorting Order`가 먹는다. 끄면 숫자가 **통째로 무시**되고 계층 순서로만 그려진다 | `Override Sorting` 체크 |
| **앞에는 나오는데 버튼이 안 눌린다** | `Override Sorting`을 켠 Canvas는 **자기 `GraphicRaycaster`** 가 필요하다 | `GraphicRaycaster` 추가 |
| **자식으로 옮겼더니 화면에서 사라졌다** | 루트 Canvas일 땐 Unity가 `localScale`을 관리해 줬다. 자식이 되면 저장된 값이 그대로 적용된다 | `localScale`을 `1,1,1`로 |
| **창 배율을 바꾸면 그 Canvas만 안 따라간다** | `CanvasScaler`가 `Constant Pixel Size`(기본값) | `Scale With Screen Size` / `1920×1080` / `Match = 1(Height)` — **Root Canvas와 동일하게** |
| 열을 껐더니 다른 열들이 가운데로 몰린다 | Column을 껐다 | Column이 아니라 **그 안의 Canvas만** 끈다 (`UIManager` 주석) |

**Sorting Order는 띄엄띄엄 준다** — `Login = 100`, `Log = 200`. 사이에 끼워 넣을 일이 반드시 생긴다.

---

## 7-2. 레이아웃 그룹의 함정

| 증상 | 원인 | 해결 |
|---|---|---|
| **자식들이 폭을 똑같이 나눠 갖는다** | `Child Force Expand Width`가 켜져 있다. 이건 "남는 폭을 **모두에게 균등 분배**"라서, 한 자식만 늘리고 싶을 때는 정반대로 동작한다 | **끄고**, 늘릴 자식에만 `LayoutElement.flexibleWidth = 1` |
| **`Preferred Height`를 줬는데 안 먹는다** | 같은 오브젝트의 `ScrollRect` 등이 자식 RectTransform을 따로 건드린다 | 안 쓰는 `ScrollRect`를 뗀다 |
| 높이 합이 부모를 넘친다 | 레이아웃에 빠진 자식이 있다 (`LayoutElement` 없이 큰 preferred를 가진 것) | 모든 자식에 높이 정책을 준다 — 고정은 `preferredHeight` + `flexibleHeight = 0`, 나머지를 채울 하나만 `flexibleHeight = 1` |
| **"높이만큼 정사각형"이 안 된다** | UGUI는 **가로를 먼저 다 정하고 세로를 정한다.** 가로를 정할 때 자기 높이가 아직 없다 | `SquareLayoutElement` (`UI/Layout/`) — 부모 높이를 보고 가로를 주장한다 |
| **내용이 늘어도 스크롤이 안 늘어난다** | `Viewport`에 직접 자식을 넣었다. Viewport는 **크기가 고정**이라 내용이 늘어도 커지지 않는다. `ScrollRect.content`도 비어 있으면 스크롤은 아예 동작하지 않는다 | 아래 "스크롤 뷰의 정석" |
| **내용이 스크롤바 밑으로 깔린다** | 자식이 자기 폭(패널 전체폭)을 주장한다. Viewport는 스크롤바만큼 좁다 | `Content`의 레이아웃 그룹에서 `Child Control Width`를 켜 폭을 넘겨받게 한다 |

### 스크롤 뷰의 정석

```
Scroll View Panel   ScrollRect   content = Content · viewport = Viewport   ← 둘 다 반드시 채운다
├─ Viewport         Image + Mask   sizeDelta (-17, 0)      ← 스크롤바 폭만큼 좁다. 레이아웃 그룹을 두지 않는다
│   └─ Content      LayoutGroup + ContentSizeFitter(v = PreferredSize)
│                   anchor (0,1)~(1,1)  pivot (0,1)        ← 위에 붙어서 아래로 자란다
│                   ChildControlWidth = on                 ← 줄 폭을 여기서 정해 스크롤바 침범을 막는다
│       └─ 줄 / 칸  LayoutElement preferredHeight 고정
└─ Scrollbar Vertical
```

**`Viewport`는 창이고 `Content`가 두루마리다.** 창에 직접 붙이면 두루마리가 없어 감을 수 없다.

### 세로 3단 배치의 정석

```
부모      VerticalLayoutGroup   ctrl(W,H)=on  expand(W)=on  expand(H)=off
├─ 머리   LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
├─ 탭     LayoutElement  preferredHeight 50   flexibleHeight 0    ← 고정
└─ 본문   LayoutElement  preferredHeight -1   flexibleHeight 1    ← 나머지를 전부
```

`expand(H)`를 켜면 고정하려던 칸까지 늘어난다. **높이를 나누는 건 `flexibleHeight`지 `expand`가 아니다.**

---

## 8. 지금 배치 (2026-08-08)

```
Root Canvas                                UIManager  (Managers/)
├─ Horizental Columns
│  ├─ Main Storage Column
│  │  └─ Storage Canvas                    StorageCanvasUI
│  │     ├─ Title
│  │     ├─ Tap Panel                      (아직 없음)
│  │     ├─ Inventory Scroll View Panel    InventoryPanelUI
│  │     │  └─ Content > Slot (N)          InventorySlotView   (런타임 생성)
│  │     └─ Information Panel              (아직 없음)
│  ├─ Main WorkStation Column
│  │  ├─ State Canvas                      StateCanvasUI
│  │  ├─ Workstation Canvas                WorkStationCanvasUI      목록 ↔ 선택 전환
│  │  │  ├─ Title
│  │  │  ├─ WorkStation Scroll View Panel  WorkStationScrollViewPanelUI
│  │  │  │  └─ Content > Work Slot (0..7)  WorkSlotFrame 프리팹 [Button]
│  │  │  │                                 + WorkStationSlotView (배치된 칸에만 런타임 생성)
│  │  │  ├─ Select Panel  (평소 꺼짐)       WorkStationSelectPanelUI
│  │  │  │  ├─ Header Panel                 h50   Title Text · Back Button(정사각형)
│  │  │  │  ├─ Industry Panel               h50   Farming~Hunting Button 5개
│  │  │  │  ├─ Character Assign Scroll View Panel   2단계 · 나머지
│  │  │  │  │  └─ Viewport > Content
│  │  │  │  │     └─ Character State Row    h50   CharacterStateRowView
│  │  │  │  └─ Character Setting Panel      3단계 · 나머지 (평소 꺼짐)
│  │  │  │     └─ Unassign Button
│  │  │  └─ Menu Panel                     (버튼 배선은 캔버스가 잡는다)
│  │  └─ Widget Canvas                     WidgetCanvasUI
│  └─ Main Market Column
│     └─ Market Canvas                     MarketCanvasUI
│        ├─ Title
│        ├─ Gacha Panel                    GachaPanelUI
│        └─ Setting Panel                  SettingsPanelUI + WidgetPositionLayout
└─ Login Canvas                            LoginCanvasUI
   └─ Login Panel                          LoginPanelUI
```

### 작업슬롯 화면 흐름

```
1  WorkStation Scroll View Panel     칸 8개
       │ 칸을 누른다 → SlotClicked(slotIndex) → 캔버스가 Select Panel 을 연다
       │
       ├── 빈 칸 ──────────────→ 2
       └── 이미 배치된 칸 ──────→ 3

   ┌─ Select Panel ─ Header(뒤로가기) · Industry(산업 5개) 는 2·3 모두에서 보인다 ─┐
   │                                                                              │
2  │  Character Assign Scroll View Panel    캐릭터 줄 목록                        │
   │      │ 줄의 [배치] → 배치 요청 → 응답 성공 ──→ 3                             │
   │      │                                                                       │
3  │  Character Setting Panel               배치된 캐릭터 세팅                    │
   │      │ [해제] → 해제 요청 → 응답 성공 ──────→ 2                              │
   └──────┴─ [뒤로가기] 또는 응답 실패 ───────────→ 1 ────────────────────────────┘
```

**요청이 성공한 뒤에 슬롯 목록으로 튕기지 않는다.** 배치했으면 이어서 세팅할 것이고, 해제했으면
이어서 다른 캐릭터를 고를 것이기 때문이다.

**단계는 응답을 보고 정한다.** 누르자마자 넘어가면 서버가 거절해도 넘어간다 — 아직 열리지 않은
슬롯에 배치를 걸면 실제로는 아무 일도 없는데 세팅 화면이 뜬다. 그래서 `WorkStationAssignCompleted`를
기다렸다가 성공이면 다음 단계로, **실패면 슬롯 목록으로 물러난다.**

> 기다리는 동안 배치·해제 버튼은 잠그되 **뒤로가기는 잠그지 않는다.** 응답이 영영 안 와도
> 나갈 길은 있어야 한다.

산업 버튼은 두 단계 모두에서 **잠기지 않는다.** 2에서는 캐릭터를 걸러 보는 수단이고,
3에서는 다른 산업으로 갈아 끼우는 수단이라서다. 고른 것은 `interactable`이 아니라
**`colors.normalColor`로만** 표시한다 — `Selectable`이 실행 중 `Image.color`를 덮어쓰기 때문이다.

### 알려진 임시 상태

- **`Character State Row`를 씬에 20줄 깔아 두고 풀처럼 쓴다.** 보유 캐릭터 수만큼만 켜고 나머지는 끈다.
  캐릭터가 20을 넘으면 그때 줄을 프리팹으로 빼고 필요한 만큼 만든다 — 그전까지는 씬에서 줄 모양을
  눈으로 보며 다듬는 편이 낫다.
- **산업 버튼 5개는 고를 뿐 캐릭터를 걸러 내지 않는다.** 고른 산업이 배치 요청에 실릴 뿐,
  그 산업을 못 다루는 캐릭터도 목록에 그대로 뜬다. → 일감 "작업슬롯 선택 패널"
- **`Setting Panel`이 거래 열에 있다.** 작업슬롯 열로 옮길 예정이다. 옮기면 `Settings/` 폴더도
  그때 위치를 다시 본다.

> 씬의 `m_EditorClassIdentifier`에 옛 클래스 이름이 남아 있어도 **문제 없다.**
> 스크립트 연결은 GUID로 이뤄지고, 그 문자열은 다음 씬 저장 때 Unity가 갱신한다.
