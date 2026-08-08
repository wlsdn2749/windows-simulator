---
name: clean-code-style
description: Unity/C# 클린 코드 스타일 규칙. 코드 작성 및 리뷰 시 이 규칙을 따른다.
---

> 최종 업데이트: 2026-07-18

# Unity/C# 클린 코드 스타일

---

## 1. C# 명명 규칙

| 대상 | 형식 | 예시 |
|------|------|------|
| 클래스, 메서드, Public 필드 | PascalCase | `PlayerController`, `CalculateTrajectory()` |
| 변수, 매개변수 | camelCase | `healthPoints`, `targetPosition` |
| Private 멤버 필드 | `_` + camelCase | `_maxHealth`, `_isDead` |
| Boolean | `is/has/can` 접두사 | `isDead`, `hasHealthPotion`, `canJump` |
| Interface | `I` 접두사 | `IDamageable`, `IInteractable` |
| Enum | 단수형 PascalCase | `WeaponType { Knife, Gun }` |
| 이벤트 | 과거형 동사 | `DoorOpened`, `PointsScored` |
| 이벤트 핸들러 | `On` + 이벤트명 | `OnDoorOpened`, `OnPointsScored` |

**금지:**

```csharp
// Bad — 약어, 한 글자, 불명확한 이름
float hp;
Vector3 pos;
GameObject obj;
string temp;

// Good
float healthPoints;
Vector3 targetPosition;
GameObject enemyObject;
string playerName;
```

---

## 2. MonoBehaviour 클래스 구성 순서

```csharp
public class PlayerController : MonoBehaviour
{
    // 1. Public Fields
    public float DamageMultiplier = 1.5f;

    // 2. [SerializeField] Private Fields
    [SerializeField] private float _maxHealth;

    // 3. Private Fields
    private bool _isDead;

    // 4. Properties
    public float MaxHealth => _maxHealth;
    public bool IsDead => _isDead;

    // 5. Events / Delegates
    public event Action<int> PointsScored;
    public event Action OnDied;

    // 6. MonoBehaviour 생명주기 (Awake → OnEnable → Start → Update → OnDisable → OnDestroy)
    private void Awake() { }
    private void Start() { }
    private void Update() { }

    // 7. 나머지 메서드는 사용 단계별 #region 으로 그룹화 (아래 규칙 참조)
    #region 초기화
    public void Setup() { }
    #endregion

    #region 게임 진행
    public void InflictDamage(float damage) { }
    private void Die() { }
    #endregion
}
```

### 메서드 그룹화 — 사용 단계별 #region

필드 → 프로퍼티 → 이벤트 → **생명주기**까지는 상단에 그대로 두고(영역 없음),
그 아래 나머지 메서드는 **사용 단계별 `#region`** 으로 묶는다. Rider 접기로 긴 클래스 탐색이 쉬워진다.

- 표준 묶음: `초기화` / `게임 진행` / `종료 · 결과` / `보조`. 클래스 성격에 맞으면 `입력` / `손패 · 배치` 같은 **관심사 기준**도 가능.
- region **내부 순서는 호출 흐름 우선**(public/private 엄격 분리보다 읽는 순서 우선).
- **소형 클래스**(메서드 3~5개 이하)는 region 없이 주석만 — 오버엔지니어링 금지.

---

## 3. 포맷팅

### Allman 스타일 — 중괄호 항상 별도 줄

```csharp
// Good
if (!showMouse)
{
    Cursor.lockState = CursorLockMode.Locked;
}

// Bad
if (!showMouse) Cursor.lockState = CursorLockMode.Locked;
if (!showMouse) { Cursor.lockState = CursorLockMode.Locked; }
```

### 수평 간격

```csharp
// Good — 연산자 전후 공백, 콤마 뒤 공백
float result = a + b * c;
Vector3 position = new Vector3(0f, 1f, 0f);

// Bad
float result=a+b*c;
Vector3 position=new Vector3(0f,1f,0f);
```

### 수직 간격

```csharp
// Good — 관련 있는 코드는 묶고, 논리 단위 사이에 빈 줄 1개
private void Update()
{
    HandleInput();

    UpdateMovement();

    CheckGroundState();
}
```

---

## 4. Properties & Serialization

### 단일 표현식 프로퍼티

```csharp
// Good — 읽기 전용 프로퍼티는 => 사용
public float MaxHealth => _maxHealth;
public bool IsAlive => _currentHealth > 0f;
```

### Inspector 어트리뷰트

```csharp
[Header("이동 설정")]
[SerializeField, Tooltip("좌우 이동 속도 (m/s)")]
[Range(1f, 20f)]
private float _moveSpeed = 5f;

[Header("점프 설정")]
[SerializeField, Tooltip("점프 힘")]
private float _jumpForce = 10f;
```

**규칙:**
- Inspector 설명은 코드 주석 대신 `[Tooltip]` 사용
- 수치 범위가 있으면 `[Range]` 필수
- 섹션 구분은 `[Header]` 사용

---

## 5. 메서드 설계

### 파라미터 개수 제한

```csharp
// Bad — 파라미터 3개 이상
public void SetupEnemy(float health, float speed, float damage, bool isBoss) { }

// Good — 구조체/클래스로 묶기
public void SetupEnemy(EnemyData data) { }
```

### Flag 파라미터 금지

```csharp
// Bad — true/false가 무슨 의미인지 호출부에서 모름
public float GetAngle(bool inDegrees) { }
GetAngle(true);

// Good — 의도가 명확한 별도 메서드
public float GetAngleInDegrees() { }
public float GetAngleInRadians() { }
```

---

## 6. 주석 규칙

### WHY를 설명, WHAT은 코드로

```csharp
// Bad — 코드가 이미 말하는 내용을 반복
int count = 0; // 카운트를 0으로 초기화

// Good — 코드만으로 알 수 없는 이유를 설명
// 물리 엔진이 FixedUpdate 이후에 적용되므로, 한 프레임 지연 후 체크
private IEnumerator CheckGroundNextFrame() { }
```

### Public API — XML 문서 주석

```csharp
/// <summary>
/// 플레이어에게 데미지를 적용하고 사망 여부를 반환합니다.
/// </summary>
/// <param name="damage">적용할 데미지 양 (0 이상)</param>
/// <returns>이 데미지로 사망했으면 true</returns>
public bool ApplyDamage(float damage) { }
```

### [Tooltip] vs 주석 선택 기준

| 상황 | 사용 |
|------|------|
| Inspector에 보이는 필드 설명 | `[Tooltip]` |
| 코드 실행 이유, 알고리즘 설명 | `//` 주석 |
| Public 메서드/클래스 API 문서 | XML `///` |

### 메서드 1줄 요약 + 바인딩 표기 (이 프로젝트 기준)

**모든 메서드 위에 1줄 요약 주석**을 단다(자명한 한 줄 getter 제외). 특히 **호출 경로가 코드만으론 안 보이는** 메서드는
어디서 불리는지를 괄호로 명시한다 — 구독/버튼/엔진 메시지 구분이 핵심.

| 종류 | 표기 예시 |
|------|-----------|
| Unity 메시지 | `// 카드 누름 — 드래그 시작 (Unity 마우스 메시지)` / `// 턴 이벤트 구독 (Unity 메시지)` |
| 이벤트 구독 핸들러 | `// 내 턴 시작 시 배치 카운트 초기화 (OnTurnStarted 구독)` |
| UI 버튼 OnClick | `// 게임 시작 (GameStartBtn OnClick에 할당)` |
| 다른 클래스가 호출 | `// 공격 실행 (EntityManager·EnemyAI가 호출)` |

라인 단위로도 의미가 갈리는 곳엔 인라인 주석을 붙인다(열 맞춰 정렬):
```csharp
_notificationPanel.ScaleZero();     // 알림 패널 숨김
_resultPanel.ScaleZero();           // 결과 패널 숨김
_titlePanel.Active(true);           // 타이틀 패널 켜기
```

---

## 7. 피해야 할 코드 스멜

| 스멜 | 설명 | 해결 |
|------|------|------|
| 불명확한 이름 | `data`, `info`, `temp`, `manager2` | 역할을 명확히 표현하는 이름 |
| 과도한 주석 | 나쁜 코드를 주석으로 설명 | 코드 개선이 우선 |
| Flag 파라미터 | `DoSomething(true, false)` | 별도 메서드로 분리 |
| 매직 넘버 | `if (health < 30f)` | `const float LowHealthThreshold = 30f;` |
| 중첩 조건문 | 3단계 이상 if 중첩 | 조기 반환(guard clause)으로 평탄화 |

---

## 8. 나의 코드 스타일 (개인 취향)

> 이 프로젝트는 개인 작업이므로, 아래 스타일을 위 규칙과 함께 적용한다.
> 가독성을 높이기 위한 선택으로, 널리 사용되는 스타일이다.

### 필드 열 정렬 (Column Alignment)

같은 modifier 그룹 내에서 타입명을 공백으로 맞춰 **변수명 열을 정렬**한다.

```csharp
// Good — 타입명 뒤 공백으로 변수명 열 맞춤
[SerializeField] private GridPool               _gridPool;
[SerializeField] private CameraScrollController _cameraScroll;
[SerializeField] private FloorGrid              _startGrid;
[SerializeField] private float                  _gridHeight = 10f;
[SerializeField] private int                    _totalFloors = 10;

private int  _currentFloor = 1;
private bool _isTransitioning;

// Bad — 열 정렬 없이 나열
[SerializeField] private GridPool _gridPool;
[SerializeField] private CameraScrollController _cameraScroll;
[SerializeField] private float _gridHeight = 10f;

private int _currentFloor = 1;
private bool _isTransitioning;
```

**규칙:**
- 정렬 단위는 **같은 modifier 그룹** (`[SerializeField] private` / `private` / `private readonly` 등)
- 그룹이 달라지면 정렬 기준 리셋 (빈 줄로 구분)
- `const`, `readonly` 등 키워드가 다르면 별도 그룹으로 분리

```csharp
// 그룹별 정렬 예시
private readonly Dictionary<int, FloorGrid> _floorGridMap = new();

private int  _currentFloor = 1;
private bool _isTransitioning;

private const int PreloadCount = 2;
```

### const / static readonly 는 클래스 최상단

상수는 멤버 필드보다 위, 클래스 선언 직후(`Inst`·enum 다음)에 모아 둔다.

```csharp
public class Order : MonoBehaviour
{
    private const int OrderMultiplier = 10;
    private const int MostFrontOrder  = 100;

    [SerializeField] private Renderer[] _backRenderers;
}
```

### modifier가 다르면 그룹을 나누고, 필요하면 변수를 추가해 타입 열을 맞춘다

`readonly` 같은 키워드 때문에 타입 열이 어긋나면, **변수를 하나 더 만들어서라도**
같은 modifier 그룹으로 묶어 타입 열을 정렬한다. (가변 1개만 별도 그룹으로 분리)

```csharp
// Bad — readonly 유무가 섞여 WaitForSeconds 열이 어긋남
private WaitForSeconds _delay05 = new WaitForSeconds(0.5f);
private readonly WaitForSeconds _delay07 = new WaitForSeconds(0.7f);

// Good — readonly 그룹으로 타입 열을 맞추고, 가변 선택 필드만 분리
private readonly WaitForSeconds _addCardDelay     = new WaitForSeconds(0.5f);
private readonly WaitForSeconds _fastAddCardDelay = new WaitForSeconds(0.05f);
private readonly WaitForSeconds _turnDelay        = new WaitForSeconds(0.7f);

private WaitForSeconds _currentAddCardDelay; // 조건에 따라 선택되는 현재 딜레이
```

### event vs Action — 의도를 주석으로 남긴다

- **`event Action<T>`**: 선언 클래스 내부에서만 `Invoke` 가능 → 순수 발행-구독.
- **`Action<T>`(event 없이)**: 외부 클래스에서도 `Invoke` 해야 할 때(치트, 디버그 등).

둘을 의도적으로 구분해 쓸 때는 **왜 그렇게 했는지** 주석으로 남긴다.

```csharp
// 외부(GameManager 치트 등)에서도 Invoke 해야 하므로 event가 아닌 Action으로 둔다
public static Action<bool> OnAddCard;

// 내부에서만 발행하는 순수 발행-구독 이벤트 (외부 Invoke 차단)
public static event Action<bool> OnTurnStarted;
```

### 인스펙터 섹션은 [CenterHeader] 로 구분

`[SerializeField]`(또는 직렬화되는 public) 필드가 3개 이상이면 역할별로 헤더를 붙여 가독성을 높인다.
기본 `[Header]`는 좌측 정렬만 되어 구분이 약하므로, 이 프로젝트는 **가운데 정렬 커스텀 헤더 `[CenterHeader]`** 를 쓴다.
(`<스크립트 루트>/Common/Attribute/CenterHeaderAttribute.cs` + `<스크립트 루트>/Common/Editor/CenterHeaderDrawer.cs`.
스크립트 루트는 1인 개발 `Assets/Scripts`, 협업(클라이언트) `Assets/Scripts_Client` —
프로젝트별 실제 경로는 해당 프로젝트 `CLAUDE.md` 의 폴더 구조 표를 따른다)

텍스트는 `< 참조 >` 처럼 양쪽을 꺾쇠로 감싸 구분을 더 또렷하게 한다.

```csharp
[CenterHeader("< 참조 >")]
[SerializeField] private ItemSO _itemSO;

[CenterHeader("< 상태 >")]
[SerializeField] private ECardState _cardState;
```

### 폴더 구성 — 역할별로 세분화

스크립트가 늘어날 때 비슷한 것끼리 빨리 찾도록 역할별 폴더로 나눈다.
**스크립트 루트**는 1인 개발 `Assets/Scripts/`, 협업(클라이언트 담당) `Assets/Scripts_Client/` 다.

```
<스크립트 루트>/
├── Common/        범용 공통 코드 (마스터 templates/code/ 에서 설치됨)
│   ├── Attribute/   런타임 어트리뷰트
│   ├── Editor/      에디터 전용 — 드로어·툴 (빌드 제외)
│   ├── Service/     서비스 로케이터 (Services · MonoService)
│   └── Extensions/  확장 메서드 (MonoBehaviourExtensions 등)
├── Gameplay/      도메인 스크립트
│   └── AI/
│       ├── Contracts/       이 기능이 정의한 계약 (예 : IOpponent)
│       ├── ComputerOpponent.cs
│       └── RemoteOpponent.cs
├── Combat/
│   ├── Contracts/           예 : IDamageable — 때리는 쪽이 정의하므로 여기
│   └── CombatSystem.cs
├── Managers/
├── UI/
└── SO/
```

- **범용 공통 코드**(프로젝트를 옮겨도 그대로 쓰는 것)는 `Common/` 아래 위 분류로 넣는다.
  분류에 안 맞으면 `Common/<범주>/` 를 추가한다 (예 : `Common/Camera/`, `Common/Sorting/`).
- **인터페이스는 그것을 정의한 기능 폴더 안 `Contracts/`** 에 둔다. 계약 전용 최상위 폴더는 두지 않는다
  — 사용처가 늘었다고 계약을 옮기지 않는다. 자세한 규칙은 `feature-design` 3-1 참조.
- **에디터 전용 스크립트는 반드시 `Editor/` 이름의 폴더 안에 둔다.** 빌드에서 제외된다.
  반대로 런타임 코드를 여기 넣으면 빌드가 깨진다.
- 도메인 스크립트는 기존대로 `Gameplay/` · `Managers/` · `UI/` · `SO/`.
- 프로젝트가 이 표준과 다른 구조를 쓰면 해당 프로젝트 `CLAUDE.md` 의 폴더 구조 표를 따른다.
